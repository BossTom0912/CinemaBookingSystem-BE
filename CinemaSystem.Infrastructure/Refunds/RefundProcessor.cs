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

public sealed class RefundProcessor : IRefundProcessor
{
    private readonly CinemaDbContext _dbContext;
    private readonly IPaymentRefundGateway _paymentRefundGateway;
    private readonly IEmailSender _emailSender;
    private readonly IClock _clock;
    private readonly EmailTemplatesSettings _emailTemplates;
    private readonly ILogger<RefundProcessor> _logger;

    public RefundProcessor(
        CinemaDbContext dbContext,
        IPaymentRefundGateway paymentRefundGateway,
        IEmailSender emailSender,
        IClock clock,
        IOptions<EmailTemplatesSettings> emailTemplates,
        ILogger<RefundProcessor> logger)
    {
        _dbContext = dbContext;
        _paymentRefundGateway = paymentRefundGateway;
        _emailSender = emailSender;
        _clock = clock;
        _emailTemplates = emailTemplates.Value;
        _logger = logger;
    }

    public async Task<ServiceResult<RefundProcessingResponse>> ProcessAsync(
        string refundId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(refundId))
        {
            return ServiceResult<RefundProcessingResponse>.Fail(
                (int)HttpStatusCode.BadRequest,
                "Refund ID is required.",
                BookingConstants.RefundErrorCodes.RefundIdRequired);
        }

        var snapshot = await LoadSnapshotAsync(refundId.Trim(), cancellationToken);
        if (snapshot is null)
        {
            return ServiceResult<RefundProcessingResponse>.Fail(
                (int)HttpStatusCode.NotFound,
                "Refund was not found.",
                BookingConstants.RefundErrorCodes.RefundNotFound);
        }

        if (IsFinalStatus(snapshot.RefundStatus))
        {
            return ServiceResult<RefundProcessingResponse>.Ok(
                ToResponse(snapshot, alreadyProcessed: true),
                "Refund has already been processed.");
        }

        if (!IsStatus(snapshot.RefundStatus, BookingConstants.RefundStatus.Pending))
        {
            return ServiceResult<RefundProcessingResponse>.Fail(
                (int)HttpStatusCode.Conflict,
                "Only pending refunds can be processed.",
                BookingConstants.RefundErrorCodes.RefundNotProcessable);
        }

        var successfulRefundAmount = await _dbContext.Refunds
            .AsNoTracking()
            .Where(item =>
                item.PaymentId == snapshot.PaymentId
                && item.RefundId != snapshot.RefundId
                && item.RefundStatus == BookingConstants.RefundStatus.Success)
            .SumAsync(item => (decimal?)item.RefundAmount, cancellationToken) ?? 0m;

        if (successfulRefundAmount + snapshot.RefundAmount > snapshot.PaymentAmount)
        {
            return await CompleteAsync(
                snapshot.RefundId,
                PaymentRefundGatewayResult.Failed(
                    "Successful refund total would exceed the original payment amount."),
                cancellationToken);
        }

        PaymentRefundGatewayResult gatewayResult;
        try
        {
            // The refund ID is the idempotency key that a real provider adapter must use.
            // The external call is deliberately outside the database transaction so a slow
            // gateway cannot hold locks on booking, ticket, and refund rows.
            gatewayResult = await _paymentRefundGateway.RefundAsync(
                new PaymentRefundGatewayRequest(
                    snapshot.RefundId,
                    snapshot.PaymentId,
                    snapshot.PaymentProviderId,
                    snapshot.PaymentProviderName,
                    snapshot.RefundAmount,
                    snapshot.ProviderTransactionCode,
                    snapshot.RefundReason),
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Refund gateway call failed for refund {RefundId}.",
                snapshot.RefundId);
            gatewayResult = PaymentRefundGatewayResult.Failed(
                "Automatic refund failed because the payment provider could not be reached.");
        }

        return await CompleteAsync(snapshot.RefundId, gatewayResult, cancellationToken);
    }

    private async Task<ServiceResult<RefundProcessingResponse>> CompleteAsync(
        string refundId,
        PaymentRefundGatewayResult gatewayResult,
        CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);

        RefundProcessingEmail? email = null;
        try
        {
            var refund = await _dbContext.Refunds
                .Include(item => item.Payment)
                    .ThenInclude(item => item.Refunds)
                .Include(item => item.Booking)
                    .ThenInclude(item => item.BookingSeats)
                        .ThenInclude(item => item.Ticket)
                .Include(item => item.Booking)
                    .ThenInclude(item => item.CustomerProfile)
                        .ThenInclude(item => item!.User)
                .Include(item => item.Booking)
                    .ThenInclude(item => item.RewardPointTransactions)
                .Include(item => item.Booking)
                    .ThenInclude(item => item.Showtime)
                        .ThenInclude(item => item.Movie)
                .Include(item => item.ShowtimeCancellation)
                .FirstOrDefaultAsync(item => item.RefundId == refundId, cancellationToken);

            if (refund is null)
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<RefundProcessingResponse>.Fail(
                    (int)HttpStatusCode.NotFound,
                    "Refund was not found.",
                    BookingConstants.RefundErrorCodes.RefundNotFound);
            }

            if (IsFinalStatus(refund.RefundStatus))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<RefundProcessingResponse>.Ok(
                    ToResponse(refund, rewardPointsReverted: 0, alreadyProcessed: true),
                    "Refund has already been processed.");
            }

            if (!IsStatus(refund.RefundStatus, BookingConstants.RefundStatus.Pending))
            {
                await transaction.RollbackAsync(cancellationToken);
                return ServiceResult<RefundProcessingResponse>.Fail(
                    (int)HttpStatusCode.Conflict,
                    "Only pending refunds can be processed.",
                    BookingConstants.RefundErrorCodes.RefundNotProcessable);
            }

            var now = _clock.UtcNow;
            var rewardPointsReverted = 0;

            if (gatewayResult.Outcome == PaymentRefundGatewayOutcome.Success)
            {
                var otherSuccessfulAmount = refund.Payment.Refunds
                    .Where(item =>
                        item.RefundId != refund.RefundId
                        && IsStatus(item.RefundStatus, BookingConstants.RefundStatus.Success))
                    .Sum(item => item.RefundAmount);

                if (otherSuccessfulAmount + refund.RefundAmount > refund.Payment.Amount)
                {
                    gatewayResult = PaymentRefundGatewayResult.Failed(
                        "Successful refund total would exceed the original payment amount.");
                }
                else
                {
                    refund.RefundStatus = BookingConstants.RefundStatus.Success;
                    refund.ProviderRefundCode = gatewayResult.ProviderRefundCode;
                    refund.FailureReason = null;
                    refund.RefundedAt = now;

                    var totalSuccessfulAmount = otherSuccessfulAmount + refund.RefundAmount;
                    if (totalSuccessfulAmount >= refund.Payment.Amount)
                    {
                        refund.Booking.BookingStatus = BookingConstants.BookingStatus.Refunded;
                        foreach (var bookingSeat in refund.Booking.BookingSeats)
                        {
                            if (bookingSeat.Ticket is not null)
                            {
                                bookingSeat.Ticket.TicketStatus =
                                    BookingConstants.TicketStatus.Refunded;
                            }
                        }

                        rewardPointsReverted = RevertEarnedRewardPoints(refund.Booking, now);
                    }

                    AddRefundNotification(
                        refund,
                        "Refund completed",
                        $"Refund {refund.RefundAmount:N0} for booking {refund.BookingId} was completed.",
                        now);
                    AddRefundAuditLog(
                        refund,
                        BookingConstants.RefundStatus.Success,
                        rewardPointsReverted,
                        now);
                    email = CreateEmail(
                        refund,
                        _emailTemplates.RefundCompletedSubject,
                        string.Format(
                            CultureInfo.InvariantCulture,
                            _emailTemplates.RefundCompletedBody,
                            refund.RefundAmount,
                            refund.Booking.Showtime.Movie.Title));
                }
            }

            if (gatewayResult.Outcome != PaymentRefundGatewayOutcome.Success)
            {
                // BR-40 and UC003 require a failed or unsupported automatic refund to
                // remain traceable for staff follow-up instead of being reported as paid.
                refund.RefundStatus = BookingConstants.RefundStatus.ManualRequired;
                refund.FailureReason = NormalizeFailureReason(gatewayResult.FailureReason);
                refund.ProviderRefundCode = null;
                refund.RefundedAt = null;
                refund.Booking.BookingStatus = BookingConstants.BookingStatus.RefundPending;

                AddRefundNotification(
                    refund,
                    "Refund requires manual processing",
                    $"Refund for booking {refund.BookingId} requires additional processing.",
                    now);
                AddRefundAuditLog(
                    refund,
                    BookingConstants.RefundStatus.ManualRequired,
                    0,
                    now);
                email = CreateEmail(
                    refund,
                    _emailTemplates.RefundManualRequiredSubject,
                    string.Format(
                        CultureInfo.InvariantCulture,
                        _emailTemplates.RefundManualRequiredBody,
                        refund.Booking.Showtime.Movie.Title));
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            if (email is not null)
            {
                await TrySendEmailAsync(email, cancellationToken);
            }

            return ServiceResult<RefundProcessingResponse>.Ok(
                ToResponse(refund, rewardPointsReverted, alreadyProcessed: false),
                refund.RefundStatus == BookingConstants.RefundStatus.Success
                    ? "Refund processed successfully."
                    : "Refund requires manual processing.");
        }
        catch
        {
            await RollbackSafelyAsync(transaction);
            throw;
        }
    }

    private async Task<RefundSnapshot?> LoadSnapshotAsync(
        string refundId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.Refunds
            .AsNoTracking()
            .Where(item => item.RefundId == refundId)
            .Select(item => new RefundSnapshot(
                item.RefundId,
                item.PaymentId,
                item.PaymentProviderId,
                item.Payment.PaymentProvider.ProviderName,
                item.RefundAmount,
                item.Payment.Amount,
                item.Payment.ProviderTransactionCode,
                item.RefundReason,
                item.RefundStatus,
                item.Booking.BookingStatus,
                item.ProviderRefundCode,
                item.FailureReason))
            .FirstOrDefaultAsync(cancellationToken);
    }

    private int RevertEarnedRewardPoints(Booking booking, DateTime now)
    {
        if (booking.CustomerProfile is null)
        {
            return 0;
        }

        var earnedPoints = booking.RewardPointTransactions
            .Where(item =>
                IsStatus(
                    item.TransactionType,
                    BookingConstants.RewardPointTransactionType.Earn)
                && item.Points > 0)
            .Sum(item => item.Points);
        var alreadyRevertedPoints = booking.RewardPointTransactions
            .Where(item =>
                IsStatus(
                    item.TransactionType,
                    BookingConstants.RewardPointTransactionType.Revert)
                && item.Points < 0)
            .Sum(item => Math.Abs(item.Points));
        var pointsToRevert = Math.Max(0, earnedPoints - alreadyRevertedPoints);
        if (pointsToRevert == 0)
        {
            return 0;
        }

        // The ledger records the full invalidation even when the customer has already
        // spent part of the earned balance. The profile balance itself cannot go below
        // zero because the database enforces that invariant.
        booking.CustomerProfile.RewardPoints = Math.Max(
            0,
            booking.CustomerProfile.RewardPoints - pointsToRevert);
        _dbContext.RewardPointTransactions.Add(new RewardPointTransaction
        {
            RewardTransactionId = NewId(BookingConstants.EntityIdPrefix.RewardPointTransaction),
            CustomerProfileId = booking.CustomerProfile.CustomerProfileId,
            BookingId = booking.BookingId,
            TransactionType = BookingConstants.RewardPointTransactionType.Revert,
            Points = -pointsToRevert,
            CreatedAt = now
        });

        return pointsToRevert;
    }

    private void AddRefundNotification(
        Refund refund,
        string title,
        string message,
        DateTime now)
    {
        var userId = refund.Booking.CustomerProfile?.UserId;
        if (string.IsNullOrWhiteSpace(userId))
        {
            return;
        }

        _dbContext.Notifications.Add(new Notification
        {
            NotificationId = NewId(BookingConstants.EntityIdPrefix.Notification),
            UserId = userId,
            BookingId = refund.BookingId,
            Title = title,
            Message = message,
            IsRead = false,
            CreatedAt = now
        });
    }

    private void AddRefundAuditLog(
        Refund refund,
        string result,
        int rewardPointsReverted,
        DateTime now)
    {
        var actorUserId = refund.ShowtimeCancellation?.CancelledByUserId;
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            _logger.LogWarning(
                "Refund {RefundId} has no cancellation actor; PROCESS_REFUND audit was not created.",
                refund.RefundId);
            return;
        }

        _dbContext.AuditLogs.Add(new AuditLog
        {
            AuditLogId = NewId(BookingConstants.EntityIdPrefix.AuditLog),
            UserId = actorUserId,
            Action = DomainConstants.AuditAction.ProcessRefund,
            EntityName = DomainConstants.AuditEntity.Refund,
            EntityId = refund.RefundId,
            OldValue = JsonSerializer.Serialize(new
            {
                status = BookingConstants.RefundStatus.Pending
            }),
            NewValue = JsonSerializer.Serialize(new
            {
                status = refund.RefundStatus,
                result,
                refund.ProviderRefundCode,
                refund.FailureReason,
                rewardPointsReverted
            }),
            CreatedAt = now
        });
    }

    private static RefundProcessingEmail? CreateEmail(
        Refund refund,
        string subject,
        string body)
    {
        var email = refund.Booking.CustomerProfile?.User.Email
            ?? refund.Booking.GuestEmail;
        return string.IsNullOrWhiteSpace(email)
            ? null
            : new RefundProcessingEmail(email, subject, body);
    }

    private async Task TrySendEmailAsync(
        RefundProcessingEmail email,
        CancellationToken cancellationToken)
    {
        try
        {
            // Email is sent after the database commit. SMTP failure must not undo a
            // financial state transition that has already been persisted.
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
                "Refund email could not be sent to {Email}.",
                email.ToEmail);
        }
    }

    private static RefundProcessingResponse ToResponse(
        RefundSnapshot snapshot,
        bool alreadyProcessed)
    {
        return new RefundProcessingResponse
        {
            RefundId = snapshot.RefundId,
            RefundStatus = snapshot.RefundStatus,
            BookingStatus = snapshot.BookingStatus,
            ProviderRefundCode = snapshot.ProviderRefundCode,
            FailureReason = snapshot.FailureReason,
            AlreadyProcessed = alreadyProcessed
        };
    }

    private static RefundProcessingResponse ToResponse(
        Refund refund,
        int rewardPointsReverted,
        bool alreadyProcessed)
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

    private static string NormalizeFailureReason(string? failureReason)
    {
        const string fallback = "Automatic refund could not be completed.";
        if (string.IsNullOrWhiteSpace(failureReason))
        {
            return fallback;
        }

        var normalized = failureReason.Trim();
        return normalized.Length <= RefundContractConstants.FailureReasonMaxLength
            ? normalized
            : normalized[..RefundContractConstants.FailureReasonMaxLength];
    }

    private static bool IsFinalStatus(string status)
    {
        return IsStatus(status, BookingConstants.RefundStatus.Success)
            || IsStatus(status, BookingConstants.RefundStatus.ManualRequired);
    }

    private static bool IsStatus(string? actual, string expected)
    {
        return string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task RollbackSafelyAsync(
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
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

    private sealed record RefundSnapshot(
        string RefundId,
        string PaymentId,
        string PaymentProviderId,
        string PaymentProviderName,
        decimal RefundAmount,
        decimal PaymentAmount,
        string? ProviderTransactionCode,
        string? RefundReason,
        string RefundStatus,
        string BookingStatus,
        string? ProviderRefundCode,
        string? FailureReason);

    private sealed record RefundProcessingEmail(
        string ToEmail,
        string Subject,
        string Body);
}
