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

public sealed class BookingService : IBookingService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IClock _clock;
    private readonly CinemaSystem.Application.Settings.SecuritySettings _securitySettings;

    public BookingService(CinemaDbContext dbContext, IClock clock, Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.SecuritySettings> securityOptions)
    {
        _dbContext = dbContext;
        _clock = clock;
        _securitySettings = securityOptions.Value;
    }

    public async Task<ServiceResult<BookingResponse>> CreateBookingAsync(
        CreateBookingRequest request,
        string userId,
        CancellationToken cancellationToken)
    {
        await CancelExpiredPendingBookingsAsync(request.ShowtimeId, null, cancellationToken);

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

        var showtimeSeats = await _dbContext.ShowtimeSeats
            .Include(ss => ss.BookingSeat)
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
            if (ss.BookingSeat != null)
            {
                return ServiceResult<BookingResponse>.Fail(409, $"Seat {ss.Seat.SeatCode} is already booked.", "SEAT_ALREADY_BOOKED");
            }

            if (ss.SeatStatus == "BOOKED")
            {
                return ServiceResult<BookingResponse>.Fail(409, $"Seat {ss.Seat.SeatCode} is already booked.", "SEAT_ALREADY_BOOKED");
            }

            if (ss.SeatStatus == "LOCKED" && ss.LockedByUserId != userId && ss.LockedUntil > now)
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
            BookingStatus = "PENDING_PAYMENT",
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
            ss.SeatStatus = "LOCKED";
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
        await CancelExpiredPendingBookingsAsync(null, userId, cancellationToken);

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

        if (string.Equals(booking.BookingStatus, BookingConstants.BookingStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<BookingDetailsResponse>.Fail(404, "Booking has been cancelled.", "BOOKING_CANCELLED");
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
        await CancelExpiredPendingBookingsAsync(null, userId, cancellationToken);

        var bookings = await _dbContext.Bookings
            .Include(b => b.CustomerProfile)
            .Where(b =>
                b.CustomerProfile != null
                && b.CustomerProfile.UserId == userId
                && b.BookingStatus != BookingConstants.BookingStatus.Cancelled)
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
        var booking = await _dbContext.Bookings
            .Include(b => b.CustomerProfile)
            .Include(b => b.BookingSeats)
                .ThenInclude(bs => bs.ShowtimeSeat)
            .Include(b => b.Payments)
            .Include(b => b.VoucherUsage)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

        if (booking == null)
        {
            return ServiceResult<bool>.Fail(404, "Booking not found.", "BOOKING_NOT_FOUND");
        }

        if (booking.CustomerProfile?.UserId != userId)
        {
            return ServiceResult<bool>.Fail(403, "You do not have permission to cancel this booking.", "FORBIDDEN");
        }

        if (string.Equals(booking.BookingStatus, BookingConstants.BookingStatus.Cancelled, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<bool>.Ok(true, "Booking has already been cancelled.");
        }

        if (!string.Equals(booking.BookingStatus, BookingConstants.BookingStatus.PendingPayment, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<bool>.Fail(409, "Only pending payment bookings can be cancelled.", "BOOKING_NOT_PENDING_PAYMENT");
        }

        CancelPendingBookingEntity(booking, _clock.UtcNow, DomainConstants.PaymentStatus.Cancelled);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, "Booking cancelled and seats released successfully.");
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

    private async Task CancelExpiredPendingBookingsAsync(
        string? showtimeId,
        string? userId,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var query = _dbContext.Bookings
            .Include(b => b.CustomerProfile)
            .Include(b => b.BookingSeats)
                .ThenInclude(bs => bs.ShowtimeSeat)
            .Include(b => b.Payments)
            .Include(b => b.VoucherUsage)
            .Where(b =>
                b.BookingStatus == BookingConstants.BookingStatus.PendingPayment
                && b.ExpiredAt.HasValue
                && b.ExpiredAt.Value <= now);

        if (!string.IsNullOrWhiteSpace(showtimeId))
        {
            query = query.Where(b => b.ShowtimeId == showtimeId);
        }

        if (!string.IsNullOrWhiteSpace(userId))
        {
            query = query.Where(b => b.CustomerProfile != null && b.CustomerProfile.UserId == userId);
        }

        var expiredBookings = await query.ToListAsync(cancellationToken);
        if (expiredBookings.Count == 0)
        {
            return;
        }

        foreach (var booking in expiredBookings)
        {
            CancelPendingBookingEntity(booking, now, DomainConstants.PaymentStatus.Expired);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
    }

    private void CancelPendingBookingEntity(
        Booking booking,
        DateTime cancelledAt,
        string paymentStatus)
    {
        booking.BookingStatus = BookingConstants.BookingStatus.Cancelled;
        booking.ExpiredAt = cancelledAt;

        foreach (var bookingSeat in booking.BookingSeats.ToList())
        {
            bookingSeat.ShowtimeSeat.SeatStatus = BookingConstants.ShowtimeSeatStatus.Available;
            bookingSeat.ShowtimeSeat.LockedUntil = null;
            bookingSeat.ShowtimeSeat.LockedByUserId = null;
        }

        _dbContext.BookingSeats.RemoveRange(booking.BookingSeats);

        foreach (var payment in booking.Payments.Where(payment =>
                     string.Equals(payment.PaymentStatus, DomainConstants.PaymentStatus.Pending, StringComparison.OrdinalIgnoreCase)))
        {
            payment.PaymentStatus = paymentStatus;
            payment.UpdatedAt = cancelledAt;
        }

        if (booking.VoucherUsage is not null)
        {
            booking.VoucherUsage.UsageStatus = "CANCELLED";
        }
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
