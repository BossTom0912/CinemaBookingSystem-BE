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
using Hangfire;

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
    private readonly Hangfire.IBackgroundJobClient _backgroundJobClient;
    private readonly IAiEmailService _aiEmailService;

    public BookingService(
        CinemaDbContext dbContext,
        IClock clock,
        Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.SecuritySettings> securityOptions,
        ISeatLockStore seatLockStore,
        IAiEmailService aiEmailService,
        Hangfire.IBackgroundJobClient? backgroundJobClient = null)
    {
        _dbContext = dbContext;
        _clock = clock;
        _seatLockStore = seatLockStore;
        _securitySettings = securityOptions.Value;
        _aiEmailService = aiEmailService;
        _backgroundJobClient = backgroundJobClient!;
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

        var booking = await _dbContext.Bookings
            .Include(item => item.CustomerProfile)
            .Include(item => item.BookingSeats)
                .ThenInclude(item => item.ShowtimeSeat)
            .Include(item => item.Payments)
            .FirstOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);

        if (booking == null)
        {
            return ServiceResult<bool>.Fail(404, "Booking not found.", "BOOKING_NOT_FOUND");
        }

        if (booking.CustomerProfile?.UserId != userId)
        {
            return ServiceResult<bool>.Fail(403, "You do not have permission to cancel this booking.", "FORBIDDEN");
        }

        if (IsFinalPaidStatus(booking.BookingStatus))
        {
            return ServiceResult<bool>.Fail(409, "Paid booking cannot be cancelled from checkout.", "BOOKING_ALREADY_PAID");
        }

        if (!string.Equals(booking.BookingStatus, DomainConstants.EntityStatus.PendingPayment, StringComparison.OrdinalIgnoreCase)
            && !string.Equals(booking.BookingStatus, DomainConstants.EntityStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<bool>.Fail(400, "Only pending payment bookings can be cancelled.", "INVALID_BOOKING_STATUS");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var now = _clock.UtcNow;
        await ReleaseBookingSeatsAsync(booking.BookingSeats.ToList(), cancellationToken);

        foreach (var payment in booking.Payments)
        {
            if (string.Equals(payment.PaymentStatus, DomainConstants.PaymentStatus.Pending, StringComparison.OrdinalIgnoreCase))
            {
                payment.PaymentStatus = DomainConstants.PaymentStatus.Cancelled;
                payment.UpdatedAt = now;
                payment.FailureReason ??= "Customer cancelled checkout transaction.";
            }
        }

        booking.BookingStatus = DomainConstants.EntityStatus.Cancelled;
        booking.ExpiredAt = now;

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, "Pending booking cancelled and seats released successfully.");
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
            .Include(b => b.Showtime)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

        if (booking == null) return ServiceResult<bool>.Fail(404, "Booking not found.", "NOT_FOUND");

        // Kiểm tra thời hạn Token (Token hết hạn trước giờ chiếu 2 tiếng)
        if (booking.Showtime != null && booking.Showtime.StartTime.AddHours(-2) < _clock.UtcNow)
        {
            return ServiceResult<bool>.Fail(400, "Token has expired because it is less than 2 hours before showtime.", "TOKEN_EXPIRED_TIME_LIMIT");
        }

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
            booking.BookingStatus = DomainConstants.EntityStatus.PendingRefund;

            var payment = booking.Payments.FirstOrDefault(p => p.PaymentStatus == DomainConstants.RefundStatus.Success) ?? booking.Payments.FirstOrDefault();

            if (payment == null)
            {
                payment = await _dbContext.Payments
                    .FirstOrDefaultAsync(p => p.BookingId == booking.BookingId && p.PaymentStatus == DomainConstants.RefundStatus.Success, cancellationToken);

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

    public async Task<ServiceResult<bool>> ReassignBookingSeatAsync(
        ReassignSeatRequest request,
        CancellationToken cancellationToken)
    {
        // 1. Tìm Booking cần chuyển ghế kèm theo các Seat liên quan
        var booking = await _dbContext.Bookings
            .Include(b => b.BookingSeats)
                .ThenInclude(bs => bs.ShowtimeSeat)
                    .ThenInclude(ss => ss.Seat)
            .Include(b => b.CustomerProfile)
                .ThenInclude(cp => cp!.User)
            .FirstOrDefaultAsync(b => b.BookingId == request.BookingId, cancellationToken);

        if (booking == null)
        {
            return ServiceResult<bool>.Fail(404, "Booking not found.", "BOOKING_NOT_FOUND");
        }

        // Không cho phép chuyển ghế đối với các vé đã hủy
        if (booking.BookingStatus == "CANCELLED")
        {
            return ServiceResult<bool>.Fail(400, "Cannot reassign seats for a cancelled booking.", "BOOKING_CANCELLED");
        }

        // 2. Tìm BookingSeat ứng với oldShowtimeSeatId trong đơn đặt vé này
        var bookingSeat = booking.BookingSeats.FirstOrDefault(bs => bs.ShowtimeSeatId == request.OldShowtimeSeatId);
        if (bookingSeat == null)
        {
            return ServiceResult<bool>.Fail(404, "The specified old seat was not found in this booking.", "OLD_SEAT_NOT_IN_BOOKING");
        }

        // 3. Tìm ShowtimeSeat mới trong CSDL
        var newShowtimeSeat = await _dbContext.ShowtimeSeats
            .Include(ss => ss.Seat)
            .FirstOrDefaultAsync(ss => ss.ShowtimeSeatId == request.NewShowtimeSeatId && ss.ShowtimeId == booking.ShowtimeId, cancellationToken);

        if (newShowtimeSeat == null)
        {
            return ServiceResult<bool>.Fail(404, "The specified new seat was not found in this showtime.", "NEW_SEAT_NOT_FOUND");
        }

        // Kiểm tra xem ghế mới có trống hay không
        if (newShowtimeSeat.SeatStatus != "AVAILABLE")
        {
            return ServiceResult<bool>.Fail(400, "The target new seat is not available.", "NEW_SEAT_NOT_AVAILABLE");
        }

        // 4. Tìm ShowtimeSeat cũ trong CSDL
        var oldShowtimeSeat = bookingSeat.ShowtimeSeat;

        // 5. Sử dụng Transaction để cập nhật an toàn
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Giải phóng ghế cũ
            oldShowtimeSeat.SeatStatus = "AVAILABLE";
            oldShowtimeSeat.LockedUntil = null;
            oldShowtimeSeat.LockedByUserId = null;

            // Khóa ghế mới cho đơn hàng này
            newShowtimeSeat.SeatStatus = "BOOKED";

            // Cập nhật lại liên kết ghế cho BookingSeat
            bookingSeat.ShowtimeSeatId = newShowtimeSeat.ShowtimeSeatId;

            // Lưu thay đổi
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Gửi email thông báo chuyển ghế thủ công cho khách hàng qua AI
            var customerEmail = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
            if (!string.IsNullOrEmpty(customerEmail) && _backgroundJobClient != null)
            {
                string subject = "Thông báo thay đổi ghế ngồi / Showtime Seat Change Notice";
                _backgroundJobClient.Enqueue<IAiEmailService>(ai => 
                    ai.SendAiApologyEmailAsync(
                        customerEmail, 
                        subject, 
                        "Thay đổi ghế ngồi do yêu cầu kỹ thuật rạp", 
                        $"Ghế ngồi của bạn cho mã đặt vé {booking.BookingId} đã được điều chỉnh đổi từ ghế {oldShowtimeSeat.Seat.SeatCode} sang ghế {newShowtimeSeat.Seat.SeatCode} do yêu cầu vận hành kỹ thuật phòng chiếu.", 
                        CancellationToken.None));
            }

            return ServiceResult<bool>.Ok(true, "Seat reassigned successfully.");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<bool>.Fail(500, $"Internal server error while reassigning seat: {ex.Message}", "INTERNAL_ERROR");
        }
    }
}