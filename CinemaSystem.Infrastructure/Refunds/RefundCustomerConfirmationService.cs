using System.Globalization;
using System.Security.Cryptography;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Refunds;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace CinemaSystem.Infrastructure.Refunds;

public sealed class RefundCustomerConfirmationService : IRefundCustomerConfirmationService
{
    private readonly CinemaDbContext _db;
    private readonly IRefundClaimIssuer _tokenIssuer;
    private readonly ISensitiveDataProtector _protector;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly RefundSettings _settings;
    private readonly EmailTemplatesSettings _emailTemplates;
    private readonly ILogger<RefundCustomerConfirmationService> _logger;

    public RefundCustomerConfirmationService(
        CinemaDbContext db,
        IRefundClaimIssuer tokenIssuer,
        ISensitiveDataProtector protector,
        IEmailSender emailSender,
        IClock clock,
        IOptions<RefundSettings> settings,
        IOptions<EmailTemplatesSettings> emailTemplates,
        ILogger<RefundCustomerConfirmationService> logger)
    {
        _db = db;
        _tokenIssuer = tokenIssuer;
        _protector = protector;
        _emailSender = emailSender;
        _clock = clock;
        _settings = settings.Value;
        _emailTemplates = emailTemplates.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<RefundCustomerConfirmationResponse>> PreviewAsync(
        string customerUserId,
        string token,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return Fail(HttpStatusCode.BadRequest, "Refund confirmation token is required.", "REFUND_CONFIRMATION_TOKEN_REQUIRED");
        }

        var confirmation = await LoadConfirmationAsync(_tokenIssuer.HashToken(token.Trim()), cancellationToken);
        if (confirmation is null)
        {
            return Fail(HttpStatusCode.NotFound, "Refund confirmation link was not found.", "REFUND_CONFIRMATION_NOT_FOUND");
        }
        if (!string.Equals(confirmation.ManualRefundProcess.RefundClaim.CustomerProfile.UserId, customerUserId, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(HttpStatusCode.Forbidden, "Refund confirmation does not belong to this customer.", "REFUND_CONFIRMATION_FORBIDDEN");
        }
        if (confirmation.ExpiresAt <= _clock.UtcNow)
        {
            return Fail(HttpStatusCode.Gone, "Refund confirmation link has expired.", "REFUND_CONFIRMATION_EXPIRED");
        }

        return ServiceResult<RefundCustomerConfirmationResponse>.Ok(
            ToResponse(confirmation.ManualRefundProcess, confirmation),
            "Refund confirmation details retrieved.");
    }

    public async Task<ServiceResult<RefundCustomerConfirmationResponse>> SendAsync(
        string refundId,
        string adminUserId,
        CancellationToken cancellationToken)
    {
        var process = await LoadProcessAsync(refundId, cancellationToken);
        if (process is null)
        {
            return Fail(HttpStatusCode.NotFound, "Manual refund was not found.", "MANUAL_REFUND_NOT_FOUND");
        }
        if (!string.Equals(process.AssignedToUserId, adminUserId, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(HttpStatusCode.Conflict, "Assign this refund before requesting customer confirmation.", "MANUAL_REFUND_NOT_ASSIGNED_TO_USER");
        }
        if (process.Refund.RefundStatus != BookingConstants.RefundStatus.ManualRequired)
        {
            return Fail(HttpStatusCode.Conflict, "Refund is not awaiting manual processing.", "REFUND_NOT_MANUAL_REQUIRED");
        }

        var now = _clock.UtcNow;
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var expiresAt = now.AddHours(Math.Max(1, _settings.CustomerConfirmationHours));
        var confirmation = process.CustomerConfirmation;
        if (confirmation is null)
        {
            confirmation = new RefundCustomerConfirmation
            {
                RefundCustomerConfirmationId = NewId(BookingConstants.EntityIdPrefix.RefundCustomerConfirmation),
                ManualRefundProcessId = process.ManualRefundProcessId,
                CreatedAt = now
            };
            process.CustomerConfirmation = confirmation;
        }
        else if (confirmation.Status == BookingConstants.RefundCustomerConfirmationStatus.ConfirmedByCustomer)
        {
            return Fail(HttpStatusCode.Conflict, "Customer has already confirmed this refund.", "REFUND_ALREADY_CONFIRMED_BY_CUSTOMER");
        }

        confirmation.TokenHash = _tokenIssuer.HashToken(rawToken);
        confirmation.Status = BookingConstants.RefundCustomerConfirmationStatus.AwaitingCustomer;
        confirmation.ExpiresAt = expiresAt;
        confirmation.ConfirmedAt = null;
        confirmation.RevokedAt = null;

        _db.AuditLogs.Add(new AuditLog
        {
            AuditLogId = NewId(BookingConstants.EntityIdPrefix.AuditLog),
            UserId = adminUserId,
            Action = "REQUEST_REFUND_CUSTOMER_CONFIRMATION",
            EntityName = DomainConstants.AuditEntity.Refund,
            EntityId = process.RefundId,
            CreatedAt = now
        });
        await _db.SaveChangesAsync(cancellationToken);

        var email = process.Refund.Booking.CustomerProfile?.User.Email ?? process.Refund.Booking.GuestEmail;
        if (!string.IsNullOrWhiteSpace(email))
        {
            await TrySendEmailAsync(email, process, rawToken, expiresAt, cancellationToken);
        }
        return ServiceResult<RefundCustomerConfirmationResponse>.Ok(ToResponse(process, confirmation), "Customer confirmation request sent.");
    }

    public async Task<ServiceResult<RefundCustomerConfirmationResponse>> ConfirmAsync(
        string customerUserId,
        ConfirmRefundByCustomerRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Fail(HttpStatusCode.BadRequest, "Refund confirmation token is required.", "REFUND_CONFIRMATION_TOKEN_REQUIRED");
        }

        var tokenHash = _tokenIssuer.HashToken(request.Token.Trim());
        var confirmation = await LoadConfirmationAsync(tokenHash, cancellationToken);
        if (confirmation is null)
        {
            return Fail(HttpStatusCode.NotFound, "Refund confirmation link was not found.", "REFUND_CONFIRMATION_NOT_FOUND");
        }
        if (!string.Equals(confirmation.ManualRefundProcess.RefundClaim.CustomerProfile.UserId, customerUserId, StringComparison.OrdinalIgnoreCase))
        {
            return Fail(HttpStatusCode.Forbidden, "Refund confirmation does not belong to this customer.", "REFUND_CONFIRMATION_FORBIDDEN");
        }
        var now = _clock.UtcNow;
        if (confirmation.Status != BookingConstants.RefundCustomerConfirmationStatus.AwaitingCustomer)
        {
            return Fail(HttpStatusCode.Conflict, "Refund confirmation is no longer pending.", "REFUND_CONFIRMATION_NOT_PENDING");
        }
        if (confirmation.ExpiresAt <= now)
        {
            confirmation.Status = BookingConstants.RefundCustomerConfirmationStatus.Expired;
            await _db.SaveChangesAsync(cancellationToken);
            return Fail(HttpStatusCode.Gone, "Refund confirmation link has expired.", "REFUND_CONFIRMATION_EXPIRED");
        }

        confirmation.Status = BookingConstants.RefundCustomerConfirmationStatus.ConfirmedByCustomer;
        confirmation.ConfirmedAt = now;
        var assignedAdmin = confirmation.ManualRefundProcess.AssignedToUserId;
        if (!string.IsNullOrWhiteSpace(assignedAdmin))
        {
            _db.Notifications.Add(new Notification
            {
                NotificationId = NewId(BookingConstants.EntityIdPrefix.Notification),
                UserId = assignedAdmin,
                BookingId = confirmation.ManualRefundProcess.Refund.BookingId,
                Title = "Refund confirmed by customer",
                Message = $"Customer confirmed refund {confirmation.ManualRefundProcess.RefundId}.",
                IsRead = false,
                CreatedAt = now
            });
        }
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<RefundCustomerConfirmationResponse>.Ok(
            ToResponse(confirmation.ManualRefundProcess, confirmation),
            "Refund details confirmed by customer.");
    }

    private async Task<ManualRefundProcess?> LoadProcessAsync(string refundId, CancellationToken cancellationToken)
        => await _db.ManualRefundProcesses
            .Include(item => item.CustomerConfirmation)
            .Include(item => item.RefundClaim)
                .ThenInclude(item => item.Bank)
            .Include(item => item.Refund)
                .ThenInclude(item => item.Booking)
                    .ThenInclude(item => item.CustomerProfile)
                        .ThenInclude(item => item!.User)
            .Include(item => item.Refund)
                .ThenInclude(item => item.Booking)
                    .ThenInclude(item => item.Showtime)
                        .ThenInclude(item => item.Movie)
            .Include(item => item.Refund)
                .ThenInclude(item => item.Booking)
                    .ThenInclude(item => item.Showtime)
                        .ThenInclude(item => item.Room)
                            .ThenInclude(item => item.Cinema)
            .Include(item => item.Refund)
                .ThenInclude(item => item.Booking)
                    .ThenInclude(item => item.BookingSeats)
                        .ThenInclude(item => item.ShowtimeSeat)
                            .ThenInclude(item => item.Seat)
            .FirstOrDefaultAsync(item => item.RefundId == refundId, cancellationToken);

    private async Task<RefundCustomerConfirmation?> LoadConfirmationAsync(
        string tokenHash,
        CancellationToken cancellationToken)
        => await _db.RefundCustomerConfirmations
            .Include(item => item.ManualRefundProcess)
                .ThenInclude(item => item.RefundClaim)
                    .ThenInclude(item => item.CustomerProfile)
            .Include(item => item.ManualRefundProcess)
                .ThenInclude(item => item.RefundClaim)
                    .ThenInclude(item => item.Bank)
            .Include(item => item.ManualRefundProcess)
                .ThenInclude(item => item.Refund)
                    .ThenInclude(item => item.Booking)
                        .ThenInclude(item => item.Showtime)
                            .ThenInclude(item => item.Movie)
            .Include(item => item.ManualRefundProcess)
                .ThenInclude(item => item.Refund)
                    .ThenInclude(item => item.Booking)
                        .ThenInclude(item => item.Showtime)
                            .ThenInclude(item => item.Room)
                                .ThenInclude(item => item.Cinema)
            .Include(item => item.ManualRefundProcess)
                .ThenInclude(item => item.Refund)
                    .ThenInclude(item => item.Booking)
                        .ThenInclude(item => item.BookingSeats)
                            .ThenInclude(item => item.ShowtimeSeat)
                                .ThenInclude(item => item.Seat)
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

    private async Task TrySendEmailAsync(string email, ManualRefundProcess process, string token, DateTime expiresAt, CancellationToken cancellationToken)
    {
        try
        {
            var account = _protector.Unprotect(process.RefundClaim.BankAccountEncrypted!);
            var accountHolderName = _protector.Unprotect(process.RefundClaim.AccountHolderNameEncrypted!);
            var booking = process.Refund.Booking;
            var showtime = booking.Showtime;
            var seatCodes = string.Join(", ", booking.BookingSeats
                .Select(item => item.ShowtimeSeat.Seat.SeatCode)
                .OrderBy(item => item, StringComparer.Ordinal));
            var link = $"{_settings.FrontendBaseUrl.TrimEnd('/')}{RefundSettings.CustomerConfirmationRoute}?t={Uri.EscapeDataString(token)}";
            await _emailSender.SendEmailAsync(email, _emailTemplates.RefundCustomerConfirmationSubject,
                string.Format(CultureInfo.InvariantCulture, _emailTemplates.RefundCustomerConfirmationBody,
                    process.Refund.RefundAmount,
                    showtime.Movie.Title,
                    showtime.Room.Cinema.CinemaName,
                    showtime.Room.RoomName,
                    showtime.StartTime,
                    seatCodes,
                    process.RefundClaim.Bank?.FullName ?? process.RefundClaim.BankCode,
                    account,
                    accountHolderName,
                    expiresAt,
                    link), cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Refund customer-confirmation email could not be sent for refund {RefundId}.", process.RefundId);
        }
    }

    private RefundCustomerConfirmationResponse ToResponse(ManualRefundProcess process, RefundCustomerConfirmation confirmation)
    {
        var booking = process.Refund.Booking;
        var showtime = booking.Showtime;
        return new RefundCustomerConfirmationResponse
        {
            RefundId = process.RefundId,
            ManualRefundProcessId = process.ManualRefundProcessId,
            Status = confirmation.Status,
            ExpiresAt = confirmation.ExpiresAt,
            ConfirmedAt = confirmation.ConfirmedAt,
            BookingId = booking.BookingId,
            RefundAmount = process.Refund.RefundAmount,
            MovieTitle = showtime.Movie.Title,
            CinemaName = showtime.Room.Cinema.CinemaName,
            RoomName = showtime.Room.RoomName,
            ShowtimeStartTime = showtime.StartTime,
            SeatCodes = booking.BookingSeats
                .Select(item => item.ShowtimeSeat.Seat.SeatCode)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToList(),
            BankCode = process.RefundClaim.BankCode,
            BankName = process.RefundClaim.Bank?.FullName,
            BankAccountNumber = process.RefundClaim.BankAccountEncrypted is null
                ? null
                : _protector.Unprotect(process.RefundClaim.BankAccountEncrypted),
            AccountHolderName = process.RefundClaim.AccountHolderNameEncrypted is null
                ? null
                : _protector.Unprotect(process.RefundClaim.AccountHolderNameEncrypted)
        };
    }

    private static ServiceResult<RefundCustomerConfirmationResponse> Fail(HttpStatusCode status, string message, string code)
        => ServiceResult<RefundCustomerConfirmationResponse>.Fail((int)status, message, code);

    private static string NewId(string prefix) => CinemaSystem.Domain.Utilities.IdGenerator.NewId(prefix);
}
