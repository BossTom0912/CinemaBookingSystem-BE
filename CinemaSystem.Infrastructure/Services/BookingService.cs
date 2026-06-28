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

    public BookingService(CinemaDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
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

        var showtimeSeats = await _dbContext.ShowtimeSeats
            .Include(ss => ss.Seat)
            .ThenInclude(s => s.SeatType)
            .Where(ss => request.ShowtimeSeatIds.Contains(ss.ShowtimeSeatId) && ss.ShowtimeId == request.ShowtimeId)
            .ToListAsync(cancellationToken);

        if (showtimeSeats.Count != request.ShowtimeSeatIds.Count)
        {
            return ServiceResult<BookingResponse>.Fail(400, "One or more selected seats are invalid.", "INVALID_SEATS");
        }

        var now = _clock.UtcNow;

        foreach (var ss in showtimeSeats)
        {
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

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";
}
