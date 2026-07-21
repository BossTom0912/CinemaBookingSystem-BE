using System.Data;
using System.Globalization;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Refunds;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Configuration;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace CinemaSystem.Infrastructure.Showtimes;

public sealed class ShowtimeCancellationService : IShowtimeCancellationService
{
    private readonly CinemaDbContext _dbContext;
    private readonly ICancellationCompensationService _compensationService;
    private readonly IVoucherReservationService _voucherReservationService;
    private readonly IEmailSender _emailSender;
    private readonly IRefundClaimIssuer _refundClaimIssuer;
    private readonly RefundSettings _refundSettings;
    private readonly IClock _clock;
    private readonly EmailTemplatesSettings _emailTemplates;
    private readonly ILogger<ShowtimeCancellationService> _logger;

    public ShowtimeCancellationService(
        CinemaDbContext dbContext,
        ICancellationCompensationService compensationService,
        IVoucherReservationService voucherReservationService,
        IEmailSender emailSender,
        IRefundClaimIssuer refundClaimIssuer,
        IClock clock,
        IOptions<RefundSettings> refundSettings,
        IOptions<EmailTemplatesSettings> emailTemplates,
        ILogger<ShowtimeCancellationService> logger)
    {
        _dbContext = dbContext;
        _compensationService = compensationService;
        _voucherReservationService = voucherReservationService;
        _emailSender = emailSender;
        _refundClaimIssuer = refundClaimIssuer;
        _clock = clock;
        _refundSettings = refundSettings.Value;
        _emailTemplates = emailTemplates.Value;
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
            return Fail(
                (int)HttpStatusCode.BadRequest,
                "Showtime ID is required.",
                BookingConstants.RefundErrorCodes.ShowtimeIdRequired);
        }

        if (string.IsNullOrWhiteSpace(userId))
        {
            return Fail(
                (int)HttpStatusCode.Unauthorized,
                "User is required.",
                BookingConstants.RefundErrorCodes.UserRequired);
        }

        var reason = request.Reason.Trim();
        if (string.IsNullOrWhiteSpace(reason))
        {
            return Fail(
                (int)HttpStatusCode.BadRequest,
                "Cancellation reason is required.",
                BookingConstants.RefundErrorCodes.CancellationReasonRequired);
        }

        if (reason.Length > RefundContractConstants.CancellationReasonMaxLength)
        {
            return Fail(
                (int)HttpStatusCode.BadRequest,
                $"Cancellation reason must not exceed {RefundContractConstants.CancellationReasonMaxLength} characters.",
                BookingConstants.RefundErrorCodes.CancellationReasonTooLong);
        }

        var actorExists = await _dbContext.Users
            .AsNoTracking()
            .AnyAsync(item => item.UserId == userId, cancellationToken);
        if (!actorExists)
        {
            return Fail(
                (int)HttpStatusCode.Unauthorized,
                "User was not found.",
                BookingConstants.RefundErrorCodes.UserNotFound);
        }

        var executionStrategy = _dbContext.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
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
                        (int)HttpStatusCode.NotFound,
                        "Showtime was not found.",
                        BookingConstants.RefundErrorCodes.ShowtimeNotFound,
                        cancellationToken);
                }

                if (IsStatus(showtime.Status, BookingConstants.ShowtimeStatus.Cancelled)
                    || showtime.ShowtimeCancellation is not null)
                {
                    return await RollbackAndFailAsync(
                        transaction,
                        (int)HttpStatusCode.Conflict,
                        "Showtime has already been cancelled.",
                        BookingConstants.RefundErrorCodes.ShowtimeAlreadyCancelled,
                        cancellationToken);
                }

                var now = _clock.UtcNow;
                if (showtime.StartTime <= now)
                {
                    return await RollbackAndFailAsync(
                        transaction,
                        (int)HttpStatusCode.Conflict,
                        "A showtime that has already started cannot be cancelled.",
                        BookingConstants.RefundErrorCodes.ShowtimeAlreadyStarted,
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
                var paidBookingsCompensated = 0;
                var refundsCreated = 0;
                var totalRefundAmount = 0m;
                var ticketVouchersIssued = 0;
                var comboVouchersIssued = 0;
                var cancellationEmails = new List<CancellationEmail>();

                foreach (var showtimeSeat in showtime.ShowtimeSeats)
                {
                    MarkShowtimeSeatUnavailable(showtimeSeat);
                }

                foreach (var booking in showtime.Bookings)
                {
                    if (IsStatus(booking.BookingStatus, BookingConstants.BookingStatus.Paid))
                    {
                        var hasSuccessfulPayment = booking.Payments.Any(item =>
                            IsStatus(
                                item.PaymentStatus,
                                BookingConstants.PaymentStatus.Success));
                        // A 100% standard voucher or compensation ticket can settle a booking
                        // immediately without a payment-gateway transaction. It is still a paid
                        // booking for the cancellation-compensation policy.
                        var isZeroAmountSettledBooking = booking.TotalAmount == 0m;
                        if (!hasSuccessfulPayment && !isZeroAmountSettledBooking)
                        {
                            return await RollbackAndFailAsync(
                                transaction,
                                (int)HttpStatusCode.Conflict,
                                $"Paid booking {booking.BookingId} has no successful payment.",
                                BookingConstants.RefundErrorCodes.PaidBookingPaymentNotFound,
                                cancellationToken);
                        }

                        await CancelPaidBookingAndRestoreVouchersAsync(
                            booking,
                            now,
                            cancellationToken);
                        var issue = await _compensationService
                            .IssueForCancelledBookingAsync(
                                booking,
                                cancellationId,
                                now,
                                cancellationToken);
                        var claimIssue = CreateRefundClaimForCancelledBooking(
                            booking,
                            cancellationId,
                            now);
                        if (claimIssue is not null)
                        {
                            refundsCreated++;
                            totalRefundAmount += booking.TotalAmount;
                        }

                        paidBookingsMoved++;
                        paidBookingsCompensated++;
                        ticketVouchersIssued += issue.AlreadyIssued
                            ? 0
                            : issue.TicketVouchersIssued;
                        comboVouchersIssued += issue.AlreadyIssued
                            ? 0
                            : issue.ComboVouchersIssued;
                        AddPaidCancellationEmail(
                            cancellationEmails,
                            booking,
                            showtime,
                            issue,
                            claimIssue);

                        AddCancellationNotification(booking, showtime, now);
                        continue;
                    }

                    if (IsStatus(booking.BookingStatus, BookingConstants.BookingStatus.Created)
                        || IsStatus(booking.BookingStatus, BookingConstants.BookingStatus.PendingPayment))
                    {
                        CancelUnpaidBooking(booking, now);
                        await _compensationService.ReleaseBookingReservationsAsync(
                            booking.BookingId,
                            cancellationToken);
                        if (booking.VoucherUsage is not null)
                        {
                            await _voucherReservationService.CancelAsync(
                                booking.VoucherUsage,
                                cancellationToken);
                        }
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
                    paidBookingsCompensated,
                    ticketVouchersIssued,
                    comboVouchersIssued,
                    now));

                await _dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                // Cancellation is committed before SMTP or payment-provider work. A failure
                // outside the database must never reopen a showtime or allow new ticket sales.
                await SendCancellationEmailsAsync(cancellationEmails, cancellationToken);

                _logger.LogInformation(
                    "Showtime {ShowtimeId} cancelled by user {UserId}; paid bookings compensated: {BookingCount}, ticket vouchers: {TicketCount}, combo vouchers: {ComboCount}.",
                    showtime.ShowtimeId,
                    userId,
                    paidBookingsCompensated,
                    ticketVouchersIssued,
                    comboVouchersIssued);

                return ServiceResult<CancelShowtimeResponse>.Ok(
                    new CancelShowtimeResponse
                    {
                        ShowtimeId = showtime.ShowtimeId,
                        ShowtimeStatus = showtime.Status,
                        ShowtimeCancellationId = cancellationId,
                        PaidBookingsMovedToRefundPending = refundsCreated,
                        UnpaidBookingsCancelled = unpaidBookingsCancelled,
                        RefundsCreated = refundsCreated,
                        TotalRefundAmount = totalRefundAmount,
                        RefundsSucceeded = 0,
                        RefundsManualRequired = 0,
                        RefundsPending = refundsCreated,
                        PaidBookingsCompensated = paidBookingsCompensated,
                        TicketVouchersIssued = ticketVouchersIssued,
                        ComboVouchersIssued = comboVouchersIssued
                    },
                    "Showtime cancelled and compensation vouchers issued successfully.");
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
                        (int)HttpStatusCode.Conflict,
                        "Showtime has already been cancelled.",
                        BookingConstants.RefundErrorCodes.ShowtimeAlreadyCancelled);
                }

                throw;
            }
            catch
            {
                await RollbackSafelyAsync(transaction);
                throw;
            }
        });
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
            .Include(item => item.Bookings)
                .ThenInclude(item => item.VoucherUsage)
                    .ThenInclude(item => item!.Voucher)
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

    private async Task CancelPaidBookingAndRestoreVouchersAsync(
        Booking booking,
        DateTime now,
        CancellationToken cancellationToken)
    {
        booking.BookingStatus = BookingConstants.BookingStatus.Cancelled;

        foreach (var bookingSeat in booking.BookingSeats)
        {
            if (bookingSeat.Ticket is not null
                && !IsStatus(bookingSeat.Ticket.TicketStatus, BookingConstants.TicketStatus.Refunded))
            {
                bookingSeat.Ticket.TicketStatus = BookingConstants.TicketStatus.Cancelled;
            }

            MarkShowtimeSeatUnavailable(bookingSeat.ShowtimeSeat);
        }

        await _compensationService.RestoreBookingEntitlementsAsync(
            booking.BookingId,
            cancellationToken);

        if (booking.VoucherUsage is not null)
        {
            var wasConfirmed = await _voucherReservationService.CancelAsync(
                booking.VoucherUsage,
                cancellationToken);
            if (wasConfirmed && booking.VoucherUsage.Voucher is not null)
            {
                booking.VoucherUsage.Voucher.UsedCount = Math.Max(
                    0,
                    booking.VoucherUsage.Voucher.UsedCount - 1);
            }
        }
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
            Message = $"Showtime {showtime.Movie.Title} at {showtime.StartTime:O} has been cancelled. Compensation ticket vouchers and one combo voucher were issued for 180 days.",
            IsRead = false,
            CreatedAt = now
        });
    }

    private void AddCancellationEmail(
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
            _emailTemplates.ShowtimeCancelledNoRefundSubject,
            string.Format(
                CultureInfo.InvariantCulture,
                _emailTemplates.ShowtimeCancelledNoRefundBody,
                showtime.Movie.Title,
                showtime.StartTime,
                booking.BookingStatus)));
    }

    private void AddPaidCancellationEmail(
        ICollection<CancellationEmail> emails,
        Booking booking,
        Showtime showtime,
        CompensationIssueResult issue,
        RefundClaimIssue? claimIssue)
    {
        var email = booking.CustomerProfile?.User.Email ?? booking.GuestEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var compensationBody = string.Format(
            CultureInfo.InvariantCulture,
            _emailTemplates.ShowtimeCancelledCompensationBody,
            showtime.Movie.Title,
            showtime.StartTime,
            issue.TicketVouchersIssued,
            issue.ExpiresAt,
            string.Join(", ", issue.TicketVoucherCodes),
            issue.ComboVoucherCode ?? "N/A");
        if (claimIssue is null)
        {
            emails.Add(new CancellationEmail(email, _emailTemplates.ShowtimeCancelledCompensationSubject, compensationBody));
            return;
        }

        var link = $"{_refundSettings.FrontendBaseUrl.TrimEnd('/')}{RefundSettings.ClaimRoute}?t={Uri.EscapeDataString(claimIssue.RawToken)}";
        var refundBody = string.Format(
            CultureInfo.InvariantCulture,
            _emailTemplates.ShowtimeCancelledRefundBody,
            showtime.Movie.Title,
            showtime.StartTime,
            booking.TotalAmount,
            claimIssue.Token.ExpiresAt,
            link);
        emails.Add(new CancellationEmail(
            email,
            _emailTemplates.ShowtimeCancelledRefundSubject,
            $"{refundBody}{Environment.NewLine}{Environment.NewLine}{compensationBody}"));
    }

    private RefundClaimIssue? CreateRefundClaimForCancelledBooking(
        Booking booking,
        string cancellationId,
        DateTime now)
    {
        if (booking.TotalAmount == 0m || string.IsNullOrWhiteSpace(booking.CustomerProfileId))
        {
            return null;
        }

        var payment = booking.Payments.FirstOrDefault(item =>
            IsStatus(item.PaymentStatus, BookingConstants.PaymentStatus.Success));
        if (payment is null)
        {
            return null;
        }

        var refund = new Refund
        {
            RefundId = NewId(BookingConstants.EntityIdPrefix.Refund),
            BookingId = booking.BookingId,
            PaymentId = payment.PaymentId,
            PaymentProviderId = payment.PaymentProviderId,
            ShowtimeCancellationId = cancellationId,
            RefundAmount = booking.TotalAmount,
            RefundStatus = BookingConstants.RefundStatus.Pending,
            RefundReason = "Showtime cancelled by cinema.",
            RequestedAt = now
        };
        _dbContext.Refunds.Add(refund);
        var issue = _refundClaimIssuer.Create(refund.RefundId, booking.CustomerProfileId, now);
        _dbContext.RefundClaims.Add(issue.Claim);
        return issue;
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
        int paidBookingsCompensated,
        int ticketVouchersIssued,
        int comboVouchersIssued,
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
                paidBookingsCompensated,
                ticketVouchersIssued,
                comboVouchersIssued
            }),
            CreatedAt = now
        };
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
        return CinemaSystem.Domain.Utilities.IdGenerator.NewId(prefix);
    }

    private sealed record CancellationEmail(
        string ToEmail,
        string Subject,
        string Body);

}
