using System.Globalization;
using System.Text.Json;
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

public sealed class RefundClaimService : IRefundClaimService
{
    private readonly CinemaDbContext _db;
    private readonly IRefundClaimIssuer _issuer;
    private readonly ISensitiveDataProtector _protector;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly RefundSettings _settings;
    private readonly EmailTemplatesSettings _emailTemplates;
    private readonly ILogger<RefundClaimService> _logger;

    public RefundClaimService(
        CinemaDbContext db,
        IRefundClaimIssuer issuer,
        ISensitiveDataProtector protector,
        IEmailSender emailSender,
        IClock clock,
        IOptions<RefundSettings> settings,
        IOptions<EmailTemplatesSettings> emailTemplates,
        ILogger<RefundClaimService> logger)
    {
        _db = db;
        _issuer = issuer;
        _protector = protector;
        _emailSender = emailSender;
        _clock = clock;
        _settings = settings.Value;
        _emailTemplates = emailTemplates.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyList<BankResponse>>> GetBanksAsync(
        CancellationToken cancellationToken)
    {
        var banks = await _db.BankDirectories
            .AsNoTracking()
            .Where(item => item.IsActive)
            .OrderBy(item => item.ShortName)
            .Select(item => new BankResponse
            {
                BankCode = item.BankCode,
                BankBin = item.BankBin,
                ShortName = item.ShortName,
                FullName = item.FullName
            })
            .ToListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<BankResponse>>.Ok(banks);
    }

    public async Task<ServiceResult<RefundClaimResponse>> ResolveAsync(
        string userId,
        ResolveRefundClaimRequest request,
        CancellationToken cancellationToken)
    {
        var tokenHash = _issuer.HashToken(request.Token.Trim());
        var token = await _db.RefundClaimTokens
            .Include(item => item.RefundClaim)
                .ThenInclude(item => item.Refund)
                    .ThenInclude(item => item.Booking)
                        .ThenInclude(item => item.Showtime)
                            .ThenInclude(item => item.Movie)
            .Include(item => item.RefundClaim)
                .ThenInclude(item => item.Refund)
                    .ThenInclude(item => item.Booking)
                        .ThenInclude(item => item.Showtime)
                            .ThenInclude(item => item.Room)
                                .ThenInclude(item => item.Cinema)
            .Include(item => item.RefundClaim)
                .ThenInclude(item => item.Bank)
            .FirstOrDefaultAsync(item => item.TokenHash == tokenHash, cancellationToken);

        if (token is null)
        {
            return FailClaim(
                (int)HttpStatusCode.NotFound,
                "Refund claim link was not found.",
                BookingConstants.RefundErrorCodes.RefundClaimNotFound);
        }

        var ownsClaim = await _db.CustomerProfiles.AnyAsync(
            item => item.CustomerProfileId == token.RefundClaim.CustomerProfileId
                && item.UserId == userId,
            cancellationToken);
        if (!ownsClaim)
        {
            return FailClaim(
                (int)HttpStatusCode.Forbidden,
                "Refund claim does not belong to this customer.",
                BookingConstants.RefundErrorCodes.RefundClaimForbidden);
        }

        var now = _clock.UtcNow;
        if (token.RevokedAt.HasValue || token.UsedAt.HasValue)
        {
            return FailClaim(
                (int)HttpStatusCode.Conflict,
                "Refund claim link has already been used.",
                BookingConstants.RefundErrorCodes.RefundClaimTokenUsed);
        }
        if (token.ExpiresAt <= now)
        {
            return FailClaim(
                (int)HttpStatusCode.Gone,
                "Refund claim link has expired.",
                BookingConstants.RefundErrorCodes.RefundClaimExpired);
        }
        if (IsFinal(token.RefundClaim))
        {
            return FailClaim(
                (int)HttpStatusCode.Conflict,
                "Refund claim can no longer be edited.",
                BookingConstants.RefundErrorCodes.RefundClaimNotEditable);
        }

        token.UsedAt = now;
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<RefundClaimResponse>.Ok(ToResponse(token.RefundClaim), "Refund claim resolved.");
    }

    public async Task<ServiceResult<RefundClaimResponse>> SaveBankAccountAsync(
        string userId,
        string claimId,
        SaveRefundBankAccountRequest request,
        CancellationToken cancellationToken)
    {
        var claim = await LoadOwnedClaimAsync(userId, claimId, cancellationToken);
        if (claim is null)
        {
            return FailClaim(
                (int)HttpStatusCode.NotFound,
                "Refund claim was not found.",
                BookingConstants.RefundErrorCodes.RefundClaimNotFound);
        }
        if (IsFinal(claim))
        {
            return FailClaim(
                (int)HttpStatusCode.Conflict,
                "Bank information cannot be changed after submission.",
                BookingConstants.RefundErrorCodes.RefundClaimNotEditable);
        }

        var bankCode = request.BankCode.Trim().ToUpperInvariant();
        var bank = await _db.BankDirectories
            .FirstOrDefaultAsync(item => item.BankCode == bankCode && item.IsActive, cancellationToken);
        if (bank is null)
        {
            return FailClaim(
                (int)HttpStatusCode.BadRequest,
                "Bank code is not supported.",
                BookingConstants.RefundErrorCodes.BankNotSupported);
        }

        var accountNumber = request.AccountNumber.Trim();
        var holderName = NormalizeHolderName(request.AccountHolderName);
        claim.BankCode = bank.BankCode;
        claim.Bank = bank;
        claim.BankAccountEncrypted = _protector.Protect(accountNumber);
        claim.BankAccountLast4 = accountNumber[
            ^Math.Min(RefundContractConstants.BankAccountVisibleSuffixLength, accountNumber.Length)..];
        claim.AccountHolderNameEncrypted = _protector.Protect(holderName);
        claim.AccountValidationStatus = BookingConstants.AccountValidationStatus.Unavailable;
        claim.UpdatedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(cancellationToken);

        return ServiceResult<RefundClaimResponse>.Ok(
            ToResponse(claim, holderName),
            "Bank information saved. Review it before submitting.");
    }

    public async Task<ServiceResult<RefundClaimResponse>> SubmitAsync(
        string userId,
        string claimId,
        CancellationToken cancellationToken)
    {
        var claim = await LoadOwnedClaimAsync(userId, claimId, cancellationToken);
        if (claim is null)
        {
            return FailClaim(
                (int)HttpStatusCode.NotFound,
                "Refund claim was not found.",
                BookingConstants.RefundErrorCodes.RefundClaimNotFound);
        }
        if (claim.ClaimStatus == BookingConstants.RefundClaimStatus.ManualRequired)
        {
            return ServiceResult<RefundClaimResponse>.Ok(ToResponse(claim), "Refund claim was already submitted.");
        }
        if (IsFinal(claim))
        {
            return FailClaim(
                (int)HttpStatusCode.Conflict,
                "Refund claim can no longer be submitted.",
                BookingConstants.RefundErrorCodes.RefundClaimNotEditable);
        }
        if (claim.BankAccountEncrypted is null || claim.AccountHolderNameEncrypted is null || claim.BankCode is null)
        {
            return FailClaim(
                (int)HttpStatusCode.BadRequest,
                "Bank information is required.",
                BookingConstants.RefundErrorCodes.BankAccountRequired);
        }

        var now = _clock.UtcNow;
        claim.ClaimStatus = BookingConstants.RefundClaimStatus.ManualRequired;
        claim.SubmittedAt = now;
        claim.UpdatedAt = now;
        claim.Refund.RefundStatus = BookingConstants.RefundStatus.ManualRequired;
        claim.Refund.FailureReason = null;
        if (claim.ManualRefundProcess is null)
        {
            claim.ManualRefundProcess = new ManualRefundProcess
            {
                ManualRefundProcessId = NewId(BookingConstants.EntityIdPrefix.ManualRefundProcess),
                RefundId = claim.RefundId,
                RefundClaimId = claim.RefundClaimId,
                ProcessStatus = BookingConstants.ManualRefundProcessStatus.Open,
                CreatedAt = now
            };
        }
        RevokeActiveTokens(claim, now);
        AddAudit(
            userId,
            DomainConstants.AuditAction.SubmitRefundClaim,
            DomainConstants.AuditEntity.RefundClaim,
            claim.RefundClaimId,
            now);
        await _db.SaveChangesAsync(cancellationToken);
        return ServiceResult<RefundClaimResponse>.Ok(ToResponse(claim), "Refund information submitted for manual processing.");
    }

    public async Task<ServiceResult<object>> RequestNewLinkAsync(
        string userId,
        RequestRefundLinkRequest request,
        CancellationToken cancellationToken)
    {
        var bookingId = request.BookingId.Trim();
        var refund = await _db.Refunds
            .Include(item => item.RefundClaim)
                .ThenInclude(item => item!.Tokens)
            .Include(item => item.Booking)
                .ThenInclude(item => item.CustomerProfile)
                    .ThenInclude(item => item!.User)
            .Include(item => item.Booking)
                .ThenInclude(item => item.BookingSeats)
                    .ThenInclude(item => item.Ticket)
            .Include(item => item.Booking)
                .ThenInclude(item => item.Showtime)
                    .ThenInclude(item => item.Movie)
            .FirstOrDefaultAsync(
                item => item.BookingId == bookingId
                    && item.Booking.CustomerProfile != null
                    && item.Booking.CustomerProfile.UserId == userId,
                cancellationToken);
        if (refund is null)
        {
            return ServiceResult<object>.Fail(
                (int)HttpStatusCode.NotFound,
                "Refund was not found.",
                BookingConstants.RefundErrorCodes.RefundNotFound);
        }
        if (refund.RefundStatus == BookingConstants.RefundStatus.Success)
        {
            return ServiceResult<object>.Fail(
                (int)HttpStatusCode.Conflict,
                "Refund has already been completed.",
                BookingConstants.RefundErrorCodes.RefundAlreadyCompleted);
        }
        if (!string.IsNullOrWhiteSpace(request.TicketId)
            && !refund.Booking.BookingSeats.Any(item => item.Ticket?.TicketId == request.TicketId.Trim()))
        {
            return ServiceResult<object>.Fail(
                (int)HttpStatusCode.Forbidden,
                "Ticket does not belong to this booking.",
                BookingConstants.RefundErrorCodes.RefundRequestTicketForbidden);
        }
        if (refund.RefundClaim is null || refund.RefundClaim.ClaimStatus != BookingConstants.RefundClaimStatus.PendingInfo)
        {
            return ServiceResult<object>.Fail(
                (int)HttpStatusCode.Conflict,
                "A new link cannot be issued for this refund.",
                BookingConstants.RefundErrorCodes.RefundClaimNotReissuable);
        }

        var now = _clock.UtcNow;
        RevokeActiveTokens(refund.RefundClaim, now);
        var issue = _issuer.Create(refund.RefundId, refund.RefundClaim.CustomerProfileId, now);
        var replacement = issue.Token;
        replacement.RefundClaimId = refund.RefundClaim.RefundClaimId;
        refund.RefundClaim.Tokens.Add(replacement);
        refund.RefundClaim.ExpiresAt = replacement.ExpiresAt;
        refund.RefundClaim.UpdatedAt = now;
        _db.CustomerRefundRequests.Add(new CustomerRefundRequest
        {
            CustomerRefundRequestId = NewId(BookingConstants.EntityIdPrefix.CustomerRefundRequest),
            RefundId = refund.RefundId,
            CustomerProfileId = refund.RefundClaim.CustomerProfileId,
            TicketId = string.IsNullOrWhiteSpace(request.TicketId) ? null : request.TicketId.Trim(),
            RequestReason = request.Reason.Trim(),
            RequestStatus = BookingConstants.CustomerRefundRequestStatus.Fulfilled,
            ProcessedAt = now,
            CreatedAt = now
        });
        AddAudit(
            userId,
            DomainConstants.AuditAction.ReissueRefundClaimLink,
            DomainConstants.AuditEntity.Refund,
            refund.RefundId,
            now);
        await _db.SaveChangesAsync(cancellationToken);

        await TrySendClaimEmailAsync(
            refund.Booking.CustomerProfile!.User.Email,
            refund.Booking.Showtime.Movie.Title,
            issue.RawToken,
            replacement.ExpiresAt,
            cancellationToken);
        return ServiceResult<object>.Ok(new { expiresAt = replacement.ExpiresAt }, "A new refund link was sent.");
    }

    private async Task<RefundClaim?> LoadOwnedClaimAsync(
        string userId,
        string claimId,
        CancellationToken cancellationToken)
    {
        return await _db.RefundClaims
            .Include(item => item.Bank)
            .Include(item => item.Tokens)
            .Include(item => item.ManualRefundProcess)
            .Include(item => item.Refund)
                .ThenInclude(item => item.Booking)
                    .ThenInclude(item => item.Showtime)
                        .ThenInclude(item => item.Movie)
            .Include(item => item.Refund)
                .ThenInclude(item => item.Booking)
                    .ThenInclude(item => item.Showtime)
                        .ThenInclude(item => item.Room)
                            .ThenInclude(item => item.Cinema)
            .FirstOrDefaultAsync(
                item => item.RefundClaimId == claimId
                    && item.CustomerProfile.UserId == userId,
                cancellationToken);
    }

    private RefundClaimResponse ToResponse(RefundClaim claim, string? holderName = null)
    {
        holderName ??= claim.AccountHolderNameEncrypted is null
            ? null
            : _protector.Unprotect(claim.AccountHolderNameEncrypted);
        return new RefundClaimResponse
        {
            RefundClaimId = claim.RefundClaimId,
            RefundId = claim.RefundId,
            BookingId = claim.Refund.BookingId,
            ClaimStatus = claim.ClaimStatus,
            RefundStatus = claim.Refund.RefundStatus,
            RefundAmount = claim.Refund.RefundAmount,
            MovieTitle = claim.Refund.Booking.Showtime.Movie.Title,
            CinemaName = claim.Refund.Booking.Showtime.Room.Cinema.CinemaName,
            ShowtimeStartTime = claim.Refund.Booking.Showtime.StartTime,
            BankCode = claim.BankCode,
            BankName = claim.Bank?.ShortName,
            MaskedAccountNumber = claim.BankAccountLast4 is null
                ? null
                : $"{RefundContractConstants.MaskedAccountPrefix}{claim.BankAccountLast4}",
            AccountHolderName = holderName,
            ExpiresAt = claim.ExpiresAt,
            SubmittedAt = claim.SubmittedAt
        };
    }

    private async Task TrySendClaimEmailAsync(
        string email,
        string movieTitle,
        string rawToken,
        DateTime expiresAt,
        CancellationToken cancellationToken)
    {
        try
        {
            var link = $"{_settings.FrontendBaseUrl.TrimEnd('/')}"
                + $"{RefundSettings.ClaimRoute}?t={Uri.EscapeDataString(rawToken)}";
            await _emailSender.SendEmailAsync(
                email,
                _emailTemplates.RefundClaimSubject,
                string.Format(
                    CultureInfo.InvariantCulture,
                    _emailTemplates.RefundClaimBody,
                    movieTitle,
                    expiresAt,
                    link),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Refund claim email could not be sent.");
        }
    }

    private void AddAudit(string userId, string action, string entityName, string entityId, DateTime now)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            AuditLogId = NewId(BookingConstants.EntityIdPrefix.AuditLog),
            UserId = userId,
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            NewValue = JsonSerializer.Serialize(new { action }),
            CreatedAt = now
        });
    }

    private static void RevokeActiveTokens(RefundClaim claim, DateTime now)
    {
        foreach (var token in claim.Tokens.Where(item => !item.UsedAt.HasValue && !item.RevokedAt.HasValue))
        {
            token.RevokedAt = now;
        }
    }

    private static bool IsFinal(RefundClaim claim)
    {
        return claim.ClaimStatus is BookingConstants.RefundClaimStatus.Completed
            or BookingConstants.RefundClaimStatus.ManualRequired
            or BookingConstants.RefundClaimStatus.Revoked;
    }

    private static string NormalizeHolderName(string value)
    {
        return string.Join(' ', value.Trim().ToUpperInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static ServiceResult<RefundClaimResponse> FailClaim(int status, string message, string code)
        => ServiceResult<RefundClaimResponse>.Fail(status, message, code);

    private static string NewId(string prefix) => CinemaSystem.Domain.Utilities.IdGenerator.NewId(prefix);
}
