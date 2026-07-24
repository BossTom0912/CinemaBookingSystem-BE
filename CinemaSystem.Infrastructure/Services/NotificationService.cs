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
        var user = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        if (user == null)
        {
            return ServiceResult<PagedList<NotificationResponse>>.Fail(
                404,
                "User not found.",
                "USER_NOT_FOUND");
        }

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Include(n => n.User)
            .ThenInclude(u => u.StaffProfile)
            .ThenInclude(sp => sp.Cinema)
            .AsQueryable();

        // For Customers, Staff, and Managers, filter by their own UserId.
        // For Admins in the Management Dashboard, return all system notifications.
        if (user.RoleId == AuthConstants.RoleIds.Customer ||
            user.RoleId == AuthConstants.RoleIds.Staff ||
            user.RoleId == AuthConstants.RoleIds.Manager)
        {
            query = query.Where(n => n.UserId == userId);
        }

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

    public Task<ServiceResult<NotificationResponse>> SendNotificationAsync(
        SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        return SendNotificationAsync(senderUserId: null, request, cancellationToken);
    }

    public async Task<ServiceResult<NotificationResponse>> SendNotificationAsync(
        string? senderUserId,
        SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        User? sender = null;
        if (!string.IsNullOrWhiteSpace(senderUserId) && !senderUserId.Equals("SYSTEM", StringComparison.OrdinalIgnoreCase))
        {
            sender = await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == senderUserId, cancellationToken);
        }

        // RBAC Enforcement Rules:
        // - Staff cannot send broadcast notifications.
        // - Manager can ONLY send notifications to Staff.
        // - Admin can send notifications to Manager, Staff, Customer, and ALL.

        if (sender != null && sender.RoleId == AuthConstants.RoleIds.Staff)
        {
            return ServiceResult<NotificationResponse>.Fail(403, "Staff members are not authorized to send broadcast notifications.", "FORBIDDEN_SENDER_ROLE");
        }

        if (sender != null && sender.RoleId == AuthConstants.RoleIds.Manager)
        {
            if (!string.IsNullOrWhiteSpace(request.TargetGroup))
            {
                var targetGroupUpper = request.TargetGroup.Trim().ToUpperInvariant();
                if (targetGroupUpper != DomainConstants.NotificationTargetGroup.Staff &&
                    targetGroupUpper != DomainConstants.NotificationTargetGroup.Admins &&
                    targetGroupUpper != "ADMIN" &&
                    targetGroupUpper != "ADMINS")
                {
                    return ServiceResult<NotificationResponse>.Fail(403, "Managers are authorized to send notifications to Staff or Admin.", "FORBIDDEN_TARGET_GROUP");
                }
            }
        }

        var targetUsers = new List<User>();

        var query = _dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.TargetGroup))
        {
            var group = request.TargetGroup.Trim().ToUpperInvariant();

            if (group == DomainConstants.NotificationTargetGroup.Customers)
            {
                query = query.Where(u => u.RoleId == AuthConstants.RoleIds.Customer);
            }
            else if (group == DomainConstants.NotificationTargetGroup.Staff)
            {
                query = query.Where(u => u.RoleId == AuthConstants.RoleIds.Staff);
            }
            else if (group == DomainConstants.NotificationTargetGroup.Managers)
            {
                query = query.Where(u => u.RoleId == AuthConstants.RoleIds.Manager);
            }
            else if (group == DomainConstants.NotificationTargetGroup.Admins || group == "ADMIN" || group == "ADMINS")
            {
                query = query.Where(u => u.RoleId == AuthConstants.RoleIds.Admin || u.RoleId == "ADMIN" || u.RoleId == "ROLE_ADMIN" || u.UserId.StartsWith("USR-ADMIN") || u.UserId.StartsWith("usr-admin"));
            }
            else if (group != DomainConstants.NotificationTargetGroup.All)
            {
                return ServiceResult<NotificationResponse>.Fail(400, $"Unknown TargetGroup: '{group}'. Valid options are: ALL, CUSTOMERS, STAFF, MANAGERS, ADMINS.", "INVALID_TARGET_GROUP");
            }
        }
        else if (request.UserIds != null && request.UserIds.Count > 0)
        {
            query = query.Where(u => request.UserIds.Contains(u.UserId));
        }
        else if (!string.IsNullOrWhiteSpace(request.UserId))
        {
            query = query.Where(u => u.UserId == request.UserId);
        }

        // Apply conditional filters
        if (request.IsFlagged == true)
        {
            query = query.Where(u => u.IsBlocked || u.SpamViolationCount > 0);
        }

        if (request.HasBooked == true)
        {
            var bookedUserIds = _dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.CustomerProfile != null)
                .Select(b => b.CustomerProfile.UserId);
            query = query.Where(u => bookedUserIds.Contains(u.UserId));
        }

        if (!string.IsNullOrWhiteSpace(request.RoomId))
        {
            var roomId = request.RoomId.Trim();
            var roomUserIds = _dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.CustomerProfile != null && b.Showtime.RoomId == roomId)
                .Select(b => b.CustomerProfile.UserId);
            query = query.Where(u => roomUserIds.Contains(u.UserId));
        }

        if (!string.IsNullOrWhiteSpace(request.ShowtimeId))
        {
            var showtimeId = request.ShowtimeId.Trim();
            var showtimeUserIds = _dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.CustomerProfile != null && b.ShowtimeId == showtimeId)
                .Select(b => b.CustomerProfile.UserId);
            query = query.Where(u => showtimeUserIds.Contains(u.UserId));
        }

        if (!string.IsNullOrWhiteSpace(request.MovieId))
        {
            var movieId = request.MovieId.Trim();
            var movieUserIds = _dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.CustomerProfile != null && b.Showtime.MovieId == movieId)
                .Select(b => b.CustomerProfile.UserId);
            query = query.Where(u => movieUserIds.Contains(u.UserId));
        }

        targetUsers = await query.ToListAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.TargetGroup))
        {
            var gUpper = request.TargetGroup.Trim().ToUpperInvariant();
            if (gUpper == DomainConstants.NotificationTargetGroup.Admins || gUpper == "ADMIN" || gUpper == "ADMINS")
            {
                if (sender != null && !targetUsers.Any(u => u.UserId == sender.UserId))
                {
                    targetUsers.Add(sender);
                }
            }
        }

        if (targetUsers.Count == 0)
        {
            return ServiceResult<NotificationResponse>.Fail(400, "No target users found for notification dispatch.", "NO_RECIPIENTS_FOUND");
        }

        // Additional Manager validation: Ensure target recipients are Staff or Admin
        if (sender != null && sender.RoleId == AuthConstants.RoleIds.Manager)
        {
            var invalidTargets = targetUsers.Where(u => u.RoleId != AuthConstants.RoleIds.Staff && u.RoleId != AuthConstants.RoleIds.Admin && u.RoleId != "ADMIN" && u.RoleId != "ROLE_ADMIN").ToList();
            if (invalidTargets.Count > 0)
            {
                return ServiceResult<NotificationResponse>.Fail(403, "Managers are authorized to send notifications to Staff or Admin.", "FORBIDDEN_RECIPIENT_ROLE");
            }
        }

        var status = "Sent";
        var firstNotifId = string.Empty;

        foreach (var user in targetUsers)
        {
            if (request.Channel.Equals(DomainConstants.NotificationChannel.Email, StringComparison.OrdinalIgnoreCase))
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
            else if (request.Channel.Equals(DomainConstants.NotificationChannel.SMS, StringComparison.OrdinalIgnoreCase) ||
                     request.Channel.Equals(DomainConstants.NotificationChannel.Push, StringComparison.OrdinalIgnoreCase))
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

    public async Task<ServiceResult<IReadOnlyList<NotificationResponse>>> GetInternalFeedAsync(
        string? userId = null,
        CancellationToken cancellationToken = default)
    {
        User? user = null;
        if (!string.IsNullOrWhiteSpace(userId))
        {
            user = await _dbContext.Users
                .AsNoTracking()
                .Include(u => u.StaffProfile)
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);
        }

        var query = _dbContext.Notifications
            .AsNoTracking()
            .Include(n => n.User)
            .ThenInclude(u => u.StaffProfile)
            .ThenInclude(sp => sp.Cinema)
            .AsQueryable();

        if (user != null && user.RoleId == AuthConstants.RoleIds.Staff)
        {
            query = query.Where(n => n.UserId == userId || n.User.RoleId == AuthConstants.RoleIds.Staff);
        }
        else if (user != null && user.RoleId == AuthConstants.RoleIds.Manager)
        {
            var managerCinemaId = user.StaffProfile?.CinemaId;
            if (!string.IsNullOrEmpty(managerCinemaId))
            {
                query = query.Where(n => (n.User.StaffProfile != null && n.User.StaffProfile.CinemaId == managerCinemaId) ||
                                         n.UserId == userId ||
                                         n.Message.Contains("Báo cáo từ Manager"));
            }
            else
            {
                query = query.Where(n => n.User.RoleId == AuthConstants.RoleIds.Staff ||
                                         n.UserId == userId ||
                                         n.Message.Contains("Báo cáo từ Manager"));
            }
        }

        var notifications = await query
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync(cancellationToken);

        List<NotificationResponse> result;
        if (user != null && user.RoleId == AuthConstants.RoleIds.Manager)
        {
            result = notifications.Select(MapToResponse).ToList();
        }
        else
        {
            var filtered = notifications
                .Where(n => n.Title.Contains("Lệnh") ||
                            n.Title.Contains("chuẩn bị") ||
                            n.Title.Contains("bảo trì") ||
                            n.Title.Contains("Cảnh báo") ||
                            n.Title.Contains("vận hành") ||
                            n.Title.Contains("khẩn cấp") ||
                            n.Title.Contains("Emergency") ||
                            n.Title.Contains("Internal") ||
                            n.Message.Contains("nội bộ") ||
                            n.Message.Contains("Nhân viên") ||
                            n.Message.Contains("Quản lý"))
                .ToList();

            var listToReturn = filtered.Count > 0 ? filtered : notifications.Take(20).ToList();
            result = listToReturn.Select(MapToResponse).ToList();
        }

        return ServiceResult<IReadOnlyList<NotificationResponse>>.Ok(result, "Internal operational feed retrieved successfully.");
    }

    public async Task<ServiceResult<IReadOnlyList<UserFilterItemResponse>>> GetFilteredUsersAsync(
        bool? isFlagged,
        bool? hasBooked,
        string? roomId,
        string? showtimeId,
        string? movieId,
        string? targetGroup,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Users.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(targetGroup))
        {
            var group = targetGroup.Trim().ToUpperInvariant();
            if (group == DomainConstants.NotificationTargetGroup.Customers)
            {
                query = query.Where(u => u.RoleId == AuthConstants.RoleIds.Customer);
            }
            else if (group == DomainConstants.NotificationTargetGroup.Staff)
            {
                query = query.Where(u => u.RoleId == AuthConstants.RoleIds.Staff);
            }
            else if (group == DomainConstants.NotificationTargetGroup.Managers)
            {
                query = query.Where(u => u.RoleId == AuthConstants.RoleIds.Manager);
            }
            else if (group == DomainConstants.NotificationTargetGroup.Admins)
            {
                query = query.Where(u => u.RoleId == AuthConstants.RoleIds.Admin);
            }
        }

        if (isFlagged == true)
        {
            query = query.Where(u => u.IsBlocked || u.SpamViolationCount > 0);
        }

        if (hasBooked == true)
        {
            var bookedUserIds = _dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.CustomerProfile != null)
                .Select(b => b.CustomerProfile.UserId);
            query = query.Where(u => bookedUserIds.Contains(u.UserId));
        }

        if (!string.IsNullOrWhiteSpace(roomId))
        {
            var rId = roomId.Trim();
            var roomUserIds = _dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.CustomerProfile != null && b.Showtime.RoomId == rId)
                .Select(b => b.CustomerProfile.UserId);
            query = query.Where(u => roomUserIds.Contains(u.UserId));
        }

        if (!string.IsNullOrWhiteSpace(showtimeId))
        {
            var stId = showtimeId.Trim();
            var showtimeUserIds = _dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.CustomerProfile != null && b.ShowtimeId == stId)
                .Select(b => b.CustomerProfile.UserId);
            query = query.Where(u => showtimeUserIds.Contains(u.UserId));
        }

        if (!string.IsNullOrWhiteSpace(movieId))
        {
            var mId = movieId.Trim();
            var movieUserIds = _dbContext.Bookings
                .AsNoTracking()
                .Where(b => b.CustomerProfile != null && b.Showtime.MovieId == mId)
                .Select(b => b.CustomerProfile.UserId);
            query = query.Where(u => movieUserIds.Contains(u.UserId));
        }

        var users = await query
            .Take(100)
            .Select(u => new UserFilterItemResponse
            {
                UserId = u.UserId,
                FullName = u.FullName ?? string.Empty,
                Email = u.Email ?? string.Empty,
                Role = u.RoleId
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<UserFilterItemResponse>>.Ok(users, $"Retrieved {users.Count} matching users.");
    }

    private NotificationResponse MapToResponse(Notification n)
    {
        var (channel, type, status) = DetermineMetadata(n.Title, n.Message);
        string? cinemaIdStr = n.User?.StaffProfile?.CinemaId;
        int? cinemaId = null;
        if (int.TryParse(cinemaIdStr, out var parsedId))
        {
            cinemaId = parsedId;
        }

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
            Status = status,
            CinemaId = cinemaId,
            CinemaName = n.User?.StaffProfile?.Cinema?.CinemaName ?? (cinemaIdStr != null ? $"Rạp #{cinemaIdStr}" : null)
        };
    }

    private static (string Channel, string Type, string Status) DetermineMetadata(string title, string message)
    {
        var channel = DomainConstants.NotificationChannel.App;
        var type = DomainConstants.NotificationType.Transactional;
        var status = "Sent";

        var combined = $"{title} {message}".ToLowerInvariant();

        if (combined.Contains("cảnh báo") || combined.Contains("lệnh") || combined.Contains("khẩn cấp") || combined.Contains("sự cố"))
        {
            type = DomainConstants.NotificationType.Internal;
            channel = combined.Contains("kỹ thuật") ? DomainConstants.NotificationChannel.SMS : DomainConstants.NotificationChannel.Internal;
        }
        else if (combined.Contains("voucher") || combined.Contains("sinh nhật") || combined.Contains("khảo sát") || combined.Contains("điểm thưởng"))
        {
            type = DomainConstants.NotificationType.Loyalty;
            channel = combined.Contains("sinh nhật") ? DomainConstants.NotificationChannel.Email : DomainConstants.NotificationChannel.App;
        }
        else if (combined.Contains("khuyến mãi") || combined.Contains("promo") || combined.Contains("bom tấn") || combined.Contains("ưu đãi"))
        {
            type = DomainConstants.NotificationType.Promotional;
            channel = DomainConstants.NotificationChannel.Push;
        }

        if (combined.Contains("hủy") || combined.Contains("hoàn tiền") || combined.Contains("refund") || combined.Contains("xác nhận"))
        {
            channel = DomainConstants.NotificationChannel.Email;
        }

        return (channel, type, status);
    }

    public async Task<ServiceResult<bool>> DeleteNotificationsAsync(
        List<string> notificationIds,
        CancellationToken cancellationToken)
    {
        if (notificationIds == null || notificationIds.Count == 0)
        {
            return ServiceResult<bool>.Ok(true, "No notifications specified for deletion.");
        }

        var notifs = await _dbContext.Notifications
            .Where(n => notificationIds.Contains(n.NotificationId))
            .ToListAsync(cancellationToken);

        if (notifs.Count > 0)
        {
            _dbContext.Notifications.RemoveRange(notifs);
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<bool>.Ok(true, $"Successfully deleted {notifs.Count} notification(s).");
    }

    public async Task<ServiceResult<bool>> UpdateNotificationAsync(
        string notificationId,
        string title,
        string message,
        CancellationToken cancellationToken)
    {
        var notif = await _dbContext.Notifications
            .FirstOrDefaultAsync(n => n.NotificationId == notificationId, cancellationToken);

        if (notif == null)
        {
            return ServiceResult<bool>.Fail(404, "Notification not found.", "NOT_FOUND");
        }

        notif.Title = title.Trim();
        notif.Message = message.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, "Notification updated successfully.");
    }
}
