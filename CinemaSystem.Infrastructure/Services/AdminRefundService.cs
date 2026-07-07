using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Application.Settings;
using Microsoft.Extensions.Options;
using Hangfire;

namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// Use case Admin hủy suất chiếu hàng loạt, tạo dữ liệu refund và xác nhận hoàn tiền.
/// </summary>
/// <remarks>
/// Nhận lệnh từ <c>CinemaSystem/Controllers/AdminRefundsController.cs</c> và
/// các luồng maintenance của Movie/Room/Showtime. Class xử lý SHOWTIME,
/// BOOKING, PAYMENT, REFUND, TICKET, seat lock và email nền; kết quả quay về
/// controller/service đã gọi.
/// </remarks>
public class AdminRefundService : IAdminRefundService
{
    private readonly CinemaDbContext _dbContext;
    private readonly ISeatLockStore _seatLockStore;
    private readonly CinemaProcessingSettings _settings;
    private readonly Hangfire.IBackgroundJobClient _backgroundJobClient;
    private readonly EmailTemplatesSettings _emailTemplates;
    private readonly IAiEmailService _aiEmailService;

    public AdminRefundService(
        CinemaDbContext dbContext, 
        ISeatLockStore seatLockStore, 
        IOptions<CinemaProcessingSettings> options, 
        Hangfire.IBackgroundJobClient backgroundJobClient, 
        IOptions<EmailTemplatesSettings> emailTemplatesOptions,
        IAiEmailService? aiEmailService = null)
    {
        _dbContext = dbContext;
        _seatLockStore = seatLockStore;
        _settings = options.Value;
        _backgroundJobClient = backgroundJobClient;
        _emailTemplates = emailTemplatesOptions.Value;
        _aiEmailService = aiEmailService!;
    }

    public async Task<ServiceResult<bool>> CancelShowtimesAndRefundAsync(string[] showtimeIds, string reason, bool forceCancel, string actionUserId, CancellationToken cancellationToken)
    {
        // Truy vấn danh sách các suất chiếu dựa trên mảng ID đầu vào
        var showtimes = await _dbContext.Showtimes
            // Bao gồm thông tin Phim để lấy tiêu đề gửi email
            .Include(s => s.Movie)
            // Bao gồm thông tin Đặt vé (Bookings) của suất chiếu đó
            .Include(s => s.Bookings)
                // Bao gồm thông tin Thanh toán (Payments) của từng Booking
                .ThenInclude(b => b.Payments)
            // Tiếp tục bao gồm thông tin Đặt vé để lấy Hồ sơ khách hàng
            .Include(s => s.Bookings)
                // Lấy thông tin CustomerProfile từ Booking
                .ThenInclude(b => b.CustomerProfile)
                    // Lấy User từ CustomerProfile (triệt tiêu cảnh báo null bằng dấu '!')
                    .ThenInclude(cp => cp!.User)
            // Bao gồm thông tin Ghế của suất chiếu để xử lý nhả ghế
            .Include(s => s.ShowtimeSeats)
            // Chỉ lọc những suất chiếu nằm trong danh sách ID và chưa bị HỦY trước đó
            .Where(s => showtimeIds.Contains(s.ShowtimeId) && s.Status != DomainConstants.EntityStatus.Cancelled)
            // Thực thi truy vấn và trả về dạng List bất đồng bộ
            .ToListAsync(cancellationToken);

        // Kiểm tra nếu không có suất chiếu nào hợp lệ để hủy
        if (!showtimes.Any())
        {
            // Trả về kết quả thành công kèm thông báo
            return ServiceResult<bool>.Ok(true, "No active showtimes to cancel.");
        }

        // Lấy thời gian hiện tại chuẩn UTC
        var now = DateTime.UtcNow;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Duyệt qua từng suất chiếu cần hủy
        foreach (var showtime in showtimes)
        {
            // Kiểm tra xem suất chiếu này đã có khách hàng đặt vé chưa
            if (showtime.Bookings.Any())
            {
                // Tính toán số phút còn lại cho đến khi suất chiếu bắt đầu
                var timeUntilShowtime = (showtime.StartTime - now).TotalMinutes;
                
                // Nếu không ép buộc hủy và thời gian còn lại nhỏ hơn thời gian khóa quy định (30 phút)
                if (!forceCancel && timeUntilShowtime < _settings.PreShowtimeBlockingMinutes)
                {
                    // Cập nhật trạng thái suất chiếu thành "Đang xử lý không ổn định"
                    showtime.Status = DomainConstants.EntityStatus.ProcessingUnstable;
                    // Lấy danh sách các đặt vé đã thanh toán hoặc đã hoàn tất
                    var paidBookings = showtime.Bookings.Where(b => b.BookingStatus == DomainConstants.EntityStatus.Completed || b.BookingStatus == DomainConstants.EntityStatus.Paid).ToList();
                    
                    // Duyệt qua từng vé đã thanh toán để xử lý gửi thông báo
                    foreach (var booking in paidBookings)
                    {
                        // Chuyển trạng thái vé thành "Đang xử lý không ổn định"
                        booking.BookingStatus = DomainConstants.EntityStatus.ProcessingUnstable;
                        // Lấy email khách hàng (ưu tiên tài khoản đăng nhập, nếu không có thì lấy email khách vãng lai)
                        var customerEmail = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
                        // Nếu có email hợp lệ
                        if (!string.IsNullOrEmpty(customerEmail))
                        {
                            // Đẩy một Job chạy nền vào Hangfire để gửi email thông báo sự cố khẩn cấp
                            _backgroundJobClient.Enqueue<IEmailService>(email => email.SendEmailAsync(customerEmail, "Unexpected Update Notification", $"Your showtime {showtime.Movie.Title} has been unexpectedly updated. Reason: {reason}. Please wait for the cinema to handle it.", CancellationToken.None));
                        }
                    }
                    // Bỏ qua các bước hủy tiếp theo cho suất chiếu này
                    continue; 
                }

                // Lấy danh sách các đặt vé ĐÃ THANH TOÁN thành công (loại bỏ dữ liệu rác không có Payment)
                var bookings = showtime.Bookings.Where(b => (b.BookingStatus == DomainConstants.EntityStatus.Completed || b.BookingStatus == DomainConstants.EntityStatus.Paid) && b.Payments.Any()).ToList();
                
                // Kiểm tra xem lịch chiếu này đã có bản ghi Hủy trong database chưa
                var cancellation = await _dbContext.ShowtimeCancellations.FirstOrDefaultAsync(c => c.ShowtimeId == showtime.ShowtimeId, cancellationToken);
                
                // Nếu chưa có bản ghi Hủy
                if (cancellation == null)
                {
                    // Khởi tạo một đối tượng Hủy lịch chiếu mới
                    cancellation = new ShowtimeCancellation
                    {
                        // Tạo ID ngẫu nhiên
                        ShowtimeCancellationId = "STC_" + Guid.NewGuid().ToString("N"),
                        // Gán ID suất chiếu bị hủy
                        ShowtimeId = showtime.ShowtimeId,
                        // Lưu lại lý do hủy
                        CancelReason = reason,
                        // Ghi nhận thời điểm hủy
                        CancelledAt = now,
                        // Ghi nhận ID của nhân viên/quản trị viên thực hiện hành động hủy
                        CancelledByUserId = actionUserId, 
                    };
                    // Đưa đối tượng Hủy vào tracking của EF Core DbContext
                    _dbContext.ShowtimeCancellations.Add(cancellation);
                }

                // Duyệt qua từng vé hợp lệ để thiết lập hoàn tiền
                foreach (var booking in bookings)
                {
                    // Đổi trạng thái vé thành "Chờ hoàn tiền" (REFUND_PENDING)
                    booking.BookingStatus = DomainConstants.EntityStatus.PendingRefund;
                    
                    // Kiểm tra xem vé này đã có yêu cầu hoàn tiền nào trước đó chưa
                    var existingRefund = await _dbContext.Refunds.FirstOrDefaultAsync(r => r.BookingId == booking.BookingId, cancellationToken);
                    
                    // Nếu chưa từng có yêu cầu hoàn tiền
                    if (existingRefund == null)
                    {
                        // Tìm bản ghi thanh toán hợp lệ nhất (ưu tiên trạng thái Success)
                        var payment = booking.Payments.FirstOrDefault(p => p.PaymentStatus == DomainConstants.PaymentStatus.Success) ?? booking.Payments.FirstOrDefault();
                        
                        if (payment == null)
                        {
                            payment = await _dbContext.Payments
                                .FirstOrDefaultAsync(p => p.BookingId == booking.BookingId && p.PaymentStatus == DomainConstants.PaymentStatus.Success, cancellationToken);
                            if (payment == null)
                            {
                                payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.BookingId, cancellationToken);
                            }
                        }
                        else
                        {
                            bool exists = await _dbContext.Payments.AnyAsync(p => p.PaymentId == payment.PaymentId, cancellationToken);
                            if (!exists) payment = null;
                        }

                        if (payment == null || string.IsNullOrEmpty(payment.PaymentId) || string.IsNullOrEmpty(payment.PaymentProviderId))
                        {
                            throw new Exception($"Cannot create refund for booking {booking.BookingId} because no valid payment record exists in the database.");
                        }

                        // Tạo mới một đối tượng Refund
                        var refund = new Refund
                        {
                            // Tạo ID hoàn tiền tự động
                            RefundId = "REF_" + Guid.NewGuid().ToString("N"),
                            // Map với ID của Booking
                            BookingId = booking.BookingId,
                            // Map chính xác ID giao dịch thanh toán (Khắc phục lỗi FK_REFUND_PAYMENT)
                            PaymentId = payment.PaymentId,
                            // Map với nhà cung cấp dịch vụ thanh toán
                            PaymentProviderId = payment.PaymentProviderId,
                            // Số tiền hoàn trả bằng tổng số tiền đã thanh toán
                            RefundAmount = booking.TotalAmount,
                            // Trạng thái hoàn tiền khởi điểm là PENDING
                            RefundStatus = DomainConstants.RefundStatus.Pending,
                            // Lý do hoàn tiền lấy từ input
                            RefundReason = reason,
                            // Liên kết khóa ngoại với hành động hủy lịch chiếu
                            ShowtimeCancellationId = cancellation.ShowtimeCancellationId,
                            // Thời điểm tạo yêu cầu hoàn tiền
                            RequestedAt = now
                        };
                        // Đưa đối tượng Refund vào Entity Framework
                        _dbContext.Refunds.Add(refund);
                    }
                    else
                    {
                        // Nếu đã tồn tại dòng hoàn tiền, tiến hành reset lại trạng thái thành PENDING
                        existingRefund.RefundStatus = DomainConstants.RefundStatus.Pending;
                        // Ghi đè lý do hoàn tiền mới nhất
                        existingRefund.RefundReason = reason;
                    }

                    // Send cancellation email using AI service
                    var customerEmail = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
                    if (!string.IsNullOrEmpty(customerEmail))
                    {
                        string subject = _emailTemplates.ShowtimeCancellationSubject;
                        string details = $"Suất chiếu phim {showtime.Movie?.Title ?? "bạn đã đặt"} vào lúc {showtime.StartTime:dd/MM/yyyy HH:mm} bị hủy bỏ do sự cố/lỗi kỹ thuật của rạp chiếu phim.";
                        _backgroundJobClient.Enqueue<IAiEmailService>(ai => 
                            ai.SendAiApologyEmailAsync(customerEmail, subject, reason, details, CancellationToken.None));
                    }
                }
            }

            // Nếu suất chiếu có thể hủy một cách an toàn (không vướng rule khóa 30 phút)
            if (showtime.Status != DomainConstants.EntityStatus.ProcessingUnstable)
            {
                // Cập nhật trạng thái suất chiếu thành HỦY
                showtime.Status = DomainConstants.EntityStatus.Cancelled;

                // Xử lý nhả toàn bộ các ghế đang bị khóa/đặt của suất chiếu này
                foreach (var seat in showtime.ShowtimeSeats)
                {
                    // Bỏ qua các ghế đã bán (Booked), chỉ nhả các ghế khác
                    if (seat.SeatStatus != DomainConstants.EntityStatus.Booked)
                    {
                        // Đặt trạng thái ghế về trống (Available)
                        seat.SeatStatus = DomainConstants.EntityStatus.Available;
                    }
                    // Reset thời điểm khóa tạm thời
                    seat.LockedUntil = null;
                    // Reset ID người đang giữ ghế
                    seat.LockedByUserId = null;
                    // Xây dựng Key định danh ghế trong hệ thống Cache (Redis/Memory)
                    var lockKey = $"seat-lock:{showtime.ShowtimeId}:{seat.SeatId}";
                    // Xóa Key khóa ghế trong Cache
                    await _seatLockStore.ReleaseAsync(lockKey, cancellationToken);
                }
            }
        }

            // Lưu toàn bộ thay đổi ở cấp Database (Transaction Commit)
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            
            // Trả về thành công
            return ServiceResult<bool>.Ok(true, "Showtimes cancelled and refunds prepared successfully.");
        }
        catch (Exception)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<ServiceResult<PagedList<RefundDto>>> GetRefundsAsync(string status, int pageIndex, int pageSize, CancellationToken cancellationToken)
    {
        // Khởi tạo truy vấn bảng Refunds kèm theo các bảng dữ liệu liên quan (Join)
        var query = _dbContext.Refunds
            // Kết nối sang bảng Booking
            .Include(r => r.Booking)
                // Từ Booking lấy thông tin Hồ sơ Khách hàng
                .ThenInclude(b => b.CustomerProfile)
                    // Lấy chi tiết thông tin User từ CustomerProfile
                    .ThenInclude(cp => cp!.User)
            // Kết nối sang bảng Booking (nhánh 2)
            .Include(r => r.Booking)
                // Từ Booking lấy thông tin Suất chiếu
                .ThenInclude(b => b.Showtime)
                    // Lấy chi tiết Bộ phim của suất chiếu đó
                    .ThenInclude(s => s.Movie)
            // Kết nối sang bảng Booking (nhánh 3)
            .Include(r => r.Booking)
                // Từ Booking lấy thông tin Suất chiếu
                .ThenInclude(b => b.Showtime)
                    // Lấy chi tiết Phòng chiếu
                    .ThenInclude(s => s.Room)
            // Biến đổi truy vấn thành kiểu IQueryable để có thể cộng gộp các biểu thức LINQ (Where, OrderBy,...)
            .AsQueryable();

        // Kiểm tra nếu tham số trạng thái được truyền vào hợp lệ
        if (!string.IsNullOrWhiteSpace(status))
        {
            // Chuẩn hóa trạng thái về dạng chữ in hoa không khoảng trắng thừa
            var normalizedStatus = status.Trim().ToUpperInvariant();
            // Bổ sung câu lệnh WHERE vào truy vấn để lọc theo trạng thái mong muốn
            query = query.Where(r => r.RefundStatus == normalizedStatus);
        }

        // Thực thi đếm tổng số lượng bản ghi thỏa mãn điều kiện (Dùng cho phân trang)
        var totalCount = await query.CountAsync(cancellationToken);

        // Thực thi truy vấn lấy dữ liệu
        var refunds = await query
            // Sắp xếp các yêu cầu hoàn tiền mới nhất lên đầu (Giảm dần theo thời gian tạo)
            .OrderByDescending(r => r.RequestedAt)
            // Phân trang: Bỏ qua các bản ghi ở các trang trước
            .Skip((pageIndex - 1) * pageSize)
            // Phân trang: Chỉ lấy đủ số lượng bản ghi của 1 trang
            .Take(pageSize)
            // Ánh xạ dữ liệu từ Entity (SQL) sang DTO (Data Transfer Object) để trả về API
            .Select(r => new RefundDto
            {
                // Gán ID của đơn đặt vé
                BookingId = r.BookingId,
                // Gán ID suất chiếu
                ShowtimeId = r.Booking.ShowtimeId,
                // Gán số tiền cần hoàn
                TotalAmount = r.RefundAmount,
                // Gán lý do hoàn tiền (hoặc chuỗi rỗng nếu null)
                RefundReason = r.RefundReason ?? string.Empty,
                // Gán trạng thái hiện tại của đơn đặt vé
                BookingStatus = r.Booking.BookingStatus,
                // Gán trạng thái hiện tại của việc hoàn tiền
                RefundStatus = r.RefundStatus,
                // Lấy tên khách hàng (Ưu tiên tên tài khoản đăng nhập, sau đó đến tên khách vãng lai)
                CustomerName = r.Booking.CustomerProfile != null ? r.Booking.CustomerProfile.User.FullName : (r.Booking.GuestName ?? "Guest"),
                // Lấy email khách hàng
                CustomerEmail = r.Booking.CustomerProfile != null ? r.Booking.CustomerProfile.User.Email : r.Booking.GuestEmail,
                // Lấy số điện thoại khách hàng
                CustomerPhone = r.Booking.CustomerProfile != null ? r.Booking.CustomerProfile.User.PhoneNumber : r.Booking.GuestPhone,
                // Lấy tên bộ phim
                MovieName = r.Booking.Showtime.Movie.Title,
                // Lấy tên phòng chiếu
                RoomName = r.Booking.Showtime.Room.RoomName,
                // Lấy thời gian bắt đầu chiếu
                StartTime = r.Booking.Showtime.StartTime,
                // Gán ID giao dịch hoàn tiền
                RefundId = r.RefundId,
                // Lấy thời điểm gửi yêu cầu hoàn tiền
                RequestedAt = r.RequestedAt
            })
            // Chạy bất đồng bộ và chuyển đổi thành danh sách (List)
            .ToListAsync(cancellationToken);

        // Khởi tạo đối tượng Phân trang bao bọc danh sách DTO
        var pagedList = new PagedList<RefundDto>(refunds, totalCount, pageIndex, pageSize);

        // Trả về kết quả cho Controller
        return ServiceResult<PagedList<RefundDto>>.Ok(pagedList, "Refunds retrieved successfully.");
    }

    public async Task<ServiceResult<bool>> ConfirmRefundAsync(string bookingId, string adminUserId, CancellationToken cancellationToken)
    {
        // Truy vấn thông tin Booking từ cơ sở dữ liệu dựa trên ID, kèm theo dữ liệu hoàn tiền (Refunds)
        var booking = await _dbContext.Bookings
            // Bao gồm danh sách Refunds của Booking này
            .Include(b => b.Refunds)
            // Thực thi truy vấn lấy bản ghi đầu tiên khớp ID
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

        // Kiểm tra nếu không tìm thấy Booking
        if (booking == null) return ServiceResult<bool>.Fail(404, "Booking not found.", "NOT_FOUND");
        
        // Kiểm tra nếu trạng thái của Booking không phải là Đang chờ hoàn tiền
        if (booking.BookingStatus != DomainConstants.EntityStatus.PendingRefund) return ServiceResult<bool>.Fail(400, "Booking is not pending refund.", "INVALID_STATUS");

        // Đổi trạng thái Booking sang Đã hoàn tiền thành công (REFUNDED)
        booking.BookingStatus = DomainConstants.EntityStatus.Refunded;

        // Tìm kiếm bản ghi hoàn tiền cụ thể đang ở trạng thái PENDING
        var refund = booking.Refunds.FirstOrDefault(r => r.RefundStatus == DomainConstants.RefundStatus.Pending);
        
        // Nếu tìm thấy bản ghi hoàn tiền đó
        if (refund != null)
        {
            // Chuyển trạng thái của bản ghi Refund thành SUCCESS
            refund.RefundStatus = DomainConstants.RefundStatus.Success;
            // Cập nhật lại thời gian đã hoàn tiền là thời điểm hiện tại
            refund.RefundedAt = DateTime.UtcNow;
        }

        // Lưu thay đổi vào Cơ sở dữ liệu (Commit Transaction)
        await _dbContext.SaveChangesAsync(cancellationToken);
        
        // Trả về kết quả hoàn tất tác vụ cho Controller
        return ServiceResult<bool>.Ok(true, "Refund confirmed successfully.");
    }
}
