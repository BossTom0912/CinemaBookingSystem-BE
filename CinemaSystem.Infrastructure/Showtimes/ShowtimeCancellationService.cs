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
using Npgsql;
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

                        var customVoucherCode = request.CompensationVoucher?.Trim()
                            ?? request.CompensationVoucherCode?.Trim();
                        var shouldIssueCompensation = !string.IsNullOrWhiteSpace(customVoucherCode);

                        CompensationIssueResult? issue = null;
                        if (shouldIssueCompensation)
                        {
                            issue = await _compensationService
                                .IssueForCancelledBookingAsync(
                                    booking,
                                    cancellationId,
                                    now,
                                    cancellationToken);
                        }

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
                        if (issue is not null)
                        {
                            paidBookingsCompensated++;
                            ticketVouchersIssued += issue.AlreadyIssued
                                ? 0
                                : issue.TicketVouchersIssued;
                            comboVouchersIssued += issue.AlreadyIssued
                                ? 0
                                : issue.ComboVouchersIssued;
                        }

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
                        AddUnpaidCancellationEmail(cancellationEmails, booking, showtime);
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

    private void AddUnpaidCancellationEmail(
        ICollection<CancellationEmail> emails,
        Booking booking,
        Showtime showtime)
    {
        var email = booking.CustomerProfile?.User.Email ?? booking.GuestEmail;
        if (string.IsNullOrWhiteSpace(email))
        {
            return;
        }

        var subject = $"[CinemaSystem] Thông báo hủy suất chiếu - Phim {showtime.Movie?.Title ?? ""}";
        var bodyHtml = BuildCancellationEmailHtml(booking, showtime, null, null);

        emails.Add(new CancellationEmail(email, subject, bodyHtml));
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

        var subject = $"[CinemaSystem] Thông báo hủy suất chiếu, hoàn tiền & phát hành Voucher đền bù";
        var bodyHtml = BuildCancellationEmailHtml(booking, showtime, issue, claimIssue);

        emails.Add(new CancellationEmail(email, subject, bodyHtml));
    }

    private string BuildCancellationEmailHtml(
        Booking booking,
        Showtime showtime,
        CompensationIssueResult? issue,
        RefundClaimIssue? claimIssue)
    {
        var displayName = string.IsNullOrWhiteSpace(booking.CustomerProfile?.User?.FullName)
            ? "Quý khách"
            : booking.CustomerProfile.User.FullName.Trim();
        var movieTitle = showtime.Movie?.Title ?? "Phim đã đặt";
        var showtimeFormatted = showtime.StartTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
        var totalAmountFormatted = booking.TotalAmount.ToString("N0", new CultureInfo("vi-VN")) + " VNĐ";

        var claimLink = claimIssue is not null
            ? $"{_refundSettings.FrontendBaseUrl.TrimEnd('/')}{RefundSettings.ClaimRoute}?t={Uri.EscapeDataString(claimIssue.RawToken)}"
            : $"{_refundSettings.FrontendBaseUrl.TrimEnd('/')}{RefundSettings.ClaimRoute}";
        var claimExpiresFormatted = claimIssue?.Token.ExpiresAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)
            ?? showtime.StartTime.AddDays(7).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        var ticketCodesStr = issue is not null && issue.TicketVoucherCodes.Any()
            ? string.Join(", ", issue.TicketVoucherCodes)
            : null;
        var comboCodeStr = issue?.ComboVoucherCode;
        var voucherExpiresFormatted = issue?.ExpiresAt.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

        var isRefundable = booking.TotalAmount > 0m;

        var voucherSectionHtml = issue is not null ? $"""
            <div style='background-color: #fffbe6; border: 1px solid #ffe58f; padding: 18px; border-radius: 10px; margin: 20px 0;'>
                <h3 style='margin: 0 0 10px 0; color: #b78103; font-size: 15px; font-weight: bold;'>QUYỀN LỢI & VOUCHER BỒI THƯỜNG DÀNH CHO BẠN</h3>
                <p style='font-size: 13px; color: #5c4300; margin: 0 0 10px 0;'>
                    CinemaSystem xin gửi tặng Quý khách các mã Voucher bồi thường sự cố (Tự động lưu vào ví tài khoản của Quý khách):
                </p>
                <ul style='margin: 0; padding-left: 20px; font-size: 13px; color: #433300;'>
                    {(ticketCodesStr != null ? $"<li style='margin-bottom: 6px;'><strong>Voucher Vé Xem Phim (100%):</strong> <span style='font-family: monospace; font-size: 14px; font-weight: bold; background-color: #fef08a; padding: 2px 8px; border-radius: 4px; border: 1px solid #fde047;'>{ticketCodesStr}</span> ({issue.TicketVouchersIssued} vé)</li>" : "")}
                    {(comboCodeStr != null ? $"<li style='margin-bottom: 6px;'><strong>Voucher Combo Bắp Nước:</strong> <span style='font-family: monospace; font-size: 14px; font-weight: bold; background-color: #fef08a; padding: 2px 8px; border-radius: 4px; border: 1px solid #fde047;'>{comboCodeStr}</span></li>" : "")}
                    {(voucherExpiresFormatted != null ? $"<li><strong>Hạn sử dụng Voucher:</strong> đến <strong>{voucherExpiresFormatted}</strong></li>" : "")}
                </ul>
            </div>
            """ : "";

        var refundSectionHtml = isRefundable ? $"""
            <div style='background-color: #eff6ff; border: 1px solid #bfdbfe; padding: 18px; border-radius: 10px; margin: 20px 0;'>
                <h3 style='margin: 0 0 10px 0; color: #1d4ed8; font-size: 15px; font-weight: bold;'>KHAI BÁO THÔNG TIN NHẬN LẠI TIỀN HOÀN</h3>
                <p style='font-size: 13px; color: #1e3a8a; margin: 0 0 12px 0;'>
                    Vui lòng bấm vào nút bên dưới để nhập thông tin tài khoản ngân hàng nhận lại <strong>{totalAmountFormatted}</strong> trước thời hạn <strong>{claimExpiresFormatted}</strong>:
                </p>
                <div style='text-align: center; margin: 15px 0;'>
                    <a href='{claimLink}' style='display: inline-block; background-color: #2563eb; color: #ffffff; font-weight: bold; text-decoration: none; padding: 12px 24px; border-radius: 8px; font-size: 14px; box-shadow: 0 3px 10px rgba(37,99,235,0.3);'>
                        Nhập Tài Khoản Ngân Hàng Nhận Tiền Hoàn
                    </a>
                </div>
                <p style='font-size: 11px; color: #64748b; margin: 0; text-align: center;'>
                    Hoặc truy cập đường dẫn: <a href='{claimLink}' style='color: #2563eb;'>{claimLink}</a>
                </p>
            </div>
            """ : "";

        var voucherSectionHtmlEn = issue is not null ? $"""
            <div style='background-color: #fffbe6; border: 1px solid #ffe58f; padding: 18px; border-radius: 10px; margin: 20px 0;'>
                <h3 style='margin: 0 0 10px 0; color: #b78103; font-size: 15px; font-weight: bold;'>COMPENSATION VOUCHERS FOR YOU</h3>
                <p style='font-size: 13px; color: #5c4300; margin: 0 0 10px 0;'>
                    CinemaSystem has issued the following compensation vouchers directly to your account wallet:
                </p>
                <ul style='margin: 0; padding-left: 20px; font-size: 13px; color: #433300;'>
                    {(ticketCodesStr != null ? $"<li style='margin-bottom: 6px;'><strong>Movie Ticket Voucher (100%):</strong> <span style='font-family: monospace; font-size: 14px; font-weight: bold; background-color: #fef08a; padding: 2px 8px; border-radius: 4px; border: 1px solid #fde047;'>{ticketCodesStr}</span> ({issue.TicketVouchersIssued} ticket(s))</li>" : "")}
                    {(comboCodeStr != null ? $"<li style='margin-bottom: 6px;'><strong>Food & Beverage Voucher:</strong> <span style='font-family: monospace; font-size: 14px; font-weight: bold; background-color: #fef08a; padding: 2px 8px; border-radius: 4px; border: 1px solid #fde047;'>{comboCodeStr}</span></li>" : "")}
                    {(voucherExpiresFormatted != null ? $"<li><strong>Voucher Expiry Date:</strong> until <strong>{voucherExpiresFormatted}</strong></li>" : "")}
                </ul>
            </div>
            """ : "";

        var refundSectionHtmlEn = isRefundable ? $"""
            <div style='background-color: #eff6ff; border: 1px solid #bfdbfe; padding: 18px; border-radius: 10px; margin: 20px 0;'>
                <h3 style='margin: 0 0 10px 0; color: #1d4ed8; font-size: 15px; font-weight: bold;'>SUBMIT BANK INFORMATION FOR REFUND</h3>
                <p style='font-size: 13px; color: #1e3a8a; margin: 0 0 12px 0;'>
                    Please click the button below to submit your bank account details to receive your <strong>{totalAmountFormatted}</strong> refund before <strong>{claimExpiresFormatted}</strong>:
                </p>
                <div style='text-align: center; margin: 15px 0;'>
                    <a href='{claimLink}' style='display: inline-block; background-color: #2563eb; color: #ffffff; font-weight: bold; text-decoration: none; padding: 12px 24px; border-radius: 8px; font-size: 14px; box-shadow: 0 3px 10px rgba(37,99,235,0.3);'>
                        Submit Bank Account Information
                    </a>
                </div>
                <p style='font-size: 11px; color: #64748b; margin: 0; text-align: center;'>
                    Or visit link: <a href='{claimLink}' style='color: #2563eb;'>{claimLink}</a>
                </p>
            </div>
            """ : "";

        return $"""
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            </head>
            <body style='font-family: Arial, Helvetica, sans-serif; line-height: 1.6; color: #1e293b; background-color: #f8fafc; margin: 0; padding: 20px;'>
                <div style='max-width: 650px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 15px rgba(0,0,0,0.08); border: 1px solid #e2e8f0;'>
                    
                    <!-- HEADER -->
                    <div style='background: linear-gradient(135deg, #0f172a 0%, #1e293b 100%); padding: 25px 30px; text-align: center; border-bottom: 3px solid #ef4444;'>
                        <h1 style='color: #ffffff; margin: 0; font-size: 22px; font-weight: bold; letter-spacing: 1px;'>CINEMASYSTEM</h1>
                        <p style='color: #fca5a5; margin: 4px 0 0 0; font-size: 13px;'>THÔNG BÁO HỦY SUẤT CHIẾU & ĐỀN BÙ</p>
                        <p style='color: #94a3b8; margin: 2px 0 0 0; font-size: 11px; text-transform: uppercase;'>Showtime Cancellation & Compensation Notice</p>
                    </div>

                    <!-- CONTENT -->
                    <div style='padding: 30px;'>
                        <!-- TIẾNG VIỆT -->
                        <div style='margin-bottom: 25px;'>
                            <p style='font-size: 15px; font-weight: bold; color: #0f172a; margin-top: 0;'>Kính gửi {displayName},</p>
                            <p style='font-size: 14px; color: #334155; margin-bottom: 15px;'>
                                Ban quản trị CinemaSystem rất tiếc phải thông báo rằng suất chiếu cho bộ phim <strong>{movieTitle}</strong> trong đơn hàng của Quý khách đã bị hủy bỏ do sự cố ngoài ý muốn.
                            </p>

                            <!-- BẢNG CHI TIẾT ĐƠN HÀNG (VI) -->
                            <div style='margin: 20px 0;'>
                                <table style='width: 100%; border-collapse: collapse; border: 1px solid #e2e8f0; font-size: 13px; text-align: left;'>
                                    <thead>
                                        <tr style='background-color: #f1f5f9; color: #0f172a;'>
                                            <th style='padding: 10px 14px; border-bottom: 2px solid #cbd5e1; width: 35%;'>Thông tin</th>
                                            <th style='padding: 10px 14px; border-bottom: 2px solid #cbd5e1;'>Chi tiết</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Mã đơn hàng</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-family: monospace; font-weight: bold;'>#{booking.BookingId}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Bộ phim</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #dc2626; font-weight: bold;'>{movieTitle}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Thời gian suất chiếu</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>{showtimeFormatted}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Số tiền hoàn trả</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #16a34a; font-weight: bold;'>{totalAmountFormatted}</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>

                            {voucherSectionHtml}
                            {refundSectionHtml}

                            <p style='font-size: 13px; color: #334155; margin-top: 20px;'>
                                CinemaSystem chân thành xin lỗi vì sự bất tiện này và hy vọng tiếp tục được phục vụ Quý khách trong những suất chiếu tiếp theo.<br><br>
                                Trân trọng,<br>
                                <strong>Ban Quản trị CinemaSystem</strong>
                            </p>
                        </div>

                        <!-- DIVIDER -->
                        <hr style='border: none; border-top: 1px dashed #cbd5e1; margin: 25px 0;' />

                        <!-- TIẾNG ANH -->
                        <div>
                            <p style='font-size: 14px; font-weight: bold; color: #64748b; margin-top: 0;'>Dear {displayName},</p>
                            <p style='font-size: 13px; color: #64748b; margin-bottom: 15px;'>
                                CinemaSystem sincerely regrets to inform you that your showtime for <strong>{movieTitle}</strong> at <strong>{showtimeFormatted}</strong> has been cancelled due to unforeseen circumstances.
                            </p>

                            <!-- BẢNG CHI TIẾT ĐƠN HÀNG (EN) -->
                            <div style='margin: 20px 0;'>
                                <table style='width: 100%; border-collapse: collapse; border: 1px solid #e2e8f0; font-size: 13px; text-align: left;'>
                                    <thead>
                                        <tr style='background-color: #f1f5f9; color: #0f172a;'>
                                            <th style='padding: 10px 14px; border-bottom: 2px solid #cbd5e1; width: 35%;'>Information</th>
                                            <th style='padding: 10px 14px; border-bottom: 2px solid #cbd5e1;'>Details</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Booking ID</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-family: monospace; font-weight: bold;'>#{booking.BookingId}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Movie Title</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #dc2626; font-weight: bold;'>{movieTitle}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Showtime</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>{showtimeFormatted}</td>
                                        </tr>
                                        <tr>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; font-weight: bold;'>Refund Amount</td>
                                            <td style='padding: 10px 14px; border-bottom: 1px solid #e2e8f0; color: #16a34a; font-weight: bold;'>{totalAmountFormatted}</td>
                                        </tr>
                                    </tbody>
                                </table>
                            </div>

                            {voucherSectionHtmlEn}
                            {refundSectionHtmlEn}

                            <p style='font-size: 12px; color: #64748b; margin-top: 20px;'>
                                CinemaSystem sincerely apologizes for any inconvenience caused and hopes to serve you again in future showtimes.<br><br>
                                Sincerely,<br>
                                <strong>CinemaSystem Management Team</strong>
                            </p>
                        </div>
                    </div>

                    <!-- FOOTER -->
                    <div style='background-color: #f1f5f9; padding: 20px 30px; border-top: 1px solid #e2e8f0; font-size: 12px; color: #64748b; text-align: center;'>
                        <p style='margin: 0 0 4px 0; font-weight: bold; color: #0f172a;'>Trung tâm Chăm sóc Khách hàng CinemaSystem</p>
                        <p style='margin: 0 0 4px 0;'>Hotline: <strong>1900 6868</strong> | Email: <strong>cskh@cinemasystem.vn</strong></p>
                        <p style='margin: 0;'>Website: <a href='https://cinemasystem.vn' style='color: #2563eb; text-decoration: none;'>cinemasystem.vn</a></p>
                    </div>
                </div>
            </body>
            </html>
            """;
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
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation
        };
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
