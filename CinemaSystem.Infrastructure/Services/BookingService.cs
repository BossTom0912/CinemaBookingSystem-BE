using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using CinemaSystem.Domain.Constants;

namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// Runtime implementation for the original booking create/detail/history
/// routes reached from <c>BookingsController</c>.
/// </summary>
/// <remarks>
/// Reads customer/showtime/seat/F&amp;B data, creates a PENDING_PAYMENT booking
/// and records temporary seat state through <c>CinemaDbContext</c>. The richer
/// transactional checkout path is implemented separately by
/// <c>CinemaSystem.Infrastructure.Bookings.CheckoutService</c>; payment is the
/// next use case handled by <c>PaymentService</c>.
/// </remarks>
public sealed class BookingService : IBookingService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ISeatLockStore _seatLockStore;
    private readonly CinemaSystem.Application.Settings.SecuritySettings _securitySettings;

    public BookingService(
        CinemaDbContext dbContext,
        IClock clock,
        Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.SecuritySettings> securityOptions,
        ISeatLockStore seatLockStore)
    {
        _dbContext = dbContext;
        _clock = clock;
        _seatLockStore = seatLockStore;
        _securitySettings = securityOptions.Value;
    }

    public async Task<ServiceResult<BookingResponse>> CreateBookingAsync(
        CreateBookingRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        var customerProfile = await _dbContext.CustomerProfiles
            .FirstOrDefaultAsync(cp => cp.UserId == userId, cancellationToken);

        if (customerProfile == null)
        {
            return ServiceResult<BookingResponse>.Fail(403, "Only customers can book tickets.", "CUSTOMER_PROFILE_NOT_FOUND");
        }

        var showtime = await _dbContext.Showtimes
            .Include(s => s.Movie)
            .Include(s => s.Room)
                .ThenInclude(r => r.Cinema)
            .FirstOrDefaultAsync(s => s.ShowtimeId == request.ShowtimeId, cancellationToken);

        if (showtime == null)
        {
            return ServiceResult<BookingResponse>.Fail(404, "Showtime not found.", "SHOWTIME_NOT_FOUND");
        }

        if (showtime.Status == DomainConstants.EntityStatus.Cancelled || showtime.Status == DomainConstants.EntityStatus.Closed)
        {
            return ServiceResult<BookingResponse>.Fail(400, "This showtime is no longer accepting bookings.", "SHOWTIME_UNAVAILABLE");
        }

        await ReleaseStaleBookingSeatsForShowtimeAsync(
            request.ShowtimeId,
            request.ShowtimeSeatIds,
            _clock.UtcNow,
            cancellationToken);

        var showtimeSeats = await _dbContext.ShowtimeSeats
            .Include(ss => ss.BookingSeat)
                .ThenInclude(bs => bs.Booking)
            .Include(ss => ss.Seat)
            .ThenInclude(s => s.SeatType)
            .Where(ss => request.ShowtimeSeatIds.Contains(ss.ShowtimeSeatId) && ss.ShowtimeId == request.ShowtimeId)
            .ToListAsync(cancellationToken);

        if (showtimeSeats.Count != request.ShowtimeSeatIds.Count)
        {
            return ServiceResult<BookingResponse>.Fail(400, "One or more selected seats are invalid.", "INVALID_SEATS");
        }

        var now = _clock.UtcNow;

        if (now >= showtime.StartTime)
        {
            return ServiceResult<BookingResponse>.Fail(400, "Cannot book tickets for a showtime that has already started.", "SHOWTIME_STARTED");
        }

        foreach (var ss in showtimeSeats)
        {
            if (IsSoldBookingSeat(ss.BookingSeat) || ss.SeatStatus == DomainConstants.EntityStatus.Booked)
            {
                return ServiceResult<BookingResponse>.Fail(409, $"Seat {ss.Seat.SeatCode} is already booked.", "SEAT_ALREADY_BOOKED");
            }

            if (IsActivePendingBookingSeat(ss.BookingSeat, now))
            {
                return ServiceResult<BookingResponse>.Fail(409, $"Seat {ss.Seat.SeatCode} is waiting for payment.", "SEAT_PENDING_PAYMENT");
            }

            if (ss.SeatStatus == DomainConstants.EntityStatus.Locked && ss.LockedByUserId != userId && ss.LockedUntil > now)
            {
                return ServiceResult<BookingResponse>.Fail(409, $"Seat {ss.Seat.SeatCode} is locked by another user.", "SEAT_LOCKED");
            }
        }

        // Calculate total amount
        decimal totalAmount = 0;
        var bookingSeats = new List<BookingSeat>();
        foreach (var ss in showtimeSeats)
        {
            var seatPrice = showtime.BasePrice + ss.Seat.SeatType.ExtraFee;
            totalAmount += seatPrice;
            bookingSeats.Add(new BookingSeat
            {
                BookingSeatId = NewId("BKS"),
                ShowtimeSeatId = ss.ShowtimeSeatId,
                SeatPrice = seatPrice
            });
        }

        // F&B
        var bookingFbItems = new List<BookingFbItem>();
        if (request.FoodAndBeverages != null && request.FoodAndBeverages.Any())
        {
            var fbItemIds = request.FoodAndBeverages.Select(f => f.FbItemId).ToList();
            var fbItems = await _dbContext.FbItems
                .Where(f => fbItemIds.Contains(f.FbItemId))
                .ToListAsync(cancellationToken);

            foreach (var itemRequest in request.FoodAndBeverages)
            {
                var fbItem = fbItems.FirstOrDefault(f => f.FbItemId == itemRequest.FbItemId);
                if (fbItem == null) continue;

                var subtotal = fbItem.Price * itemRequest.Quantity;
                totalAmount += subtotal;
                bookingFbItems.Add(new BookingFbItem
                {
                    BookingFbitemId = NewId("BFI"),
                    FbItemId = fbItem.FbItemId,
                    Quantity = itemRequest.Quantity,
                    UnitPrice = fbItem.Price,
                    Subtotal = subtotal
                });
            }
        }

        var bookingId = NewId("BOK");
        var booking = new Booking
        {
            BookingId = bookingId,
            CustomerProfileId = customerProfile.CustomerProfileId,
            ShowtimeId = showtime.ShowtimeId,
            BookingStatus = DomainConstants.EntityStatus.PendingPayment,
            TotalAmount = totalAmount,
            CreatedAt = now,
            ExpiredAt = now.AddMinutes(10),
            BookingChannel = "ONLINE",
            BookingSeats = bookingSeats,
            BookingFbItems = bookingFbItems
        };

        // Update showtime seats to LOCKED
        foreach (var ss in showtimeSeats)
        {
            ss.SeatStatus = DomainConstants.EntityStatus.Locked;
            ss.LockedUntil = booking.ExpiredAt;
            ss.LockedByUserId = userId;
        }

        _dbContext.Bookings.Add(booking);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<BookingResponse>.Ok(new BookingResponse
        {
            BookingId = booking.BookingId,
            ShowtimeId = booking.ShowtimeId,
            MovieTitle = showtime.Movie.Title,
            CinemaName = showtime.Room.Cinema.CinemaName,
            RoomName = showtime.Room.RoomName,
            StartTime = showtime.StartTime,
            TotalAmount = booking.TotalAmount,
            Status = booking.BookingStatus,
            CreatedAt = booking.CreatedAt,
            ExpiredAt = booking.ExpiredAt
        }, "Booking created successfully.");
    }

    public async Task<ServiceResult<BookingDetailsResponse>> GetBookingDetailsAsync(
        string bookingId,
        string userId,
        CancellationToken cancellationToken)
    {
        var booking = await _dbContext.Bookings
            .Include(b => b.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(b => b.Showtime)
                .ThenInclude(s => s.Room)
                    .ThenInclude(r => r.Cinema)
            .Include(b => b.BookingSeats)
                .ThenInclude(bs => bs.ShowtimeSeat)
                    .ThenInclude(ss => ss.Seat)
                        .ThenInclude(s => s.SeatType)
            .Include(b => b.BookingSeats)
                .ThenInclude(bs => bs.Ticket)
            .Include(b => b.BookingFbItems)
                .ThenInclude(bfi => bfi.FbItem)
            .Include(b => b.CustomerProfile)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

        if (booking == null)
        {
            return ServiceResult<BookingDetailsResponse>.Fail(404, "Booking not found.", "BOOKING_NOT_FOUND");
        }

        if (booking.CustomerProfile?.UserId != userId)
        {
            return ServiceResult<BookingDetailsResponse>.Fail(403, "You do not have permission to view this booking.", "FORBIDDEN");
        }

        return ServiceResult<BookingDetailsResponse>.Ok(new BookingDetailsResponse
        {
            BookingId = booking.BookingId,
            ShowtimeId = booking.ShowtimeId,
            MovieTitle = booking.Showtime.Movie.Title,
            CinemaName = booking.Showtime.Room.Cinema.CinemaName,
            RoomName = booking.Showtime.Room.RoomName,
            StartTime = booking.Showtime.StartTime,
            TotalAmount = booking.TotalAmount,
            Status = booking.BookingStatus,
            CreatedAt = booking.CreatedAt,
            Seats = booking.BookingSeats.Select(bs => new BookedSeatDetailsResponse
            {
                SeatId = bs.ShowtimeSeat.SeatId,
                SeatNumber = bs.ShowtimeSeat.Seat.SeatNumber.ToString(),
                RowLabel = bs.ShowtimeSeat.Seat.RowLabel,
                SeatType = bs.ShowtimeSeat.Seat.SeatType.TypeName,
                Price = bs.SeatPrice,
                TicketId = bs.Ticket?.TicketId,
                TicketQrCode = bs.Ticket?.QrCode,
                TicketStatus = bs.Ticket?.TicketStatus
            }).ToList(),
            FoodAndBeverages = booking.BookingFbItems.Select(bfi => new BookedFbItemResponse
            {
                ItemName = bfi.FbItem.ItemName,
                Quantity = bfi.Quantity,
                Subtotal = bfi.Subtotal
            }).ToList()
        }, "Booking details retrieved successfully.");
    }

    public async Task<ServiceResult<IReadOnlyList<BookingResponse>>> GetMyBookingsAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var bookings = await _dbContext.Bookings
            .Include(b => b.CustomerProfile)
            .Where(b => b.CustomerProfile != null && b.CustomerProfile.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .Select(b => new BookingResponse
            {
                BookingId = b.BookingId,
                ShowtimeId = b.ShowtimeId,
                MovieTitle = b.Showtime.Movie.Title,
                CinemaName = b.Showtime.Room.Cinema.CinemaName,
                RoomName = b.Showtime.Room.RoomName,
                StartTime = b.Showtime.StartTime,
                TotalAmount = b.TotalAmount,
                Status = b.BookingStatus,
                CreatedAt = b.CreatedAt,
                ExpiredAt = b.ExpiredAt
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<BookingResponse>>.Ok(bookings, "My bookings retrieved successfully.");
    }

    public async Task<ServiceResult<bool>> CancelPendingBookingAsync(
        string bookingId,
        string userId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(bookingId))
        {
            return ServiceResult<bool>.Fail(400, "Booking ID is required.", "BOOKING_ID_REQUIRED");
        }

    public async Task<ServiceResult<bool>> CancelBookingAsync(
        string bookingId,
        string userId,
        CancellationToken cancellationToken)
    {
        // Truy vấn thông tin đơn đặt vé cần hủy
        var booking = await _dbContext.Bookings
            // Bao gồm thông tin hồ sơ khách hàng
            .Include(b => b.CustomerProfile)
            // Bao gồm danh sách ghế đã đặt
            .Include(b => b.BookingSeats)
                // Từ thông tin ghế đặt lấy thông tin ghế của suất chiếu để xử lý việc mở khóa
                .ThenInclude(bs => bs.ShowtimeSeat)
            // Lấy đơn đặt vé khớp với ID được yêu cầu
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

        // Kiểm tra nếu không tìm thấy đơn đặt vé
        if (booking == null)
        {
            // Trả về lỗi 404 (Không tìm thấy)
            return ServiceResult<bool>.Fail(404, "Booking not found.", "BOOKING_NOT_FOUND");
        }

        // Kiểm tra xem đơn đặt vé này có thuộc về người dùng đang thực hiện yêu cầu không
        if (booking.CustomerProfile?.UserId != userId)
        {
            // Trả về lỗi 403 (Cấm truy cập) nếu người dùng không phải là chủ sở hữu đơn đặt vé
            return ServiceResult<bool>.Fail(403, "You do not have permission to cancel this booking.", "FORBIDDEN");
        }

        // Kiểm tra trạng thái hiện tại của đơn đặt vé. Chỉ cho phép hủy khi đang chờ thanh toán
        if (booking.BookingStatus != DomainConstants.EntityStatus.PendingPayment)
        {
            // Trả về lỗi 400 (Yêu cầu không hợp lệ) vì đơn đã được xử lý (thanh toán hoặc đã hủy)
            return ServiceResult<bool>.Fail(400, "Only bookings in pending payment status can be cancelled.", "INVALID_STATUS");
        }

        // Cập nhật trạng thái đơn đặt vé thành Đã Hủy (CANCELLED)
        booking.BookingStatus = DomainConstants.EntityStatus.Cancelled;

        // Duyệt qua từng ghế đã chọn trong đơn đặt vé để thực hiện mở khóa
        foreach (var bs in booking.BookingSeats)
        {
            // Kiểm tra ghế có tồn tại, đang ở trạng thái Khóa, và do chính người dùng này khóa
            if (bs.ShowtimeSeat != null && bs.ShowtimeSeat.SeatStatus == DomainConstants.EntityStatus.Locked && bs.ShowtimeSeat.LockedByUserId == userId)
            {
                // Đổi trạng thái ghế trở lại thành Có sẵn (AVAILABLE)
                bs.ShowtimeSeat.SeatStatus = DomainConstants.EntityStatus.Available;
                // Xóa thời gian khóa ghế
                bs.ShowtimeSeat.LockedUntil = null;
                // Xóa ID người dùng đang khóa ghế
                bs.ShowtimeSeat.LockedByUserId = null;
            }
        }

        // Lưu các thay đổi (cập nhật trạng thái đơn vé và mở khóa ghế) vào cơ sở dữ liệu
        await _dbContext.SaveChangesAsync(cancellationToken);

        // Trả về kết quả thành công
        return ServiceResult<bool>.Ok(true, "Booking cancelled successfully.");
    }

    public async Task<ServiceResult<bool>> ConfirmTimeChangeAsync(
        string bookingId,
        bool accept,
        string token,
        CancellationToken cancellationToken)
    {
        // 1. Verify token
        var secret = _securitySettings.ConfirmationTokenSecret;
        using var hmac = new System.Security.Cryptography.HMACSHA256(System.Text.Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(bookingId));
        var expectedToken = Convert.ToBase64String(hash);

        // Make it URL safe if needed, but standard Base64 should match if unescaped
        if (token.Replace(" ", "+") != expectedToken)
        {
            return ServiceResult<bool>.Fail(400, "Invalid or expired token.", "INVALID_TOKEN");
        }

        var booking = await _dbContext.Bookings
            .Include(b => b.Payments)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

        if (booking == null) return ServiceResult<bool>.Fail(404, "Booking not found.", "NOT_FOUND");
        if (booking.BookingStatus != DomainConstants.EntityStatus.ProcessingUnstable)
            return ServiceResult<bool>.Fail(400, "Booking is not pending a time change confirmation.", "INVALID_STATUS");

        if (accept)
        {
            booking.BookingStatus = DomainConstants.EntityStatus.Paid;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<bool>.Ok(true, "Time change accepted successfully.");
        }
        else
        {
            // Đổi trạng thái đơn vé thành Đang chờ hoàn tiền (PENDING_REFUND)
            booking.BookingStatus = DomainConstants.EntityStatus.PendingRefund;
            
            // Tìm giao dịch thanh toán thành công của đơn hàng này
            var payment = booking.Payments.FirstOrDefault(p => p.PaymentStatus == DomainConstants.PaymentStatus.Success) ?? booking.Payments.FirstOrDefault();
            
            // Nếu không tìm thấy thông tin thanh toán trong danh sách đã tải
            if (payment == null)
            {
                // Truy vấn trực tiếp từ database để lấy giao dịch thanh toán thành công
                payment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => p.BookingId == booking.BookingId && p.PaymentStatus == DomainConstants.PaymentStatus.Success, cancellationToken);
                    
                // Nếu vẫn không có giao dịch thành công, lấy bất kỳ giao dịch nào của đơn này
                if (payment == null)
                {
                    payment = await _dbContext.Payments.FirstOrDefaultAsync(p => p.BookingId == booking.BookingId, cancellationToken);
                }
            }
            else
            {
                // Kiểm tra xem bản ghi thanh toán này có thực sự tồn tại trong CSDL không
                bool exists = await _dbContext.Payments.AnyAsync(p => p.PaymentId == payment.PaymentId, cancellationToken);
                // Nếu không tồn tại thì gán thành null
                if (!exists) payment = null;
            }

            if (payment == null || string.IsNullOrEmpty(payment.PaymentId) || string.IsNullOrEmpty(payment.PaymentProviderId))
            {
                return ServiceResult<bool>.Fail(400, "Cannot reject time change because no valid payment record exists to process the refund.", "INVALID_PAYMENT");
            }

            var refund = new Refund
            {
                RefundId = NewId("REF"),
                BookingId = booking.BookingId,
                PaymentId = payment.PaymentId,
                PaymentProviderId = payment.PaymentProviderId,
                RefundAmount = booking.TotalAmount,
                RefundStatus = DomainConstants.RefundStatus.Pending,
                RefundReason = "User rejected time change",
                RequestedAt = _clock.UtcNow
            };
            _dbContext.Refunds.Add(refund);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<bool>.Ok(true, "Time change rejected. Refund initiated.");
        }
    }

    private async Task ReleaseStaleBookingSeatsForShowtimeAsync(
        string showtimeId,
        IEnumerable<string> showtimeSeatIds,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var selectedShowtimeSeatIds = showtimeSeatIds.Distinct().ToList();
        if (selectedShowtimeSeatIds.Count == 0)
        {
            return;
        }

        var staleBookingIds = await _dbContext.BookingSeats
            .Where(item =>
                selectedShowtimeSeatIds.Contains(item.ShowtimeSeatId)
                && item.ShowtimeSeat.ShowtimeId == showtimeId
                && (item.Booking.BookingStatus == DomainConstants.EntityStatus.Cancelled
                    || (item.Booking.BookingStatus == DomainConstants.EntityStatus.PendingPayment
                        && item.Booking.ExpiredAt.HasValue
                        && item.Booking.ExpiredAt <= now)))
            .Select(item => item.BookingId)
            .Distinct()
            .ToListAsync(cancellationToken);

        if (staleBookingIds.Count == 0)
        {
            return;
        }

        var staleBookings = await _dbContext.Bookings
            .Include(item => item.BookingSeats)
                .ThenInclude(item => item.ShowtimeSeat)
            .Include(item => item.Payments)
            .Where(item => staleBookingIds.Contains(item.BookingId))
            .ToListAsync(cancellationToken);

        foreach (var booking in staleBookings)
        {
            foreach (var payment in booking.Payments)
            {
                if (string.Equals(payment.PaymentStatus, DomainConstants.PaymentStatus.Pending, StringComparison.OrdinalIgnoreCase))
                {
                    payment.PaymentStatus = DomainConstants.PaymentStatus.Expired;
                    payment.UpdatedAt = now;
                    payment.FailureReason ??= "Pending booking expired before a new checkout attempt.";
                }
            }

            booking.BookingStatus = DomainConstants.EntityStatus.Cancelled;
            booking.ExpiredAt ??= now;
            await ReleaseBookingSeatsAsync(booking.BookingSeats.ToList(), cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task ReleaseBookingSeatsAsync(
        IReadOnlyCollection<BookingSeat> bookingSeats,
        CancellationToken cancellationToken)
    {
        if (bookingSeats.Count == 0)
        {
            return;
        }

        foreach (var bookingSeat in bookingSeats)
        {
            var showtimeSeat = bookingSeat.ShowtimeSeat;
            if (showtimeSeat == null)
            {
                continue;
            }

            showtimeSeat.SeatStatus = DomainConstants.EntityStatus.Available;
            showtimeSeat.LockedUntil = null;
            showtimeSeat.LockedByUserId = null;

            await _seatLockStore.ReleaseAsync(
                BuildSeatLockKey(showtimeSeat.ShowtimeId, showtimeSeat.SeatId),
                cancellationToken);
        }

        _dbContext.BookingSeats.RemoveRange(bookingSeats);
    }

    private static bool IsActivePendingBookingSeat(BookingSeat? bookingSeat, DateTime now)
    {
        var booking = bookingSeat?.Booking;
        return booking != null
            && string.Equals(booking.BookingStatus, DomainConstants.EntityStatus.PendingPayment, StringComparison.OrdinalIgnoreCase)
            && (!booking.ExpiredAt.HasValue || booking.ExpiredAt > now);
    }

    private static bool IsSoldBookingSeat(BookingSeat? bookingSeat)
    {
        if (bookingSeat == null)
        {
            return false;
        }

        var status = bookingSeat.Booking?.BookingStatus;
        return status == null
            || string.Equals(status, DomainConstants.EntityStatus.Paid, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DomainConstants.EntityStatus.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DomainConstants.EntityStatus.ProcessingUnstable, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DomainConstants.EntityStatus.PendingRefund, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFinalPaidStatus(string? status)
    {
        return string.Equals(status, DomainConstants.EntityStatus.Paid, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DomainConstants.EntityStatus.Completed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DomainConstants.EntityStatus.ProcessingUnstable, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DomainConstants.EntityStatus.PendingRefund, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSeatLockKey(string showtimeId, string seatId)
    {
        return $"seat-lock:{showtimeId}:{seatId}";
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}