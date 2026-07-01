using System.Data;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Refunds;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Showtimes;

public sealed class ShowtimeCancellationService : IShowtimeCancellationService
{
    private const string ActiveEmploymentStatus = DomainConstants.EntityStatus.Active;

    private readonly CinemaDbContext _dbContext;
    private readonly IRefundClaimIssuer _refundClaimIssuer;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly RefundSettings _refundSettings;
    private readonly ILogger<ShowtimeCancellationService> _logger;

    public ShowtimeCancellationService(
        CinemaDbContext dbContext,
        IRefundClaimIssuer refundClaimIssuer,
        IEmailSender emailSender,
        IClock clock,
        IOptions<RefundSettings> refundSettings,
        ILogger<ShowtimeCancellationService> logger)
    {
        _dbContext = dbContext;
        _refundClaimIssuer = refundClaimIssuer;
        _emailSender = emailSender;
        _clock = clock;
        _refundSettings = refundSettings.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<CancelShowtimeResponse>> CancelShowtimeAsync(
        string showtimeId,
        string userId,
        CancelShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(showtimeId))
        {
            return Fail(400, "Showtime ID is required.", "SHOWTIME_ID_REQUIRED");
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Fail(401, "User is required.", "USER_REQUIRED");
        }

        var reason = request.Reason.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Fail(400, "Cancellation reason is required.", "CANCEL_REASON_REQUIRED");
        }

        if (reason.Length > RefundContractConstants.CancellationReasonMaxLength)
        {
            return Fail(
                400,
                $"Cancellation reason must not exceed {RefundContractConstants.CancellationReasonMaxLength} characters.",
                "CANCEL_REASON_TOO_LONG");
        }

        var actorExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(item => item.UserId == userId, cancellationToken);
        if (!actorExists)
        {
            return Fail(401, "User was not found.", "USER_NOT_FOUND");
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        try
        {
            var showtime = await LoadShowtimeForCancellationAsync(showtimeId.Trim(), cancellationToken);
            if (showtime is null)
            {
                return await RollbackAndFailAsync(
                    transaction,
                    404,
                    "Showtime was not found.",
                    "SHOWTIME_NOT_FOUND",
                    cancellationToken);
            }

            if (IsStatus(showtime.Status, BookingConstants.ShowtimeStatus.Cancelled)
                || showtime.ShowtimeCancellation is not null)
            {
                return await RollbackAndFailAsync(
                    transaction,
                    409,
                    "Showtime has already been cancelled.",
                    "SHOWTIME_ALREADY_CANCELLED",
                    cancellationToken);
            }

            var now = _clock.UtcNow;
            if (showtime.StartTime <= now)
            {
                return await RollbackAndFailAsync(
                    transaction,
                    409,
                    "A showtime that has already started cannot be cancelled.",
                    "SHOWTIME_ALREADY_STARTED",
                    cancellationToken);
            }

            var oldStatus = showtime.Status;
            var cancellationId = NewId(BookingConstants.EntityIdPrefix.ShowtimeCancellation);
            var staffProfileId = await GetActiveStaffProfileIdAsync(userId, cancellationToken);

            showtime.Status = BookingConstants.ShowtimeStatus.Cancelled;
            var cancellation = new ShowtimeCancellation
            {
                ShowtimeCancellationId = cancellationId,
                ShowtimeId = showtime.ShowtimeId,
                CancelledByUserId = userId,
                CancelledByStaffId = staffProfileId,
                CancelReason = reason,
                CancelledAt = now
            };
            _dbContext.ShowtimeCancellations.Add(cancellation);

            var paidBookingsMoved = 0;
            var unpaidBookingsCancelled = 0;
            var refundsCreated = 0;
            var totalRefundAmount = 0m;
            var cancellationEmails = new List<CancellationEmail>();

            foreach (var showtimeSeat in showtime.ShowtimeSeats)
            {
                MarkShowtimeSeatUnavailable(showtimeSeat);
            }

            foreach (var booking in showtime.Bookings)
            {
                if (IsStatus(booking.BookingStatus, BookingConstants.BookingStatus.Paid))
                {
                    var refundResult = MovePaidBookingToRefundPending(
                        booking,
                        cancellationId,
                        reason,
                        now);
                    if (!refundResult.Success)
                    {
                        return await RollbackAndFailAsync(
                            transaction,
                            409,
                            refundResult.Message,
                            refundResult.ErrorCode,
                            cancellationToken);
                    }

                    paidBookingsMoved++;
                    refundsCreated += refundResult.RefundCreated ? 1 : 0;
                    totalRefundAmount += refundResult.RefundAmount;
                    if (refundResult.RefundId is not null)
                    {
                        if (!string.IsNullOrWhiteSpace(booking.CustomerProfileId))
                        {
                            var issue = _refundClaimIssuer.Create(
                                refundResult.RefundId,
                                booking.CustomerProfileId,
                                now);
                            _dbContext.RefundClaims.Add(issue.Claim);
                            AddPaidCancellationEmail(
                                cancellationEmails,
                                booking,
                                showtime,
                                refundResult.RefundAmount,
                                issue.RawToken,
                                issue.Token.ExpiresAt);
                        }
                        else
                        {
                            AddCancellationEmail(cancellationEmails, booking, showtime);
                        }
                    }

                    AddCancellationNotification(booking, showtime, now);
                    continue;
                }

                if (IsStatus(booking.BookingStatus, BookingConstants.BookingStatus.Created)
                    || IsStatus(booking.BookingStatus, BookingConstants.BookingStatus.PendingPayment))
                {
                    CancelUnpaidBooking(booking, now);
                    unpaidBookingsCancelled++;
                    AddCancellationNotification(booking, showtime, now);
                    AddCancellationEmail(cancellationEmails, booking, showtime);
                }
            }

            _dbContext.AuditLogs.Add(CreateAuditLog(
                userId,
                showtime.ShowtimeId,
                oldStatus,
                cancellationId,
                reason,
                paidBookingsMoved,
                unpaidBookingsCancelled,
                refundsCreated,
                totalRefundAmount,
                now));

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Cancellation is committed before SMTP or payment-provider work. A failure
            // outside the database must never reopen a showtime or allow new ticket sales.
            await SendCancellationEmailsAsync(cancellationEmails, cancellationToken);

            _logger.LogInformation(
                "Showtime {ShowtimeId} cancelled by user {UserId}; refunds created: {RefundCount}, amount: {RefundAmount}.",
                showtime.ShowtimeId,
                userId,
                refundsCreated,
                totalRefundAmount);

            return ServiceResult<CancelShowtimeResponse>.Ok(
                new CancelShowtimeResponse
                {
                    ShowtimeId = showtime.ShowtimeId,
                    ShowtimeStatus = showtime.Status,
                    ShowtimeCancellationId = cancellationId,
                    PaidBookingsMovedToRefundPending = paidBookingsMoved,
                    UnpaidBookingsCancelled = unpaidBookingsCancelled,
                    RefundsCreated = refundsCreated,
                    TotalRefundAmount = totalRefundAmount,
                    RefundsSucceeded = 0,
                    RefundsManualRequired = 0,
                    RefundsPending = refundsCreated
                },
                "Showtime cancelled and refund data generated successfully.");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            await RollbackSafelyAsync(transaction);
            var alreadyCancelled = await _dbContext.ShowtimeCancellations
                .AsNoTracking()
                .AnyAsync(item => item.ShowtimeId == showtimeId, cancellationToken);
            if (alreadyCancelled)
            {
                return Fail(
                    409,
                    "Showtime has already been cancelled.",
                    "SHOWTIME_ALREADY_CANCELLED");
            }

            throw;
        }
        catch
        {
            await RollbackSafelyAsync(transaction);
            throw;
        }
    }

    private async Task<Showtime?> LoadShowtimeForCancellationAsync(
        string showtimeId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Showtimes
            .Include(item => item.Movie)
            .Include(item => item.Room)
                .ThenInclude(item => item.Cinema)
            .Include(item => item.ShowtimeCancellation)
            .Include(item => item.ShowtimeSeats)
            .Include(item => item.Bookings)
                .ThenInclude(item => item.CustomerProfile)
                    .ThenInclude(item => item!.User)
            .Include(item => item.Bookings)
                .ThenInclude(item => item.Payments)
                    .ThenInclude(item => item.Refunds)
            .Include(item => item.Bookings)
                .ThenInclude(item => item.BookingSeats)
                    .ThenInclude(item => item.Ticket)
            .Include(item => item.Bookings)
                .ThenInclude(item => item.BookingSeats)
                    .ThenInclude(item => item.ShowtimeSeat)
            .AsSplitQuery()
            .FirstOrDefaultAsync(item => item.ShowtimeId == showtimeId, cancellationToken);
    }

    private async Task<string?> GetActiveStaffProfileIdAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.StaffProfiles
            .AsNoTracking()
            .Where(item =>
                item.UserId == userId
                && item.EmploymentStatus == BookingConstants.ResourceStatus.Active)
            .Select(item => item.StaffProfileId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static RefundCreationResult MovePaidBookingToRefundPending(
        Booking booking,
        string cancellationId,
        string reason,
        DateTime now)
    {
        var successfulPayment = booking.Payments
            .Where(item => IsStatus(item.PaymentStatus, BookingConstants.PaymentStatus.Success))
            .OrderByDescending(item => item.PaidAt ?? item.CreatedAt)
            .FirstOrDefault();
        if (successfulPayment is null)
        {
            return RefundCreationResult.Fail(
                $"Paid booking {booking.BookingId} has no successful payment.",
                "PAID_BOOKING_PAYMENT_NOT_FOUND");
        }

        booking.BookingStatus = BookingConstants.BookingStatus.RefundPending;

        foreach (var bookingSeat in booking.BookingSeats)
        {
            if (bookingSeat.Ticket is not null
                && !IsStatus(bookingSeat.Ticket.TicketStatus, BookingConstants.TicketStatus.Refunded))
            {
                bookingSeat.Ticket.TicketStatus = BookingConstants.TicketStatus.Cancelled;
            }

            MarkShowtimeSeatUnavailable(bookingSeat.ShowtimeSeat);
        }

        if (successfulPayment.Refunds.Any(IsActiveRefund))
        {
            return RefundCreationResult.Ok(
                refundCreated: false,
                refundAmount: 0m,
                refundId: null);
        }

        var refund = new Refund
        {
            RefundId = NewId(BookingConstants.EntityIdPrefix.Refund),
            BookingId = booking.BookingId,
            PaymentId = successfulPayment.PaymentId,
            PaymentProviderId = successfulPayment.PaymentProviderId,
            ShowtimeCancellationId = cancellationId,
            RefundAmount = successfulPayment.Amount,
            RefundStatus = BookingConstants.RefundStatus.Pending,
            RefundReason = reason,
            RequestedAt = now
        };
        booking.Refunds.Add(refund);

        return RefundCreationResult.Ok(
            refundCreated: true,
            refundAmount: refund.RefundAmount,
            refundId: refund.RefundId);
    }

    private static void CancelUnpaidBooking(Booking booking, DateTime now)
    {
        booking.BookingStatus = BookingConstants.BookingStatus.Cancelled;

        foreach (var payment in booking.Payments)
        {
            if (IsStatus(payment.PaymentStatus, BookingConstants.PaymentStatus.Pending))
            {
                payment.PaymentStatus = BookingConstants.PaymentStatus.Cancelled;
                payment.UpdatedAt = now;
            }
        }

        foreach (var bookingSeat in booking.BookingSeats)
        {
            if (bookingSeat.Ticket is not null)
            {
                bookingSeat.Ticket.TicketStatus = BookingConstants.TicketStatus.Cancelled;
            }

            MarkShowtimeSeatUnavailable(bookingSeat.ShowtimeSeat);
        }
    }

    private static void MarkShowtimeSeatUnavailable(ShowtimeSeat showtimeSeat)
    {
        showtimeSeat.SeatStatus = BookingConstants.ShowtimeSeatStatus.Unavailable;
        showtimeSeat.LockedUntil = null;
        showtimeSeat.LockedByUserId = null;
    }

    private void AddCancellationNotification(Booking booking, Showtime showtime, DateTime now)
    {
        var userId = booking.CustomerProfile?.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        _dbContext.Notifications.Add(new Notification
        {
            NotificationId = NewId(BookingConstants.EntityIdPrefix.Notification),
            UserId = userId,
            BookingId = booking.BookingId,
            Title = "Showtime cancelled",
            Message = $"Showtime {showtime.Movie.Title} at {showtime.StartTime:O} has been cancelled. Refund processing status: {booking.BookingStatus}.",
            IsRead = false,
            CreatedAt = now
        });
    }

    private static void AddCancellationEmail(
        ICollection<CancellationEmail> emails,
        Booking booking,
        Showtime showtime)
    {
        var email = booking.CustomerProfile?.User.Email ?? booking.GuestEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        emails.Add(new CancellationEmail(
            email,
            "Showtime cancelled",
            $"Showtime {showtime.Movie.Title} at {showtime.StartTime:O} has been cancelled. Booking status: {booking.BookingStatus}."));
    }

    private void AddPaidCancellationEmail(
        ICollection<CancellationEmail> emails,
        Booking booking,
        Showtime showtime,
        decimal amount,
        string rawToken,
        DateTime expiresAt)
    {
        var email = booking.CustomerProfile?.User.Email ?? booking.GuestEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var link = $"{_refundSettings.FrontendBaseUrl.TrimEnd('/')}"
            + $"{RefundSettings.ClaimRoute}?t={Uri.EscapeDataString(rawToken)}";
        emails.Add(new CancellationEmail(
            email,
            "Showtime cancelled - refund information required",
            $"Showtime {showtime.Movie.Title} at {showtime.StartTime:O} was cancelled. "
            + $"Expected refund: {amount:N0}. Submit bank information before {expiresAt:O}: {link}"));
    }

    private async Task SendCancellationEmailsAsync(
        IEnumerable<CancellationEmail> emails,
        CancellationToken cancellationToken)
    {
        foreach (var email in emails)
        {
            try
            {
                await _emailSender.SendEmailAsync(
                    email.ToEmail,
                    email.Subject,
                    email.Body,
                    cancellationToken);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Cancellation email could not be sent to {Email}.",
                    email.ToEmail);
            }
        }
    }

    private static AuditLog CreateAuditLog(
        string userId,
        string showtimeId,
        string oldStatus,
        string cancellationId,
        string reason,
        int paidBookingsMoved,
        int unpaidBookingsCancelled,
        int refundsCreated,
        decimal totalRefundAmount,
        DateTime now)
    {
        return new AuditLog
        {
            AuditLogId = NewId(BookingConstants.EntityIdPrefix.AuditLog),
            UserId = userId,
            Action = DomainConstants.AuditAction.CancelShowtime,
            EntityName = DomainConstants.AuditEntity.Showtime,
            EntityId = showtimeId,
            OldValue = JsonSerializer.Serialize(new { status = oldStatus }),
            NewValue = JsonSerializer.Serialize(new
            {
                status = BookingConstants.ShowtimeStatus.Cancelled,
                cancellationId,
                reason,
                paidBookingsMoved,
                unpaidBookingsCancelled,
                refundsCreated,
                totalRefundAmount
            }),
            CreatedAt = now
        };
    }

    private static bool IsActiveRefund(Refund refund)
    {
        return IsStatus(refund.RefundStatus, BookingConstants.RefundStatus.Pending)
            || IsStatus(refund.RefundStatus, BookingConstants.RefundStatus.Success)
            || IsStatus(refund.RefundStatus, BookingConstants.RefundStatus.ManualRequired);
    }

    private static bool IsStatus(string? actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 };
    }

    private static ServiceResult<CancelShowtimeResponse> Fail(
        int statusCode,
        string message,
        string errorCode)
    {
        return ServiceResult<CancelShowtimeResponse>.Fail(statusCode, message, errorCode);
    }

    private static async Task<ServiceResult<CancelShowtimeResponse>> RollbackAndFailAsync(
        IDbContextTransaction transaction,
        int statusCode,
        string message,
        string errorCode,
        CancellationToken cancellationToken)
    {
        await transaction.RollbackAsync(cancellationToken);
        return Fail(statusCode, message, errorCode);
    }

    private static async Task RollbackSafelyAsync(IDbContextTransaction transaction)
    {
        try
        {
            await transaction.RollbackAsync(CancellationToken.None);
        }
        catch
        {
            // Preserve the original exception.
        }
    }

    private static string NewId(string prefix)
    {
        return $"{prefix}_{Guid.NewGuid():N}";
    }

    private sealed record RefundCreationResult(
        bool Success,
        bool RefundCreated,
        decimal RefundAmount,
        string? RefundId,
        string Message,
        string ErrorCode)
    {
        public static RefundCreationResult Ok(
            bool refundCreated,
            decimal refundAmount,
            string? refundId)
        {
            return new RefundCreationResult(
                true,
                refundCreated,
                refundAmount,
                refundId,
                string.Empty,
                string.Empty);
        }

        public static RefundCreationResult Fail(string message, string errorCode)
        {
            return new RefundCreationResult(
                false,
                false,
                0m,
                null,
                message,
                errorCode);
        }
    }

    private sealed record CancellationEmail(
        string ToEmail,
        string Subject,
        string Body);

}
