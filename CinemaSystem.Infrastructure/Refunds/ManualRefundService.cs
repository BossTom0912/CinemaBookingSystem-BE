using System.Data;
using System.Globalization;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Refunds;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace CinemaSystem.Infrastructure.Refunds;

public sealed class ManualRefundService : IManualRefundService
{
    private readonly CinemaDbContext _db;
    private readonly ISensitiveDataProtector _protector;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly EmailTemplatesSettings _emailTemplates;
    private readonly ILogger<ManualRefundService> _logger;

    public ManualRefundService(
        CinemaDbContext db,
        ISensitiveDataProtector protector,
        IEmailSender emailSender,
        IClock clock,
        IOptions<EmailTemplatesSettings> emailTemplates,
        ILogger<ManualRefundService> logger)
    {
        _db = db;
        _protector = protector;
        _emailSender = emailSender;
        _clock = clock;
        _emailTemplates = emailTemplates.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<IReadOnlyList<ManualRefundResponse>>> GetPendingAsync(
        CancellationToken cancellationToken)
    {
        var processes = await _db.ManualRefundProcesses
            .AsNoTracking()
            .Include(item => item.CustomerConfirmation)
            .Include(item => item.RefundClaim)
                .ThenInclude(item => item.Bank)
            .Include(item => item.Refund)
                .ThenInclude(item => item.Booking)
                    .ThenInclude(item => item.Showtime)
                        .ThenInclude(item => item.Movie)
            .Include(item => item.Refund)
                .ThenInclude(item => item.Booking)
                    .ThenInclude(item => item.Showtime)
                        .ThenInclude(item => item.Room)
                            .ThenInclude(item => item.Cinema)
            .Where(item => item.ProcessStatus != BookingConstants.ManualRefundProcessStatus.Confirmed)
            .OrderBy(item => item.CreatedAt)
            .ToListAsync(cancellationToken);

        var response = processes.Select(ToResponse).ToList();
        return ServiceResult<IReadOnlyList<ManualRefundResponse>>.Ok(response);
    }

    public async Task<ServiceResult<AssignManualRefundResponse>> AssignAsync(
        string refundId,
        string adminUserId,
        CancellationToken cancellationToken)
    {
        var executionStrategy = _db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            try
            {
                var process = await _db.ManualRefundProcesses
                    .FirstOrDefaultAsync(item => item.RefundId == refundId, cancellationToken);
                if (process is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<AssignManualRefundResponse>.Fail(
                        (int)HttpStatusCode.NotFound,
                        "Manual refund was not found.",
                        BookingConstants.RefundErrorCodes.ManualRefundNotFound);
                }
                if (process.ProcessStatus == BookingConstants.ManualRefundProcessStatus.Confirmed)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<AssignManualRefundResponse>.Fail(
                        (int)HttpStatusCode.Conflict,
                        "Manual refund has already been confirmed.",
                        BookingConstants.RefundErrorCodes.RefundAlreadyCompleted);
                }
                if (!string.IsNullOrWhiteSpace(process.AssignedToUserId)
                    && !string.Equals(process.AssignedToUserId, adminUserId, StringComparison.OrdinalIgnoreCase))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<AssignManualRefundResponse>.Fail(
                        (int)HttpStatusCode.Conflict,
                        "Manual refund is assigned to another administrator.",
                        BookingConstants.RefundErrorCodes.ManualRefundAlreadyAssigned);
                }

                var now = _clock.UtcNow;
                process.AssignedToUserId = adminUserId;
                process.AssignedAt ??= now;
                process.ProcessStatus = BookingConstants.ManualRefundProcessStatus.InProgress;
                AddAudit(adminUserId, DomainConstants.AuditAction.AssignManualRefund, process.RefundId, now);
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ServiceResult<AssignManualRefundResponse>.Ok(new AssignManualRefundResponse
                {
                    RefundId = process.RefundId,
                    ManualRefundProcessId = process.ManualRefundProcessId,
                    ProcessStatus = process.ProcessStatus,
                    AssignedToUserId = adminUserId,
                    AssignedAt = process.AssignedAt.Value
                });
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }

    public async Task<ServiceResult<RefundProcessingResponse>> ConfirmAsync(
        string refundId,
        string adminUserId,
        ManualRefundConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var executionStrategy = _db.Database.CreateExecutionStrategy();
        return await executionStrategy.ExecuteAsync(async () =>
        {
            await using var transaction = await _db.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            try
            {
                var process = await _db.ManualRefundProcesses
                    .Include(item => item.CustomerConfirmation)
                    .Include(item => item.RefundClaim)
                    .Include(item => item.Refund)
                        .ThenInclude(item => item.Payment)
                            .ThenInclude(item => item.Refunds)
                    .Include(item => item.Refund)
                        .ThenInclude(item => item.Booking)
                            .ThenInclude(item => item.BookingSeats)
                                .ThenInclude(item => item.Ticket)
                    .Include(item => item.Refund)
                        .ThenInclude(item => item.Booking)
                            .ThenInclude(item => item.CustomerProfile)
                                .ThenInclude(item => item!.User)
                    .Include(item => item.Refund)
                        .ThenInclude(item => item.Booking)
                            .ThenInclude(item => item.RewardPointTransactions)
                    .Include(item => item.Refund)
                        .ThenInclude(item => item.Booking)
                            .ThenInclude(item => item.Showtime)
                                .ThenInclude(item => item.Movie)
                    .FirstOrDefaultAsync(item => item.RefundId == refundId, cancellationToken);
                if (process is null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return FailConfirm(
                        (int)HttpStatusCode.NotFound,
                        "Manual refund was not found.",
                        BookingConstants.RefundErrorCodes.ManualRefundNotFound);
                }
                if (process.ProcessStatus == BookingConstants.ManualRefundProcessStatus.Confirmed
                    && process.Refund.RefundStatus == BookingConstants.RefundStatus.Success)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return ServiceResult<RefundProcessingResponse>.Ok(
                        ToProcessingResponse(process.Refund, true),
                        "Manual refund was already confirmed.");
                }
                if (!string.Equals(process.AssignedToUserId, adminUserId, StringComparison.OrdinalIgnoreCase))
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return FailConfirm(
                        (int)HttpStatusCode.Conflict,
                        "Assign this refund before confirming it.",
                        BookingConstants.RefundErrorCodes.ManualRefundNotAssignedToUser);
                }
                if (process.Refund.RefundStatus != BookingConstants.RefundStatus.ManualRequired)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return FailConfirm(
                        (int)HttpStatusCode.Conflict,
                        "Refund is not awaiting manual processing.",
                        BookingConstants.RefundErrorCodes.RefundNotManualRequired);
                }
                if (process.CustomerConfirmation is not null
                    && process.CustomerConfirmation.Status != BookingConstants.RefundCustomerConfirmationStatus.ConfirmedByCustomer)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return FailConfirm(
                        (int)HttpStatusCode.Conflict,
                        "Wait for the customer to confirm the refund details before recording the transfer.",
                        "REFUND_CUSTOMER_CONFIRMATION_REQUIRED");
                }
                if (request.TransferredAmount != process.Refund.RefundAmount)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return FailConfirm(
                        (int)HttpStatusCode.BadRequest,
                        "Transferred amount must match the refund amount.",
                        BookingConstants.RefundErrorCodes.RefundAmountMismatch);
                }
                if (!Uri.TryCreate(request.ProofUrl.Trim(), UriKind.Absolute, out var proofUri)
                    || proofUri.Scheme != Uri.UriSchemeHttps)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return FailConfirm(
                        (int)HttpStatusCode.BadRequest,
                        "Proof URL must be an absolute HTTPS URL.",
                        BookingConstants.RefundErrorCodes.InvalidRefundProofUrl);
                }

                var transactionCode = request.BankTransactionCode.Trim();
                var duplicateCode = await _db.ManualRefundProcesses.AnyAsync(
                    item => item.ManualRefundProcessId != process.ManualRefundProcessId
                        && item.BankTransactionCode == transactionCode,
                    cancellationToken);
                if (duplicateCode)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return FailConfirm(
                        (int)HttpStatusCode.Conflict,
                        "Bank transaction code has already been used.",
                        BookingConstants.RefundErrorCodes.RefundTransactionCodeDuplicate);
                }

                var otherSuccessfulAmount = process.Refund.Payment.Refunds
                    .Where(item => item.RefundId != process.RefundId
                        && item.RefundStatus == BookingConstants.RefundStatus.Success)
                    .Sum(item => item.RefundAmount);
                if (otherSuccessfulAmount + process.Refund.RefundAmount > process.Refund.Payment.Amount)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return FailConfirm(
                        (int)HttpStatusCode.Conflict,
                        "Successful refund total would exceed the payment amount.",
                        BookingConstants.RefundErrorCodes.RefundTotalExceedsPayment);
                }

                var now = _clock.UtcNow;
                process.BankTransactionCode = transactionCode;
                process.TransferredAmount = request.TransferredAmount;
                process.ProofUrl = proofUri.AbsoluteUri;
                process.AdminNote = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim();
                process.ConfirmedAt = now;
                process.ProcessStatus = BookingConstants.ManualRefundProcessStatus.Confirmed;

                process.Refund.RefundStatus = BookingConstants.RefundStatus.Success;
                process.Refund.ProviderRefundCode = transactionCode;
                process.Refund.FailureReason = null;
                process.Refund.RefundedAt = now;
                process.RefundClaim.ClaimStatus = BookingConstants.RefundClaimStatus.Completed;
                process.RefundClaim.CompletedAt = now;
                process.RefundClaim.UpdatedAt = now;

                var totalSuccessful = otherSuccessfulAmount + process.Refund.RefundAmount;
                var revertedPoints = 0;
                if (totalSuccessful >= process.Refund.Payment.Amount)
                {
                    process.Refund.Booking.BookingStatus = BookingConstants.BookingStatus.Refunded;
                    foreach (var bookingSeat in process.Refund.Booking.BookingSeats)
                    {
                        if (bookingSeat.Ticket is not null)
                        {
                            bookingSeat.Ticket.TicketStatus = BookingConstants.TicketStatus.Refunded;
                        }
                    }
                    revertedPoints = RevertRewardPoints(process.Refund.Booking, now);
                }

                var customerUserId = process.Refund.Booking.CustomerProfile?.UserId;
                if (!string.IsNullOrWhiteSpace(customerUserId))
                {
                    _db.Notifications.Add(new Notification
                    {
                        NotificationId = NewId(BookingConstants.EntityIdPrefix.Notification),
                        UserId = customerUserId,
                        BookingId = process.Refund.BookingId,
                        Title = "Refund completed",
                        Message = $"Refund {process.Refund.RefundAmount:N0} was completed.",
                        IsRead = false,
                        CreatedAt = now
                    });
                }
                AddAudit(
                    adminUserId,
                    DomainConstants.AuditAction.ConfirmManualRefund,
                    process.RefundId,
                    now,
                    new { transactionCode, request.TransferredAmount, revertedPoints });
                await _db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);

                var email = process.Refund.Booking.CustomerProfile?.User.Email
                    ?? process.Refund.Booking.GuestEmail;
                if (!string.IsNullOrWhiteSpace(email))
                {
                    await TrySendCompletedEmailAsync(
                        email,
                        process.Refund.Booking.Showtime.Movie.Title,
                        process.Refund.RefundAmount,
                        transactionCode,
                        cancellationToken);
                }
                return ServiceResult<RefundProcessingResponse>.Ok(
                    ToProcessingResponse(process.Refund, false, revertedPoints),
                    "Manual refund confirmed successfully.");
            }
            catch
            {
                await transaction.RollbackAsync(CancellationToken.None);
                throw;
            }
        });
    }

    private ManualRefundResponse ToResponse(ManualRefundProcess process)
    {
        return new ManualRefundResponse
        {
            RefundId = process.RefundId,
            BookingId = process.Refund.BookingId,
            RefundClaimId = process.RefundClaimId,
            RefundStatus = process.Refund.RefundStatus,
            ClaimStatus = process.RefundClaim.ClaimStatus,
            ProcessStatus = process.ProcessStatus,
            RefundAmount = process.Refund.RefundAmount,
            MovieTitle = process.Refund.Booking.Showtime.Movie.Title,
            CinemaName = process.Refund.Booking.Showtime.Room.Cinema.CinemaName,
            ShowtimeStartTime = process.Refund.Booking.Showtime.StartTime,
            BankCode = process.RefundClaim.BankCode ?? string.Empty,
            BankName = process.RefundClaim.Bank?.ShortName ?? string.Empty,
            AccountNumber = process.RefundClaim.BankAccountEncrypted is null
                ? string.Empty
                : _protector.Unprotect(process.RefundClaim.BankAccountEncrypted),
            AccountHolderName = process.RefundClaim.AccountHolderNameEncrypted is null
                ? string.Empty
                : _protector.Unprotect(process.RefundClaim.AccountHolderNameEncrypted),
            AssignedToUserId = process.AssignedToUserId,
            BankTransactionCode = process.BankTransactionCode,
            ProofUrl = process.ProofUrl,
            RequestedAt = process.Refund.RequestedAt,
            ConfirmedAt = process.ConfirmedAt,
            CustomerConfirmationStatus = process.CustomerConfirmation?.Status,
            CustomerConfirmationExpiresAt = process.CustomerConfirmation?.ExpiresAt
        };
    }

    private int RevertRewardPoints(Booking booking, DateTime now)
    {
        if (booking.CustomerProfile is null)
        {
            return 0;
        }
        var earned = booking.RewardPointTransactions
            .Where(item => item.TransactionType == BookingConstants.RewardPointTransactionType.Earn && item.Points > 0)
            .Sum(item => item.Points);
        var reverted = booking.RewardPointTransactions
            .Where(item => item.TransactionType == BookingConstants.RewardPointTransactionType.Revert && item.Points < 0)
            .Sum(item => Math.Abs(item.Points));
        var amount = Math.Max(0, earned - reverted);
        if (amount == 0)
        {
            return 0;
        }
        booking.CustomerProfile.RewardPoints = Math.Max(0, booking.CustomerProfile.RewardPoints - amount);
        _db.RewardPointTransactions.Add(new RewardPointTransaction
        {
            RewardTransactionId = NewId(BookingConstants.EntityIdPrefix.RewardPointTransaction),
            CustomerProfileId = booking.CustomerProfile.CustomerProfileId,
            BookingId = booking.BookingId,
            TransactionType = BookingConstants.RewardPointTransactionType.Revert,
            Points = -amount,
            CreatedAt = now
        });
        return amount;
    }

    private void AddAudit(
        string userId,
        string action,
        string refundId,
        DateTime now,
        object? value = null)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            AuditLogId = NewId(BookingConstants.EntityIdPrefix.AuditLog),
            UserId = userId,
            Action = action,
            EntityName = DomainConstants.AuditEntity.Refund,
            EntityId = refundId,
            NewValue = value is null ? null : JsonSerializer.Serialize(value),
            CreatedAt = now
        });
    }

    private async Task TrySendCompletedEmailAsync(
        string email,
        string movieTitle,
        decimal amount,
        string transactionCode,
        CancellationToken cancellationToken)
    {
        try
        {
            await _emailSender.SendEmailAsync(
                email,
                _emailTemplates.ManualRefundCompletedSubject,
                string.Format(
                    CultureInfo.InvariantCulture,
                    _emailTemplates.ManualRefundCompletedBody,
                    amount,
                    movieTitle,
                    transactionCode),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Refund completion email could not be sent.");
        }
    }

    private static RefundProcessingResponse ToProcessingResponse(
        Refund refund,
        bool alreadyProcessed,
        int rewardPointsReverted = 0)
    {
        return new RefundProcessingResponse
        {
            RefundId = refund.RefundId,
            RefundStatus = refund.RefundStatus,
            BookingStatus = refund.Booking.BookingStatus,
            ProviderRefundCode = refund.ProviderRefundCode,
            FailureReason = refund.FailureReason,
            RewardPointsReverted = rewardPointsReverted,
            AlreadyProcessed = alreadyProcessed
        };
    }

    private static ServiceResult<RefundProcessingResponse> FailConfirm(int status, string message, string code)
        => ServiceResult<RefundProcessingResponse>.Fail(status, message, code);

    private static string NewId(string prefix) => CinemaSystem.Domain.Utilities.IdGenerator.NewId(prefix);
}
