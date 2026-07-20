using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Notifications;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CinemaSystem.Infrastructure.Services;

public sealed class NotificationService : INotificationService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly IClock _clock;
    private readonly ILogger<NotificationService> _logger;

    public NotificationService(
        CinemaDbContext dbContext,
        IEmailService emailService,
        IClock clock,
        ILogger<NotificationService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    private static string NewId() => CinemaSystem.Domain.Utilities.IdGenerator.NewId(DomainConstants.EntityIdPrefix.Notification);

    public async Task<ServiceResult<PagedList<NotificationResponse>>> GetNotificationsAsync(
        string userId,
        bool? isRead,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var userExists = await _dbContext.Users.AnyAsync(u => u.UserId == userId, cancellationToken);
        if (!userExists)
        {
            return ServiceResult<PagedList<NotificationResponse>>.Fail(
                404,
                "User not found.",
                "USER_NOT_FOUND");
        }

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (isRead.HasValue)
        {
            query = query.Where(n => n.IsRead == isRead.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var mappedList = notifications.Select(MapToResponse).ToList();
        var pagedList = new PagedList<NotificationResponse>(mappedList, totalCount, pageIndex, pageSize);

        return ServiceResult<PagedList<NotificationResponse>>.Ok(pagedList, "Notifications retrieved successfully.");
    }

    public async Task<ServiceResult<bool>> MarkAsReadAsync(
        string userId,
        List<string> notificationIds,
        CancellationToken cancellationToken)
    {
        if (notificationIds == null || notificationIds.Count == 0)
        {
            return ServiceResult<bool>.Fail(400, "Notification IDs must not be empty.", "INVALID_INPUT");
        }

        var notifications = await _dbContext.Notifications
            .Where(n => n.UserId == userId && notificationIds.Contains(n.NotificationId))
            .ToListAsync(cancellationToken);

        if (notifications.Count == 0)
        {
            return ServiceResult<bool>.Fail(404, "No matching notifications found for the user.", "NOTIFICATIONS_NOT_FOUND");
        }

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, "Notifications marked as read.");
    }

    public async Task<ServiceResult<bool>> MarkAllAsReadAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var userExists = await _dbContext.Users.AnyAsync(u => u.UserId == userId, cancellationToken);
        if (!userExists)
        {
            return ServiceResult<bool>.Fail(404, "User not found.", "USER_NOT_FOUND");
        }

        var notifications = await _dbContext.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            notification.IsRead = true;
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, "All notifications marked as read.");
    }

    public async Task<ServiceResult<NotificationResponse>> SendNotificationAsync(
        SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var targetUsers = new List<User>();

        if (!string.IsNullOrWhiteSpace(request.TargetGroup))
        {
            var group = request.TargetGroup.Trim().ToUpperInvariant();
            var query = _dbContext.Users.AsNoTracking();

            if (group == "CUSTOMERS")
            {
                query = query.Where(u => u.RoleId == AuthConstants.RoleIds.Customer);
            }
            else if (group == "STAFF")
            {
                query = query.Where(u => u.RoleId == AuthConstants.RoleIds.Staff);
            }
            else if (group == "MANAGERS")
            {
                query = query.Where(u => u.RoleId == AuthConstants.RoleIds.Manager);
            }
            else if (group == "ADMINS")
            {
                query = query.Where(u => u.RoleId == AuthConstants.RoleIds.Admin);
            }
            else if (group != "ALL")
            {
                return ServiceResult<NotificationResponse>.Fail(400, $"Unknown TargetGroup: '{group}'. Valid options are: ALL, CUSTOMERS, STAFF, MANAGERS, ADMINS.", "INVALID_TARGET_GROUP");
            }

            targetUsers = await query.ToListAsync(cancellationToken);
        }
        else if (request.UserIds != null && request.UserIds.Count > 0)
        {
            targetUsers = await _dbContext.Users
                .AsNoTracking()
                .Where(u => request.UserIds.Contains(u.UserId))
                .ToListAsync(cancellationToken);
        }
        else if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == request.UserId, cancellationToken);
            if (user != null)
            {
                targetUsers.Add(user);
            }
        }

        if (targetUsers.Count == 0)
        {
            return ServiceResult<NotificationResponse>.Fail(400, "No target users found for notification dispatch.", "NO_RECIPIENTS_FOUND");
        }

        var status = "Sent";
        var firstNotifId = string.Empty;

        foreach (var user in targetUsers)
        {
            if (request.Channel.Equals("Email", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    await _emailService.SendEmailAsync(user.Email, request.Title, request.Message, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send email notification to {Email}", user.Email);
                    status = "Failed";
                }
            }
            else if (request.Channel.Equals("SMS", StringComparison.OrdinalIgnoreCase) ||
                     request.Channel.Equals("Push", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation("Simulating {Channel} delivery to User {UserId}: [{Title}] {Message}", 
                    request.Channel, user.UserId, request.Title, request.Message);
            }

            var notifId = NewId();
            if (string.IsNullOrEmpty(firstNotifId))
            {
                firstNotifId = notifId;
            }

            var notification = new Notification
            {
                NotificationId = notifId,
                UserId = user.UserId,
                BookingId = request.BookingId,
                Title = request.Title,
                Message = request.Message,
                IsRead = false,
                CreatedAt = _clock.UtcNow
            };

            _dbContext.Notifications.Add(notification);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new NotificationResponse
        {
            NotificationId = firstNotifId,
            UserId = request.TargetGroup ?? (targetUsers.Count > 1 ? "MULTIPLE" : targetUsers[0].UserId),
            BookingId = request.BookingId,
            Title = request.Title,
            Message = request.Message,
            IsRead = false,
            CreatedAt = _clock.UtcNow,
            Channel = request.Channel,
            Type = request.Type,
            Status = status
        };

        var targetDescription = request.TargetGroup ?? (targetUsers.Count > 1 ? $"{targetUsers.Count} users" : targetUsers[0].UserId);
        return ServiceResult<NotificationResponse>.Ok(response, $"Notification successfully dispatched to {targetDescription}.");
    }

    public async Task<ServiceResult<bool>> TriggerSystemNotificationAsync(
        TriggerSystemNotificationRequest request,
        CancellationToken cancellationToken)
    {
        var type = request.Type.Trim();
        var title = "";
        var message = "";
        var targetRoles = new List<string>();
        var specificUserId = "";

        if (type.Equals("RoomCleanup", StringComparison.OrdinalIgnoreCase))
        {
            title = "Lệnh chuẩn bị phòng chiếu";
            var movieName = "Phim";
            var roomName = "Phòng chiếu";
            if (!string.IsNullOrEmpty(request.TargetId))
            {
                var showtime = await _dbContext.Showtimes
                    .AsNoTracking()
                    .Include(s => s.Movie)
                    .Include(s => s.Room)
                    .FirstOrDefaultAsync(s => s.ShowtimeId == request.TargetId, cancellationToken);
                if (showtime != null)
                {
                    movieName = showtime.Movie.Title;
                    roomName = showtime.Room.RoomName;
                }
            }
            message = request.Message ?? $"Suất chiếu phim '{movieName}' tại phòng '{roomName}' vừa kết thúc. Nhân viên vệ sinh chuẩn bị dọn dẹp phòng chiếu.";
            targetRoles.Add(AuthConstants.RoleIds.Staff);
        }
        else if (type.Equals("RoomReady", StringComparison.OrdinalIgnoreCase))
        {
            title = "Lệnh kiểm vé soát khách";
            var movieName = "Phim";
            var roomName = "Phòng chiếu";
            if (!string.IsNullOrEmpty(request.TargetId))
            {
                var showtime = await _dbContext.Showtimes
                    .AsNoTracking()
                    .Include(s => s.Movie)
                    .Include(s => s.Room)
                    .FirstOrDefaultAsync(s => s.ShowtimeId == request.TargetId, cancellationToken);
                if (showtime != null)
                {
                    movieName = showtime.Movie.Title;
                    roomName = showtime.Room.RoomName;
                }
            }
            message = request.Message ?? $"Phòng chiếu '{roomName}' đã dọn dẹp xong và sẵn sàng cho suất chiếu phim '{movieName}'. Nhân viên soát vé chuẩn bị đón khách (10-15 phút trước giờ chiếu).";
            targetRoles.Add(AuthConstants.RoleIds.Staff);
        }
        else if (type.Equals("TechnicalIssue", StringComparison.OrdinalIgnoreCase))
        {
            title = "SỰ CỐ KỸ THUẬT KHẨN CẤP";
            message = request.Message ?? $"Khẩn cấp: Thiết bị máy chiếu/âm thanh tại phòng chiếu '{request.TargetId ?? "Chưa rõ"}' gặp sự cố kỹ thuật. Đội kỹ thuật và quản lý ca trực kiểm tra ngay!";
            targetRoles.Add(AuthConstants.RoleIds.Staff);
            targetRoles.Add(AuthConstants.RoleIds.Manager);
            targetRoles.Add(AuthConstants.RoleIds.Admin);
        }
        else if (type.Equals("LowInventory", StringComparison.OrdinalIgnoreCase))
        {
            title = "Cảnh báo tồn kho thấp";
            message = request.Message ?? $"Cảnh báo kho hàng: Nguyên liệu quầy concession '{request.TargetId ?? "Hạt bắp/Siro"}' đã chạm mức tối thiểu để vận hành. Vui lòng nhập hàng.";
            targetRoles.Add(AuthConstants.RoleIds.Manager);
            targetRoles.Add(AuthConstants.RoleIds.Admin);
        }
        else if (type.Equals("ComboPreparation", StringComparison.OrdinalIgnoreCase))
        {
            title = "Lệnh chuẩn bị Combo (Concession)";
            var detailText = "";
            if (!string.IsNullOrEmpty(request.TargetId))
            {
                var booking = await _dbContext.Bookings
                    .AsNoTracking()
                    .Include(b => b.BookingFbItems)
                    .ThenInclude(bf => bf.FbItem)
                    .FirstOrDefaultAsync(b => b.BookingId == request.TargetId, cancellationToken);

                if (booking != null && booking.BookingFbItems.Count > 0)
                {
                    var items = string.Join(", ", booking.BookingFbItems.Select(i => $"{i.Quantity}x {i.FbItem.ItemName}"));
                    detailText = $" ({items})";
                }
            }
            message = request.Message ?? $"Quầy bắp nước: Khách hàng check-in vé '{request.TargetId ?? "Mã vé"}'{detailText}. Vui lòng chuẩn bị sẵn combo bắp nước để giảm thời gian chờ đợi.";
            targetRoles.Add(AuthConstants.RoleIds.Staff);
        }
        else if (type.Equals("ShiftChange", StringComparison.OrdinalIgnoreCase))
        {
            title = "Thông báo thay đổi ca trực";
            message = request.Message ?? "Lịch làm việc của bạn trong tuần/tháng này đã được quản lý thay đổi. Vui lòng cập nhật và bàn giao ca trực đúng giờ.";
            specificUserId = request.TargetId ?? string.Empty;
        }
        else if (type.Equals("EmergencyBroadcast", StringComparison.OrdinalIgnoreCase))
        {
            title = "Thông báo khẩn cấp Ban quản lý";
            message = request.Message ?? "Ban quản lý thông báo khẩn: Nhắc nhở nghiêm túc việc kiểm tra an toàn PCCC toàn rạp và điều phối nhân sự chống quá tải.";
            targetRoles.Add(AuthConstants.RoleIds.Staff);
            targetRoles.Add(AuthConstants.RoleIds.Manager);
            targetRoles.Add(AuthConstants.RoleIds.Admin);
        }
        else
        {
            return ServiceResult<bool>.Fail(400, $"Unknown notification trigger type: '{type}'.", "UNKNOWN_TYPE");
        }

        var userIds = new List<string>();

        if (!string.IsNullOrEmpty(specificUserId))
        {
            var userExists = await _dbContext.Users.AnyAsync(u => u.UserId == specificUserId, cancellationToken);
            if (userExists)
            {
                userIds.Add(specificUserId);
            }
        }
        else if (targetRoles.Count > 0)
        {
            userIds = await _dbContext.Users
                .AsNoTracking()
                .Where(u => targetRoles.Contains(u.RoleId))
                .Select(u => u.UserId)
                .ToListAsync(cancellationToken);
        }

        if (userIds.Count == 0)
        {
            return ServiceResult<bool>.Ok(false, "No active target users found to notify.");
        }

        var notifications = userIds.Select(userId => new Notification
        {
            NotificationId = NewId(),
            UserId = userId,
            BookingId = type.Equals("ComboPreparation", StringComparison.OrdinalIgnoreCase) ? request.TargetId : null,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = _clock.UtcNow
        }).ToList();

        _dbContext.Notifications.AddRange(notifications);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, $"System notification of type '{type}' broadcasted to {notifications.Count} users.");
    }

    public async Task<ServiceResult<IReadOnlyList<NotificationResponse>>> GetSignageFeedAsync(CancellationToken cancellationToken)
    {
        // Digital signage displays messages related to room ready or room cleanups in the last 6 hours
        var cutoff = _clock.UtcNow.AddHours(-6);
        var notifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.CreatedAt >= cutoff && (n.Title.Contains("dọn dẹp") || n.Title.Contains("soát vé") || n.Title.Contains("Room")))
            .OrderByDescending(n => n.CreatedAt)
            .Take(20)
            .ToListAsync(cancellationToken);

        var mapped = notifications.Select(MapToResponse).ToList();
        return ServiceResult<IReadOnlyList<NotificationResponse>>.Ok(mapped, "Signage feed retrieved successfully.");
    }

    public async Task<ServiceResult<IReadOnlyList<NotificationResponse>>> GetInternalFeedAsync(CancellationToken cancellationToken)
    {
        // Internal app feed displays general operations notifications in the last 24 hours
        var cutoff = _clock.UtcNow.AddHours(-24);
        var notifications = await _dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.CreatedAt >= cutoff && (n.Title.Contains("Lệnh") || n.Title.Contains("sự cố") || n.Title.Contains("Cảnh báo") || n.Title.Contains("khẩn cấp") || n.Title.Contains("Emergency")))
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        // Group/Distinct by title and message to keep clean feed (don't duplicate broadcasts)
        var distinctList = notifications
            .GroupBy(n => new { n.Title, n.Message })
            .Select(g => g.First())
            .Select(MapToResponse)
            .ToList();

        return ServiceResult<IReadOnlyList<NotificationResponse>>.Ok(distinctList, "Internal operational feed retrieved successfully.");
    }

    private NotificationResponse MapToResponse(Notification n)
    {
        var (channel, type, status) = DetermineMetadata(n.Title, n.Message);
        return new NotificationResponse
        {
            NotificationId = n.NotificationId,
            UserId = n.UserId,
            BookingId = n.BookingId,
            Title = n.Title,
            Message = n.Message,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt,
            Channel = channel,
            Type = type,
            Status = status
        };
    }

    private static (string Channel, string Type, string Status) DetermineMetadata(string title, string message)
    {
        var channel = "App";
        var type = "Transactional";
        var status = "Sent";

        var combined = $"{title} {message}".ToLowerInvariant();

        if (combined.Contains("cảnh báo") || combined.Contains("lệnh") || combined.Contains("khẩn cấp") || combined.Contains("sự cố"))
        {
            type = "Internal";
            channel = combined.Contains("kỹ thuật") ? "SMS" : "Internal";
        }
        else if (combined.Contains("voucher") || combined.Contains("sinh nhật") || combined.Contains("khảo sát") || combined.Contains("điểm thưởng"))
        {
            type = "Loyalty";
            channel = combined.Contains("sinh nhật") ? "Email" : "App";
        }
        else if (combined.Contains("khuyến mãi") || combined.Contains("promo") || combined.Contains("bom tấn") || combined.Contains("ưu đãi"))
        {
            type = "Promotional";
            channel = "Push";
        }

        if (combined.Contains("hủy") || combined.Contains("hoàn tiền") || combined.Contains("refund") || combined.Contains("xác nhận"))
        {
            channel = "Email";
        }

        return (channel, type, status);
    }
}
