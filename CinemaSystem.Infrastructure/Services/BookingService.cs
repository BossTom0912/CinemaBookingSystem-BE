using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CinemaSystem.Domain.Constants;
using Hangfire;

namespace CinemaSystem.Infrastructure.Services;

public sealed class BookingService : IBookingService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IClock _clock;
    private readonly ISeatLockStore _seatLockStore;
    private readonly ILogger<BookingService> _logger;
    private readonly CinemaSystem.Application.Settings.SecuritySettings _securitySettings;
    private readonly Hangfire.IBackgroundJobClient _backgroundJobClient;
    private readonly IAiEmailService _aiEmailService;
    private readonly BookingSettings _bookingSettings;
    private readonly IVoucherService _voucherService;
    private readonly IVoucherReservationService _voucherReservationService;
    private readonly ICancellationCompensationService _cancellationCompensationService;
    private readonly IRefundClaimIssuer _refundClaimIssuer;
    private readonly IEmailSender _emailSender;
    private readonly RefundSettings _refundSettings;
    private readonly CinemaSystem.Application.Settings.EmailTemplatesSettings _emailTemplates;

    public BookingService(
        CinemaDbContext dbContext,
        IClock clock,
        Microsoft.Extensions.Options.IOptions<CinemaSystem.Application.Settings.SecuritySettings> securityOptions,
        IOptions<BookingSettings> bookingOptions,
        ISeatLockStore seatLockStore,
        IAiEmailService aiEmailService,
        ILogger<BookingService> logger,
        IVoucherService voucherService,
        IVoucherReservationService voucherReservationService,
        ICancellationCompensationService cancellationCompensationService,
        IRefundClaimIssuer refundClaimIssuer,
        IEmailSender emailSender,
        IOptions<RefundSettings> refundSettings,
        IOptions<CinemaSystem.Application.Settings.EmailTemplatesSettings> emailTemplates,
        Hangfire.IBackgroundJobClient? backgroundJobClient = null)
    {
        _dbContext = dbContext;
        _clock = clock;
        _seatLockStore = seatLockStore;
        _logger = logger;
        _securitySettings = securityOptions.Value;
        _bookingSettings = bookingOptions.Value;
        _aiEmailService = aiEmailService;
        _voucherService = voucherService;
        _voucherReservationService = voucherReservationService;
        _cancellationCompensationService = cancellationCompensationService;
        _refundClaimIssuer = refundClaimIssuer;
        _emailSender = emailSender;
        _refundSettings = refundSettings.Value;
        _emailTemplates = emailTemplates.Value;
        _backgroundJobClient = backgroundJobClient!;
    }

    public async Task<ServiceResult<BookingResponse>> CreateBookingAsync(
        CreateBookingRequest request,
        string userId,
        Guid? clientRequestId,
        CancellationToken cancellationToken)
    {
        var customerProfile = await _dbContext.CustomerProfiles
            .FirstOrDefaultAsync(cp => cp.UserId == userId, cancellationToken);

        if (customerProfile == null)
        {
            return ServiceResult<BookingResponse>.Fail(403, "Only customers can book tickets.", "CUSTOMER_PROFILE_NOT_FOUND");
        }

        var requestFingerprint = clientRequestId.HasValue
            ? CreateRequestFingerprint(request)
            : null;
        var existingBooking = clientRequestId.HasValue
            ? await FindBookingByClientRequestAsync(
                customerProfile.CustomerProfileId,
                clientRequestId.Value,
                cancellationToken)
            : null;

        if (existingBooking != null)
        {
            if (!string.Equals(existingBooking.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
            {
                return ServiceResult<BookingResponse>.Fail(
                    409,
                    "This Idempotency-Key was already used for a different checkout request.",
                    "IDEMPOTENCY_KEY_REUSED");
            }

            return ServiceResult<BookingResponse>.Ok(
                ToBookingResponse(existingBooking),
                "Existing checkout returned successfully.");
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

        if (!string.Equals(showtime.Status, DomainConstants.ShowtimeStatus.Open, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<BookingResponse>.Fail(400, "This showtime is no longer accepting bookings.", "SHOWTIME_UNAVAILABLE");
        }

        if (request.ShowtimeSeatIds.Count > _bookingSettings.MaxSeatsPerCheckout)
        {
            return ServiceResult<BookingResponse>.Fail(
                400,
                $"A booking can contain at most {_bookingSettings.MaxSeatsPerCheckout} seats.",
                "MAX_SEATS_EXCEEDED");
        }

        var compensationTicketCodes = request.CompensationTicketCodes ?? [];
        if (compensationTicketCodes.Count > 0
            && !string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            return ServiceResult<BookingResponse>.Fail(
                400,
                "A standard voucher cannot be combined with compensation tickets.",
                "VOUCHER_STACKING_NOT_ALLOWED");
        }

        await ReleaseStaleBookingSeatsForShowtimeAsync(
            request.ShowtimeId,
            request.ShowtimeSeatIds,
            _clock.UtcNow,
            cancellationToken);

        var showtimeSeats = await _dbContext.ShowtimeSeats
            .Include(ss => ss.BookingSeat)
                .ThenInclude(bs => bs!.Booking)
            .Include(ss => ss.Seat)
            .ThenInclude(s => s.SeatType)
            .Where(ss => request.ShowtimeSeatIds.Contains(ss.ShowtimeSeatId) && ss.ShowtimeId == request.ShowtimeId)
            .ToListAsync(cancellationToken);

        if (showtimeSeats.Count != request.ShowtimeSeatIds.Count)
        {
            return ServiceResult<BookingResponse>.Fail(400, "One or more selected seats are invalid.", "INVALID_SEATS");
        }

        var now = _clock.UtcNow;

        var onlineSaleClosesAt = showtime.StartTime.AddMinutes(-_bookingSettings.OnlineSaleCutoffMinutes);
        if (now >= onlineSaleClosesAt)
        {
            return ServiceResult<BookingResponse>.Fail(
                400,
                "Online ticket sales have closed for this showtime.",
                BookingConstants.ErrorCodes.OnlineSaleClosed);
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

        // Calculate total amount in the same order as the request so each
        // compensation ticket deterministically maps to one selected seat.
        decimal totalAmount = 0;
        var bookingSeats = new List<BookingSeat>();
        var showtimeSeatsById = showtimeSeats.ToDictionary(
            item => item.ShowtimeSeatId,
            StringComparer.Ordinal);
        foreach (var showtimeSeatId in request.ShowtimeSeatIds)
        {
            var ss = showtimeSeatsById[showtimeSeatId];
            var seatPrice = showtime.BasePrice + ss.Seat.SeatType.ExtraFee;
            totalAmount += seatPrice;
            bookingSeats.Add(new BookingSeat
            {
                BookingSeatId = NewId(DomainConstants.EntityIdPrefix.BookingSeat),
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
                    BookingFbitemId = NewId(DomainConstants.EntityIdPrefix.BookingFoodItem),
                    FbItemId = fbItem.FbItemId,
                    Quantity = itemRequest.Quantity,
                    UnitPrice = fbItem.Price,
                    Subtotal = subtotal
                });
            }
        }

        decimal bookingSubtotal = totalAmount;
        decimal discountAmount = 0;
        VoucherUsage? voucherUsage = null;
        Voucher? voucher = null;
        var bookingId = NewId(DomainConstants.EntityIdPrefix.Booking);
        var compensationDiscountAmount = 0m;
        if (compensationTicketCodes.Count > 0)
        {
            var reservationResult =
                await _cancellationCompensationService.ReserveTicketsAsync(
                    customerProfile.CustomerProfileId,
                    null,
                    null,
                    compensationTicketCodes,
                    bookingId,
                    bookingSeats,
                    now,
                    cancellationToken);
            if (!reservationResult.Success)
            {
                return ServiceResult<BookingResponse>.Fail(
                    reservationResult.StatusCode,
                    reservationResult.Message,
                    reservationResult.ErrorCode ?? "COMPENSATION_TICKET_ERROR");
            }

            compensationDiscountAmount = reservationResult.Data;
            totalAmount = Math.Max(0m, totalAmount - compensationDiscountAmount);
        }

        if (!string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            var validationResult = await _voucherService.ValidateAndGetVoucherAsync(
                request.VoucherCode,
                bookingSubtotal,
                customerProfile.CustomerProfileId,
                cancellationToken);

            if (!validationResult.Success)
            {
                return ServiceResult<BookingResponse>.Fail(validationResult.StatusCode, validationResult.Message, validationResult.ErrorCode ?? "VOUCHER_ERROR");
            }

            voucher = validationResult.Data.Voucher;
            discountAmount = validationResult.Data.DiscountAmount;
            totalAmount = bookingSubtotal - discountAmount;

            // Rule: If total amount after discount is less than 1,000 VND, convert to 0 VND (completely free)
            if (totalAmount < 1000m)
            {
                totalAmount = 0;
            }

            var claimedVoucher = await _voucherReservationService.FindAvailableClaimAsync(
                voucher.VoucherId,
                customerProfile.CustomerProfileId,
                cancellationToken);

            voucherUsage = new VoucherUsage
            {
                VoucherUsageId = NewId(DomainConstants.EntityIdPrefix.VoucherUsage),
                VoucherId = voucher.VoucherId,
                CustomerProfileId = customerProfile.CustomerProfileId,
                BookingId = bookingId,
                CustomerVoucherId = claimedVoucher?.CustomerVoucherId,
                CustomerVoucher = claimedVoucher,
                DiscountAmount = discountAmount,
                UsageStatus = DomainConstants.VoucherUsageStatus.Applied,
                UsedAt = null
            };

            if (totalAmount == 0)
            {
                await _voucherReservationService.ConfirmAsync(
                    voucherUsage,
                    now,
                    cancellationToken);
            }
        }

       

        var booking = new Booking
        {
            BookingId = bookingId,
            CustomerProfileId = customerProfile.CustomerProfileId,
            ShowtimeId = showtime.ShowtimeId,
            BookingStatus = totalAmount == 0 ? DomainConstants.EntityStatus.Paid : DomainConstants.EntityStatus.PendingPayment,
            TotalAmount = totalAmount,
            CompensationDiscountAmount = compensationDiscountAmount,
            CreatedAt = now,
            ExpiredAt = totalAmount == 0 ? null : now.AddMinutes(_bookingSettings.PendingPaymentExpiryMinutes),
            ClientRequestId = clientRequestId,
            RequestFingerprint = requestFingerprint,
            BookingChannel = DomainConstants.BookingChannel.Online,
            FbFulfillmentStatus = bookingFbItems.Count == 0
                ? FbConstants.FulfillmentStatus.NotRequired
                : FbConstants.FulfillmentStatus.Pending,
            BookingSeats = bookingSeats,
            BookingFbItems = bookingFbItems
        };

        if (totalAmount == 0)
        {
            // For free orders, book the seats immediately and generate tickets
            foreach (var ss in showtimeSeats)
            {
                ss.SeatStatus = DomainConstants.EntityStatus.Booked;
                ss.LockedUntil = null;
                ss.LockedByUserId = null;
            }

            foreach (var bs in bookingSeats)
            {
                bs.Ticket = new Ticket
                {
                    TicketId = NewId(DomainConstants.EntityIdPrefix.Ticket),
                    BookingSeatId = bs.BookingSeatId,
                    QrCode = GenerateTicketQrCode(bookingId, bs.BookingSeatId),
                    TicketStatus = DomainConstants.TicketStatus.Unused,
                    GeneratedAt = now
                };
                _dbContext.Tickets.Add(bs.Ticket);
            }

            if (voucherUsage != null && voucher != null)
            {
                voucher.UsedCount += 1;
            }

            await _cancellationCompensationService.ConfirmBookingReservationsAsync(
                bookingId,
                now,
                cancellationToken);
        }
        else
        {
            // Update showtime seats to LOCKED
            foreach (var ss in showtimeSeats)
            {
                ss.SeatStatus = DomainConstants.EntityStatus.Locked;
                ss.LockedUntil = booking.ExpiredAt;
                ss.LockedByUserId = userId;
            }
        }

        _dbContext.Bookings.Add(booking);
        if (voucherUsage != null)
        {
            _dbContext.VoucherUsages.Add(voucherUsage);
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            if (!IsCheckoutConflict(exception))
            {
                _logger.LogError(
                    exception,
                    "Checkout persistence failed for booking {BookingId}, showtime {ShowtimeId}, client request {ClientRequestId}.",
                    bookingId,
                    request.ShowtimeId,
                    clientRequestId);
                throw;
            }

            _logger.LogWarning(
                exception,
                "Checkout concurrency conflict for booking {BookingId}, showtime {ShowtimeId}, client request {ClientRequestId}.",
                bookingId,
                request.ShowtimeId,
                clientRequestId);

            // SQL Server's filtered unique index is the final guard when two
            // requests with the same key arrive at the same time. A fresh query
            // returns the winner's response; other seat conflicts stay a 409.
            _dbContext.ChangeTracker.Clear();
            var recoveredBooking = clientRequestId.HasValue
                ? await FindBookingByClientRequestAsync(
                    customerProfile.CustomerProfileId,
                    clientRequestId.Value,
                    cancellationToken)
                : null;

            if (recoveredBooking != null
                && string.Equals(recoveredBooking.RequestFingerprint, requestFingerprint, StringComparison.Ordinal))
            {
                return ServiceResult<BookingResponse>.Ok(
                    ToBookingResponse(recoveredBooking),
                    "Existing checkout returned successfully.");
            }

            if (IsVoucherReservationConflict(exception))
            {
                return ServiceResult<BookingResponse>.Fail(
                    409,
                    "This claimed voucher is already reserved by another checkout.",
                    "VOUCHER_ALREADY_RESERVED");
            }

            if (IsCompensationReservationConflict(exception))
            {
                return ServiceResult<BookingResponse>.Fail(
                    409,
                    "This compensation ticket is already reserved by another checkout.",
                    "COMPENSATION_TICKET_ALREADY_RESERVED");
            }

            return ServiceResult<BookingResponse>.Fail(
                409,
                "The selected seats were changed by another checkout. Please refresh the seat map.",
                "CHECKOUT_CONFLICT");
        }

        // Release temporary selection locks from Redis since DB now holds the authoritative lock (LockedUntil = booking.ExpiredAt)
        foreach (var ss in showtimeSeats)
        {
            await _seatLockStore.ReleaseAsync($"seat_lock_{showtime.ShowtimeId}_{ss.SeatId}", cancellationToken);
        }

        return ServiceResult<BookingResponse>.Ok(new BookingResponse
        {
            BookingId = booking.BookingId,
            ShowtimeId = booking.ShowtimeId,
            MovieTitle = showtime.Movie.Title,
            CinemaName = showtime.Room.Cinema.CinemaName,
            RoomName = showtime.Room.RoomName,
            StartTime = showtime.StartTime,
            TotalAmount = booking.TotalAmount,
            CompensationDiscountAmount = booking.CompensationDiscountAmount,
            Status = booking.BookingStatus,
            CreatedAt = booking.CreatedAt,
            ExpiredAt = booking.ExpiredAt
        }, totalAmount == 0 ? "Booking created and confirmed successfully (Free order)." : "Booking created successfully.");
    }

    private static bool IsCheckoutConflict(DbUpdateException exception)
    {
        return exception is DbUpdateConcurrencyException
            || exception.InnerException is SqlException { Number: 2601 or 2627 };
    }

    private static bool IsVoucherReservationConflict(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 } sqlException
            && sqlException.Message.Contains(
                "UX_VOUCHER_USAGE_ACTIVE_CUSTOMER_VOUCHER",
                StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsCompensationReservationConflict(
        DbUpdateException exception)
    {
        return exception is DbUpdateConcurrencyException concurrencyException
            && concurrencyException.Entries.Any(entry =>
                entry.Entity is CompensationTicket);
    }

    public async Task<ServiceResult<CheckoutRecoveryResponse>> GetCheckoutRecoveryAsync(
        string userId,
        Guid clientRequestId,
        CancellationToken cancellationToken)
    {
        var recovery = await _dbContext.Bookings
            .AsNoTracking()
            .Where(booking => booking.CustomerProfile != null
                && booking.CustomerProfile.UserId == userId
                && booking.ClientRequestId == clientRequestId)
            .Select(booking => new CheckoutRecoveryResponse
            {
                BookingId = booking.BookingId,
                ShowtimeId = booking.ShowtimeId,
                BookingStatus = booking.BookingStatus,
                PaymentStatus = booking.Payments
                    .OrderByDescending(payment => payment.CreatedAt)
                    .Select(payment => payment.PaymentStatus)
                    .FirstOrDefault(),
                ExpiredAt = booking.ExpiredAt
            })
            .FirstOrDefaultAsync(cancellationToken);

        return recovery == null
            ? ServiceResult<CheckoutRecoveryResponse>.Fail(
                404,
                "No checkout was found for this Idempotency-Key.",
                "CHECKOUT_NOT_FOUND")
            : ServiceResult<CheckoutRecoveryResponse>.Ok(
                recovery,
                "Checkout recovery state retrieved successfully.");
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
            .Include(b => b.VoucherUsage)
                .ThenInclude(vu => vu!.Voucher)
            .AsSplitQuery()
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
            DiscountAmount = booking.VoucherUsage?.DiscountAmount ?? 0,
            CompensationDiscountAmount = booking.CompensationDiscountAmount,
            VoucherCode = booking.VoucherUsage?.Voucher?.VoucherCode,
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
                CompensationDiscountAmount = b.CompensationDiscountAmount,
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
            .Include(item => item.VoucherUsage)
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.BookingId == bookingId, cancellationToken);

        if (booking == null)
        {
            return ServiceResult<bool>.Fail(404, "Booking not found.", "BOOKING_NOT_FOUND");
        }

        if (booking.CustomerProfile?.UserId != userId)
        {
            return ServiceResult<bool>.Fail(403, "You do not have permission to cancel this booking.", "FORBIDDEN");
        }

        var hasSuccessfulPayment = booking.Payments.Any(payment =>
            string.Equals(payment.PaymentStatus, DomainConstants.PaymentStatus.Success, StringComparison.OrdinalIgnoreCase));

        if (IsFinalPaidStatus(booking.BookingStatus) || hasSuccessfulPayment)
        {
            return ServiceResult<bool>.Fail(409, "Paid booking cannot be cancelled from checkout.", "BOOKING_ALREADY_PAID");
        }

        if (!CanCancelCheckoutBookingStatus(booking.BookingStatus))
        {
            return ServiceResult<bool>.Fail(400, "Only unpaid checkout bookings can be cancelled.", "INVALID_BOOKING_STATUS");
        }

        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

            var now = _clock.UtcNow;
            await _cancellationCompensationService.ReleaseBookingReservationsAsync(
                booking.BookingId,
                cancellationToken);

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

            if (booking.VoucherUsage != null && string.Equals(booking.VoucherUsage.UsageStatus, DomainConstants.VoucherUsageStatus.Applied, StringComparison.OrdinalIgnoreCase))
            {
                await _voucherReservationService.CancelAsync(
                    booking.VoucherUsage,
                    cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });

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
            .Include(b => b.CustomerProfile)
                .ThenInclude(profile => profile!.User)
            .Include(b => b.Showtime)
                .ThenInclude(showtime => showtime.Movie)
            .FirstOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);

        if (booking == null) return ServiceResult<bool>.Fail(404, "Booking not found.", "NOT_FOUND");

        // Kiểm tra thời hạn Token (Token hết hạn trước giờ chiếu 2 tiếng)
        if (booking.Showtime != null && booking.Showtime.StartTime.AddHours(-2) < _clock.UtcNow)
        {
            return ServiceResult<bool>.Fail(400, "Token has expired because it is less than 2 hours before showtime.", "TOKEN_EXPIRED_TIME_LIMIT");
        }

        // Xử lý Idempotency cho đơn hàng đã xác nhận hoặc đã hoàn tiền trước đó
        if (booking.BookingStatus != DomainConstants.EntityStatus.ProcessingUnstable)
        {
            if (booking.BookingStatus == DomainConstants.EntityStatus.Paid)
            {
                return ServiceResult<bool>.Ok(
                    true,
                    "ALREADY_ACCEPTED: Quý khách đã lựa chọn xác nhận tham dự suất chiếu mới cho đơn hàng này trước đó. Đơn vé của bạn đã ở trạng thái hợp lệ và vị trí ghế được giữ nguyên.");
            }

            if (booking.BookingStatus == DomainConstants.EntityStatus.PendingRefund ||
                booking.BookingStatus == DomainConstants.EntityStatus.Refunded ||
                booking.BookingStatus == DomainConstants.EntityStatus.Cancelled)
            {
                return ServiceResult<bool>.Ok(
                    true,
                    "ALREADY_REFUNDED: Quý khách đã lựa chọn yêu cầu hoàn tiền 100% cho đơn hàng này trước đó. Hệ thống đang tiến hành xử lý hoàn tiền tự động về tài khoản của bạn.");
            }

            return ServiceResult<bool>.Fail(400, "Đơn hàng không ở trạng thái chờ xác nhận đổi giờ chiếu.", "INVALID_STATUS");
        }

        if (accept)
        {
            booking.BookingStatus = DomainConstants.EntityStatus.Paid;
            await IssueRescheduleAcceptanceVoucherAsync(booking, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<bool>.Ok(
                true,
                "Time change accepted successfully. A 20% ticket voucher valid for one month was added to your compensation vouchers.");
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

            if (booking.TotalAmount > 0 && (payment == null || string.IsNullOrEmpty(payment.PaymentId) || string.IsNullOrEmpty(payment.PaymentProviderId)))
            {
                return ServiceResult<bool>.Fail(400, "Cannot reject time change because no valid payment record exists to process the refund.", "INVALID_PAYMENT");
            }

            var refund = new Refund
            {
                RefundId = NewId(DomainConstants.EntityIdPrefix.Refund),
                BookingId = booking.BookingId,
                PaymentId = payment?.PaymentId ?? "ZERO_PAYMENT",
                PaymentProviderId = payment?.PaymentProviderId ?? "VOUCHER",
                RefundAmount = booking.TotalAmount,
                RefundStatus = DomainConstants.RefundStatus.Pending,
                RefundReason = "User rejected time change",
                RequestedAt = _clock.UtcNow
            };
            _dbContext.Refunds.Add(refund);
            RefundClaimIssue? claimIssue = null;
            if (!string.IsNullOrWhiteSpace(booking.CustomerProfileId))
            {
                claimIssue = _refundClaimIssuer.Create(
                    refund.RefundId,
                    booking.CustomerProfileId,
                    _clock.UtcNow);
                _dbContext.RefundClaims.Add(claimIssue.Claim);
            }
            await IssueRescheduleRejectionVoucherAsync(booking, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await TrySendTimeChangeRefundClaimEmailAsync(booking, claimIssue, cancellationToken);
            return ServiceResult<bool>.Ok(true, "Time change rejected. Refund claim initiated.");
        }
    }

    private async Task TrySendTimeChangeRefundClaimEmailAsync(
        Booking booking,
        RefundClaimIssue? claimIssue,
        CancellationToken cancellationToken)
    {
        var email = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
        if (claimIssue is null || string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        try
        {
            var link = $"{_refundSettings.FrontendBaseUrl.TrimEnd('/')}{RefundSettings.ClaimRoute}?t={Uri.EscapeDataString(claimIssue.RawToken)}";
            await _emailSender.SendEmailAsync(
                email,
                _emailTemplates.ShowtimeCancelledRefundSubject,
                string.Format(
                    CultureInfo.InvariantCulture,
                    _emailTemplates.ShowtimeCancelledRefundBody,
                    booking.Showtime.Movie.Title,
                    booking.Showtime.StartTime,
                    booking.TotalAmount,
                    claimIssue.Token.ExpiresAt,
                    link),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Refund claim email could not be sent for booking {BookingId}.", booking.BookingId);
        }
    }

    private async Task IssueRescheduleAcceptanceVoucherAsync(
        Booking booking,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(booking.CustomerProfileId))
        {
            return;
        }

        var voucherCode = $"VOUCHER_REFUND_20PCT_{booking.BookingId}".ToUpperInvariant();
        var voucher = await _dbContext.Vouchers
            .FirstOrDefaultAsync(item => item.VoucherCode == voucherCode, cancellationToken);
        if (voucher is null)
        {
            var now = _clock.UtcNow;
            voucher = new Voucher
            {
                VoucherId = NewId(DomainConstants.EntityIdPrefix.Voucher),
                VoucherCode = voucherCode,
                Title = "VOUCHER_REFUND_20_PERCENT_RESCHEDULE",
                Description = "Voucher bồi thường giảm 20% giá vé phim khi khách đồng ý đổi suất chiếu.",
                DiscountType = DomainConstants.DiscountType.Percent,
                DiscountValue = 20m,
                UsageLimit = 1,
                PerCustomerLimit = 1,
                UsedCount = 0,
                StartDate = now,
                EndDate = now.AddMonths(1),
                VoucherStatus = DomainConstants.VoucherStatus.Active
            };
            _dbContext.Vouchers.Add(voucher);
        }

        var alreadyAssigned = await _dbContext.CustomerVouchers.AnyAsync(
            item => item.CustomerProfileId == booking.CustomerProfileId
                && item.VoucherId == voucher.VoucherId,
            cancellationToken);
        if (!alreadyAssigned)
        {
            _dbContext.CustomerVouchers.Add(new CustomerVoucher
            {
                CustomerVoucherId = $"CV_{Guid.NewGuid():N}",
                CustomerProfileId = booking.CustomerProfileId,
                VoucherId = voucher.VoucherId,
                ClaimedAt = _clock.UtcNow,
                IsUsed = false
            });
        }
    }

    private async Task IssueRescheduleRejectionVoucherAsync(
        Booking booking,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(booking.CustomerProfileId))
        {
            return;
        }

        var voucherCode = $"VOUCHER_REFUND_100PCT_{booking.BookingId}".ToUpperInvariant();
        var voucher = await _dbContext.Vouchers
            .FirstOrDefaultAsync(item => item.VoucherCode == voucherCode, cancellationToken);
        if (voucher is null)
        {
            var now = _clock.UtcNow;
            voucher = new Voucher
            {
                VoucherId = NewId(DomainConstants.EntityIdPrefix.Voucher),
                VoucherCode = voucherCode,
                Title = "VOUCHER_REFUND_100_PERCENT_RESCHEDULE_CANCEL",
                Description = "Voucher bồi thường 100% khi khách từ chối đổi suất chiếu.",
                DiscountType = DomainConstants.DiscountType.Percent,
                DiscountValue = 100m,
                UsageLimit = 1,
                PerCustomerLimit = 1,
                UsedCount = 0,
                StartDate = now,
                EndDate = now.AddDays(180),
                VoucherStatus = DomainConstants.VoucherStatus.Active
            };
            _dbContext.Vouchers.Add(voucher);
        }

        var alreadyAssigned = await _dbContext.CustomerVouchers.AnyAsync(
            item => item.CustomerProfileId == booking.CustomerProfileId
                && item.VoucherId == voucher.VoucherId,
            cancellationToken);
        if (!alreadyAssigned)
        {
            _dbContext.CustomerVouchers.Add(new CustomerVoucher
            {
                CustomerVoucherId = $"CV_{Guid.NewGuid():N}",
                CustomerProfileId = booking.CustomerProfileId,
                VoucherId = voucher.VoucherId,
                ClaimedAt = _clock.UtcNow,
                IsUsed = false
            });
        }
    }

    private async Task<Booking?> FindBookingByClientRequestAsync(
        string customerProfileId,
        Guid clientRequestId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Bookings
            .Include(booking => booking.Showtime)
                .ThenInclude(showtime => showtime!.Movie)
            .Include(booking => booking.Showtime)
                .ThenInclude(showtime => showtime!.Room)
                    .ThenInclude(room => room!.Cinema)
            .FirstOrDefaultAsync(
                booking => booking.CustomerProfileId == customerProfileId
                    && booking.ClientRequestId == clientRequestId,
                cancellationToken);
    }

    private static BookingResponse ToBookingResponse(Booking booking)
    {
        return new BookingResponse
        {
            BookingId = booking.BookingId,
            ShowtimeId = booking.ShowtimeId ?? string.Empty,
            MovieTitle = booking.Showtime?.Movie?.Title ?? string.Empty,
            CinemaName = booking.Showtime?.Room?.Cinema?.CinemaName ?? string.Empty,
            RoomName = booking.Showtime?.Room?.RoomName ?? string.Empty,
            StartTime = booking.Showtime?.StartTime,
            TotalAmount = booking.TotalAmount,
            CompensationDiscountAmount = booking.CompensationDiscountAmount,
            Status = booking.BookingStatus,
            CreatedAt = booking.CreatedAt,
            ExpiredAt = booking.ExpiredAt
        };
    }

    private static string CreateRequestFingerprint(CreateBookingRequest request)
    {
        var seatIds = request.ShowtimeSeatIds
            .Select(item => item.Trim())
            .OrderBy(item => item, StringComparer.Ordinal);
        var foodItems = (request.FoodAndBeverages ?? [])
            .OrderBy(item => item.FbItemId, StringComparer.Ordinal)
            .ThenBy(item => item.Quantity)
            .Select(item => $"{item.FbItemId.Trim()}:{item.Quantity}");
        var canonicalRequest = string.Join(
            "|",
            request.ShowtimeId.Trim(),
            string.Join(",", seatIds),
            (request.VoucherCode ?? string.Empty).Trim(),
            string.Join(",", foodItems),
            string.Join(
                ",",
                (request.CompensationTicketCodes ?? [])
                    .Select(item => item.Trim().ToUpperInvariant())
                    .OrderBy(item => item, StringComparer.Ordinal)));

        return Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(canonicalRequest)));
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
            .Include(item => item.VoucherUsage)
            .AsSplitQuery()
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

            if (booking.VoucherUsage != null && string.Equals(booking.VoucherUsage.UsageStatus, DomainConstants.VoucherUsageStatus.Applied, StringComparison.OrdinalIgnoreCase))
            {
                await _voucherReservationService.CancelAsync(
                    booking.VoucherUsage,
                    cancellationToken);
            }

            await _cancellationCompensationService.ReleaseBookingReservationsAsync(
                booking.BookingId,
                cancellationToken);

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

    private static bool CanCancelCheckoutBookingStatus(string? status)
    {
        return string.Equals(status, DomainConstants.EntityStatus.PendingPayment, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DomainConstants.BookingStatus.Created, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DomainConstants.EntityStatus.Failed, StringComparison.OrdinalIgnoreCase)
            || string.Equals(status, DomainConstants.EntityStatus.Cancelled, StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSeatLockKey(string showtimeId, string seatId)
    {
        return $"seat-lock:{showtimeId}:{seatId}";
    }

    private static string NewId(string prefix) => CinemaSystem.Domain.Utilities.IdGenerator.NewId(prefix);

    private static string GenerateTicketQrCode(string bookingId, string bookingSeatId) =>
        string.Join(
            DomainConstants.TicketQrCode.Separator,
            DomainConstants.TicketQrCode.Prefix,
            bookingId,
            bookingSeatId,
            Guid.NewGuid().ToString("N"));

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
        if (booking.BookingStatus == DomainConstants.BookingStatus.Cancelled)
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
        if (newShowtimeSeat.SeatStatus != DomainConstants.ShowtimeSeatStatus.Available)
        {
            return ServiceResult<bool>.Fail(400, "The target new seat is not available.", "NEW_SEAT_NOT_AVAILABLE");
        }

        // 4. Tìm ShowtimeSeat cũ trong CSDL
        var oldShowtimeSeat = bookingSeat.ShowtimeSeat;

        // 5. Sử dụng Transaction để cập nhật an toàn
        try
        {
            var strategy = _dbContext.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

                // Giải phóng ghế cũ
                oldShowtimeSeat.SeatStatus = DomainConstants.ShowtimeSeatStatus.Available;
                oldShowtimeSeat.LockedUntil = null;
                oldShowtimeSeat.LockedByUserId = null;

                // Khóa ghế mới cho đơn hàng này
                newShowtimeSeat.SeatStatus = DomainConstants.ShowtimeSeatStatus.Booked;

                // Cập nhật lại liên kết ghế cho BookingSeat
                bookingSeat.ShowtimeSeatId = newShowtimeSeat.ShowtimeSeatId;

                // Lưu thay đổi
                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            });

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
            return ServiceResult<bool>.Fail(500, $"Internal server error while reassigning seat: {ex.Message}", "INTERNAL_ERROR");
        }
    }

    public async Task<ServiceResult<BookingDetailsResponse>> CreateCounterBookingAsync(
        CreateCounterBookingRequest request,
        string staffProfileId,
        string currentStaffCinemaId,
        CancellationToken cancellationToken)
    {
        var showtime = await _dbContext.Showtimes
            .Include(s => s.Movie)
            .Include(s => s.Room)
                .ThenInclude(r => r.Cinema)
            .FirstOrDefaultAsync(s => s.ShowtimeId == request.ShowtimeId, cancellationToken);

        if (showtime == null)
        {
            return ServiceResult<BookingDetailsResponse>.Fail(404, "Showtime not found.", "SHOWTIME_NOT_FOUND");
        }

        // Validate cinema scoping: counter booking must belong to the staff's cinema
        if (!string.IsNullOrEmpty(currentStaffCinemaId) && !string.Equals(showtime.Room.CinemaId, currentStaffCinemaId, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<BookingDetailsResponse>.Fail(403, "Staff is not authorized to sell tickets for other cinemas.", "FORBIDDEN_CINEMA_BRANCH");
        }

        if (!string.Equals(showtime.Status, DomainConstants.ShowtimeStatus.Open, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<BookingDetailsResponse>.Fail(400, "This showtime is no longer accepting bookings.", "SHOWTIME_UNAVAILABLE");
        }

        if (request.ShowtimeSeatIds.Count > _bookingSettings.MaxSeatsPerCheckout)
        {
            return ServiceResult<BookingDetailsResponse>.Fail(
                400,
                $"A booking can contain at most {_bookingSettings.MaxSeatsPerCheckout} seats.",
                "MAX_SEATS_EXCEEDED");
        }

        var counterCompensationCodes = request.CompensationTicketCodes ?? [];
        if (counterCompensationCodes.Count > 0
            && !string.IsNullOrWhiteSpace(request.VoucherCode))
        {
            return ServiceResult<BookingDetailsResponse>.Fail(
                400,
                "A standard voucher cannot be combined with compensation tickets.",
                "VOUCHER_STACKING_NOT_ALLOWED");
        }

        await ReleaseStaleBookingSeatsForShowtimeAsync(
            request.ShowtimeId,
            request.ShowtimeSeatIds,
            _clock.UtcNow,
            cancellationToken);

        var showtimeSeats = await _dbContext.ShowtimeSeats
            .Include(ss => ss.BookingSeat)
                .ThenInclude(bs => bs!.Booking)
            .Include(ss => ss.Seat)
                .ThenInclude(s => s.SeatType)
            .Where(ss => request.ShowtimeSeatIds.Contains(ss.ShowtimeSeatId) && ss.ShowtimeId == request.ShowtimeId)
            .ToListAsync(cancellationToken);

        if (showtimeSeats.Count != request.ShowtimeSeatIds.Count)
        {
            return ServiceResult<BookingDetailsResponse>.Fail(400, "One or more selected seats are invalid.", "INVALID_SEATS");
        }

        var now = _clock.UtcNow;

        foreach (var ss in showtimeSeats)
        {
            if (IsSoldBookingSeat(ss.BookingSeat) || ss.SeatStatus == DomainConstants.EntityStatus.Booked)
            {
                return ServiceResult<BookingDetailsResponse>.Fail(409, $"Seat {ss.Seat.SeatCode} is already booked.", "SEAT_ALREADY_BOOKED");
            }

            if (IsActivePendingBookingSeat(ss.BookingSeat, now))
            {
                return ServiceResult<BookingDetailsResponse>.Fail(409, $"Seat {ss.Seat.SeatCode} is waiting for payment.", "SEAT_PENDING_PAYMENT");
            }
        }

        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Calculate total amount from seats
            decimal totalAmount = 0;
            var bookingSeats = new List<BookingSeat>();
            var showtimeSeatsById = showtimeSeats.ToDictionary(
                item => item.ShowtimeSeatId,
                StringComparer.Ordinal);
            foreach (var showtimeSeatId in request.ShowtimeSeatIds)
            {
                var ss = showtimeSeatsById[showtimeSeatId];
                var seatPrice = showtime.BasePrice + ss.Seat.SeatType.ExtraFee;
                totalAmount += seatPrice;
                bookingSeats.Add(new BookingSeat
                {
                    BookingSeatId = NewId(DomainConstants.EntityIdPrefix.BookingSeat),
                    ShowtimeSeatId = ss.ShowtimeSeatId,
                    SeatPrice = seatPrice
                });
            }

            // Calculate F&B items
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
                    if (fbItem == null || fbItem.ItemStatus == FbConstants.ItemStatus.Inactive)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return ServiceResult<BookingDetailsResponse>.Fail(404, $"F&B item '{itemRequest.FbItemId}' is unavailable.", "ITEM_UNAVAILABLE");
                    }

                    // Deduct stock via Raw SQL (atomic inventory decrease)
                    int rowsAffected = await _dbContext.Database.ExecuteSqlRawAsync(
                        "UPDATE CINEMA_FB_INVENTORY SET quantity = quantity - {0} WHERE cinemaId = {1} AND fbItemId = {2} AND quantity >= {3}",
                        new object[] { itemRequest.Quantity, showtime.Room.CinemaId, itemRequest.FbItemId, itemRequest.Quantity },
                        cancellationToken);

                    if (rowsAffected == 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return ServiceResult<BookingDetailsResponse>.Fail(409, $"Insufficient stock for item '{fbItem.ItemName}' at this cinema branch.", "INSUFFICIENT_STOCK");
                    }

                    var subtotal = fbItem.Price * itemRequest.Quantity;
                    totalAmount += subtotal;
                    bookingFbItems.Add(new BookingFbItem
                    {
                        BookingFbitemId = NewId(DomainConstants.EntityIdPrefix.BookingFoodItem),
                        FbItemId = fbItem.FbItemId,
                        Quantity = itemRequest.Quantity,
                        UnitPrice = fbItem.Price,
                        Subtotal = subtotal
                    });
                }
            }

            decimal bookingSubtotal = totalAmount;
            decimal discountAmount = 0;
            VoucherUsage? voucherUsage = null;
            Voucher? voucher = null;
            var bookingId = NewId(DomainConstants.EntityIdPrefix.Booking);
            var compensationDiscountAmount = 0m;

            if (counterCompensationCodes.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(request.CustomerProfileId)
                    && (string.IsNullOrWhiteSpace(request.GuestEmail)
                        || string.IsNullOrWhiteSpace(request.GuestPhone)))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<BookingDetailsResponse>.Fail(
                        400,
                        "A customer profile or the original guest email and phone is required to use compensation tickets at the counter.",
                        "COMPENSATION_CUSTOMER_REQUIRED");
                }

                var reservationResult =
                    await _cancellationCompensationService.ReserveTicketsAsync(
                        request.CustomerProfileId,
                        request.GuestEmail,
                        request.GuestPhone,
                        counterCompensationCodes,
                        bookingId,
                        bookingSeats,
                        now,
                        cancellationToken);
                if (!reservationResult.Success)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<BookingDetailsResponse>.Fail(
                        reservationResult.StatusCode,
                        reservationResult.Message,
                        reservationResult.ErrorCode
                            ?? "COMPENSATION_TICKET_ERROR");
                }

                compensationDiscountAmount = reservationResult.Data;
                totalAmount = Math.Max(0m, totalAmount - compensationDiscountAmount);
            }

            if (!string.IsNullOrWhiteSpace(request.VoucherCode))
            {
                if (string.IsNullOrWhiteSpace(request.CustomerProfileId))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<BookingDetailsResponse>.Fail(
                        400,
                        "A customer profile is required to apply a voucher at the counter.",
                        "VOUCHER_CUSTOMER_REQUIRED");
                }

                var validationResult = await _voucherService.ValidateAndGetVoucherAsync(
                    request.VoucherCode,
                    bookingSubtotal,
                    request.CustomerProfileId,
                    cancellationToken);

                if (!validationResult.Success)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<BookingDetailsResponse>.Fail(validationResult.StatusCode, validationResult.Message, validationResult.ErrorCode ?? "VOUCHER_ERROR");
                }

                voucher = validationResult.Data.Voucher;
                discountAmount = validationResult.Data.DiscountAmount;
                totalAmount = bookingSubtotal - discountAmount;

                if (totalAmount < 1000m)
                {
                    totalAmount = 0;
                }

                var claimedVoucher = await _voucherReservationService.FindAvailableClaimAsync(
                    voucher.VoucherId,
                    request.CustomerProfileId,
                    cancellationToken);

                voucherUsage = new VoucherUsage
                {
                    VoucherUsageId = NewId(DomainConstants.EntityIdPrefix.VoucherUsage),
                    VoucherId = voucher.VoucherId,
                    CustomerProfileId = request.CustomerProfileId,
                    CustomerVoucherId = claimedVoucher?.CustomerVoucherId,
                    CustomerVoucher = claimedVoucher,
                    BookingId = string.Empty, // Set below
                    DiscountAmount = discountAmount,
                    UsageStatus = DomainConstants.VoucherUsageStatus.Applied,
                    UsedAt = null
                };

                await _voucherReservationService.ConfirmAsync(
                    voucherUsage,
                    now,
                    cancellationToken);
            }

            if (voucherUsage != null)
            {
                voucherUsage.BookingId = bookingId;
            }

            var booking = new Booking
            {
                BookingId = bookingId,
                CustomerProfileId = !string.IsNullOrEmpty(request.CustomerProfileId) ? request.CustomerProfileId : null,
                ShowtimeId = showtime.ShowtimeId,
                BookingStatus = DomainConstants.EntityStatus.Paid, // Paid immediately at counter
                TotalAmount = totalAmount,
                CompensationDiscountAmount = compensationDiscountAmount,
                CreatedAt = now,
                ExpiredAt = null, // POS tickets never expire
                BookingChannel = DomainConstants.BookingChannel.Counter,
                CreatedByStaffProfileId = staffProfileId,
                GuestName = request.GuestName,
                GuestPhone = request.GuestPhone,
                GuestEmail = request.GuestEmail,
                BookingSeats = bookingSeats,
                BookingFbItems = bookingFbItems,
                FbFulfillmentStatus = bookingFbItems.Any() ? FbConstants.FulfillmentStatus.Fulfilled : null,
                FbFulfilledAt = bookingFbItems.Any() ? now : null
            };

            // Book seats and generate tickets immediately
            foreach (var ss in showtimeSeats)
            {
                ss.SeatStatus = DomainConstants.EntityStatus.Booked;
                ss.LockedUntil = null;
                ss.LockedByUserId = null;
            }

            foreach (var bs in bookingSeats)
            {
                bs.Ticket = new Ticket
                {
                    TicketId = NewId(DomainConstants.EntityIdPrefix.Ticket),
                    BookingSeatId = bs.BookingSeatId,
                    QrCode = GenerateTicketQrCode(bookingId, bs.BookingSeatId),
                    TicketStatus = DomainConstants.TicketStatus.Unused,
                    GeneratedAt = now
                };
                _dbContext.Tickets.Add(bs.Ticket);
            }

            if (voucherUsage != null && voucher != null)
            {
                voucher.UsedCount += 1;
                _dbContext.VoucherUsages.Add(voucherUsage);
            }

            _dbContext.Bookings.Add(booking);
            await _cancellationCompensationService.ConfirmBookingReservationsAsync(
                bookingId,
                now,
                cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Generate detailed response body
            var seatResponses = new List<BookedSeatDetailsResponse>();
            foreach (var bs in bookingSeats)
            {
                var ss = showtimeSeats.First(s => s.ShowtimeSeatId == bs.ShowtimeSeatId);
                seatResponses.Add(new BookedSeatDetailsResponse
                {
                    SeatId = ss.SeatId,
                    SeatNumber = ss.Seat.SeatCode,
                    RowLabel = ss.Seat.RowLabel,
                    SeatType = ss.Seat.SeatType.TypeName,
                    Price = bs.SeatPrice,
                    TicketId = bs.Ticket?.TicketId,
                    TicketQrCode = bs.Ticket?.QrCode,
                    TicketStatus = bs.Ticket?.TicketStatus
                });
            }

            var fbResponses = new List<BookedFbItemResponse>();
            if (bookingFbItems.Any())
            {
                var fbItemIds = bookingFbItems.Select(f => f.FbItemId).ToList();
                var fbItems = await _dbContext.FbItems
                    .Where(f => fbItemIds.Contains(f.FbItemId))
                    .ToListAsync(cancellationToken);

                foreach (var bfi in bookingFbItems)
                {
                    var item = fbItems.FirstOrDefault(f => f.FbItemId == bfi.FbItemId);
                    fbResponses.Add(new BookedFbItemResponse
                    {
                        ItemName = item?.ItemName ?? "Unknown Item",
                        Quantity = bfi.Quantity,
                        Subtotal = bfi.Subtotal
                    });
                }
            }

            var response = new BookingDetailsResponse
            {
                BookingId = booking.BookingId,
                ShowtimeId = booking.ShowtimeId,
                MovieTitle = showtime.Movie.Title,
                CinemaName = showtime.Room.Cinema.CinemaName,
                RoomName = showtime.Room.RoomName,
                StartTime = showtime.StartTime,
                TotalAmount = booking.TotalAmount,
                DiscountAmount = discountAmount,
                CompensationDiscountAmount = compensationDiscountAmount,
                VoucherCode = request.VoucherCode,
                Status = booking.BookingStatus,
                CreatedAt = booking.CreatedAt,
                Seats = seatResponses,
                FoodAndBeverages = fbResponses
            };

            return ServiceResult<BookingDetailsResponse>.Ok(response, "Counter ticket booking completed successfully.", 201);
        }
        catch (DbUpdateConcurrencyException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<BookingDetailsResponse>.Fail(
                409,
                "This compensation ticket was used by another checkout.",
                "COMPENSATION_TICKET_ALREADY_RESERVED");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return ServiceResult<BookingDetailsResponse>.Fail(500, $"Internal server error while creating counter booking: {ex.Message}", "INTERNAL_ERROR");
        }
    }
}
