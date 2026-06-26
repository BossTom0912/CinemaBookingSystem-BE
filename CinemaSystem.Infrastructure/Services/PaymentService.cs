using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Payments;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CinemaSystem.Infrastructure.Configuration;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using CinemaSystem.Application.Common;
using Microsoft.Extensions.Logging;

namespace CinemaSystem.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly CinemaDbContext _db;
    private readonly SepaySettings _sepaySettings;
    private readonly IRefundClaimIssuer _refundClaimIssuer;
    private readonly IEmailSender _emailSender;
    private readonly RefundSettings _refundSettings;
    private readonly ILogger<PaymentService> _logger;
    private const int PaymentExpiryMinutes = 10;
    private static readonly Regex TransactionCodeRegex = new(@"T[A-Z0-9]{10}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public PaymentService(
        CinemaDbContext db,
        IOptions<SepaySettings> sepayOptions,
        IRefundClaimIssuer refundClaimIssuer,
        IEmailSender emailSender,
        IOptions<RefundSettings> refundSettings,
        ILogger<PaymentService> logger)
    {
        _db = db;
        _sepaySettings = sepayOptions.Value;
        _refundClaimIssuer = refundClaimIssuer;
        _emailSender = emailSender;
        _refundSettings = refundSettings.Value;
        _logger = logger;
    }

    // Create payment record for a booking and return bank info + transaction code
    public async Task<CreatePaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request,
        string userId,
        CancellationToken cancellationToken = default)
    {
        var bookingId = request.BookingId.Trim();
        var paymentProviderId = request.PaymentProviderId.Trim();
        var normalizedUserId = userId?.Trim();

        if (string.IsNullOrWhiteSpace(bookingId))
            throw new ArgumentException("BookingId must be provided.", nameof(request));
        if (string.IsNullOrWhiteSpace(paymentProviderId))
            throw new ArgumentException("PaymentProviderId must be provided.", nameof(request));
        if (string.IsNullOrWhiteSpace(normalizedUserId))
            throw new UnauthorizedAccessException("Unauthorized.");

        // Find booking
        var booking = await _db.Bookings
            .Include(item => item.Payments)
            .Include(item => item.CustomerProfile)
            .SingleOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);
        if (booking == null)
            throw new InvalidOperationException($"Booking {bookingId} not found.");

        if (booking.CustomerProfile is null ||
            !string.Equals(booking.CustomerProfile.UserId, normalizedUserId, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("You are not allowed to pay for this booking.");

        if (!string.Equals(booking.BookingStatus, "PENDING_PAYMENT", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Booking {booking.BookingId} is not awaiting payment.");

        // Ensure payment provider exists
        var provider = await _db.PaymentProviders.SingleOrDefaultAsync(p => p.PaymentProviderId == paymentProviderId, cancellationToken);
        if (provider == null)
            throw new InvalidOperationException($"Payment provider {paymentProviderId} not found.");
        if (!string.Equals(provider.ProviderStatus, "ACTIVE", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Payment provider {paymentProviderId} is not active.");

        var successfulPayment = booking.Payments
            .FirstOrDefault(item => string.Equals(item.PaymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase));
        if (successfulPayment != null)
            throw new InvalidOperationException($"Booking {booking.BookingId} has already been paid.");

        var paymentAmount = GetPaymentAmount(booking.TotalAmount);
        var pendingPayment = booking.Payments
            .Where(item =>
                item.PaymentProviderId == provider.PaymentProviderId
                && string.Equals(item.PaymentStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        if (pendingPayment != null)
        {
            if (pendingPayment.Amount != paymentAmount)
            {
                pendingPayment.Amount = paymentAmount;
                await _db.SaveChangesAsync(cancellationToken);
            }

            return ToCreatePaymentResponse(pendingPayment, booking.ExpiredAt);
        }

        var now = DateTime.UtcNow;

        // Create payment
        var payment = new Payment
        {
            PaymentId = GenerateId("PAY"),
            BookingId = booking.BookingId,
            PaymentProviderId = provider.PaymentProviderId,
            Amount = paymentAmount,
            TransactionCode = GenerateTransactionCode(),
            PaymentStatus = "PENDING",
            CreatedAt = now,
            PaymentMethod = "SEPAY"
        };

        booking.ExpiredAt = now.AddMinutes(PaymentExpiryMinutes);
        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        return ToCreatePaymentResponse(payment, booking.ExpiredAt);
    }

    private CreatePaymentResponse ToCreatePaymentResponse(Payment payment, DateTime? expiresAt)
    {
        return new CreatePaymentResponse
        {
            PaymentId = payment.PaymentId,
            Amount = payment.Amount,
            TransactionCode = payment.TransactionCode ?? string.Empty,
            BankName = _sepaySettings.BankName,
            BankAccount = _sepaySettings.BankAccount,
            ExpiresAt = expiresAt
        };
    }

    private decimal GetPaymentAmount(decimal bookingTotalAmount)
    {
        return _sepaySettings.DevelopmentPaymentAmountOverride is > 0
            ? _sepaySettings.DevelopmentPaymentAmountOverride.Value
            : bookingTotalAmount;
    }

    public async Task ConfirmPaymentAsync(
        string transactionContent,
        decimal amount,
        string? providerTransactionCode = null,
        string? rawCallbackPayload = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionContent))
            throw new ArgumentException("Transaction content must be provided.", nameof(transactionContent));
        if (amount <= 0)
            throw new ArgumentException("Payment amount must be greater than zero.", nameof(amount));

        // Extract transaction code from content using regex (transaction codes are like TXXXXXXXXXX)
        var match = TransactionCodeRegex.Match(transactionContent);
        if (!match.Success)
            throw new InvalidOperationException("No transaction code found in the provided transaction content.");

        var transactionCode = match.Value.ToUpperInvariant();

        // Find payment by exact transaction code
        var payment = await _db.Payments
            .Include(p => p.Refunds)
            .Include(p => p.Booking)
                .ThenInclude(b => b.BookingSeats)
                    .ThenInclude(bs => bs.ShowtimeSeat)
            .Include(p => p.Booking)
                .ThenInclude(b => b.BookingSeats)
                    .ThenInclude(bs => bs.Ticket)
            .Include(p => p.Booking)
                .ThenInclude(b => b.CustomerProfile)
                    .ThenInclude(cp => cp!.User)
            .SingleOrDefaultAsync(
                p => p.TransactionCode != null
                    && p.TransactionCode.ToUpper() == transactionCode,
                cancellationToken);

        if (payment == null)
            throw new InvalidOperationException($"Payment with transaction code {transactionCode} not found.");

        // Validate amount
        if (payment.Amount != amount)
            throw new InvalidOperationException($"Payment amount mismatch. Expected {payment.Amount} got {amount}.");

        if (string.Equals(payment.PaymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        // Persist changes inside a transaction
        await using var tx = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            payment.PaymentStatus = "SUCCESS";
            payment.PaidAt = DateTime.UtcNow;
            payment.UpdatedAt = DateTime.UtcNow;
            payment.ProviderTransactionCode = string.IsNullOrWhiteSpace(providerTransactionCode)
                ? payment.ProviderTransactionCode
                : providerTransactionCode.Trim();
            payment.RawCallbackPayload = string.IsNullOrWhiteSpace(rawCallbackPayload)
                ? payment.RawCallbackPayload
                : rawCallbackPayload;

            // Update booking status to PAID when previously PENDING_PAYMENT
            var booking = payment.Booking ?? await _db.Bookings.SingleOrDefaultAsync(b => b.BookingId == payment.BookingId, cancellationToken);
            if (booking == null)
                throw new InvalidOperationException($"Booking {payment.BookingId} not found.");

            var showtime = await _db.Showtimes
                .Include(item => item.ShowtimeCancellation)
                .Include(item => item.Movie)
                .FirstOrDefaultAsync(item => item.ShowtimeId == booking.ShowtimeId, cancellationToken);

            if (IsCancelledOrRefundFlow(booking, showtime))
            {
                RefundClaimIssue? claimIssue = null;
                booking.BookingStatus = BookingConstants.BookingStatus.RefundPending;

                foreach (var bookingSeat in booking.BookingSeats)
                {
                    bookingSeat.ShowtimeSeat.SeatStatus = BookingConstants.ShowtimeSeatStatus.Unavailable;
                    bookingSeat.ShowtimeSeat.LockedUntil = null;
                    bookingSeat.ShowtimeSeat.LockedByUserId = null;

                    if (bookingSeat.Ticket is not null
                        && !IsStatus(bookingSeat.Ticket.TicketStatus, BookingConstants.TicketStatus.Refunded))
                    {
                        bookingSeat.Ticket.TicketStatus = BookingConstants.TicketStatus.Cancelled;
                    }
                }

                if (!payment.Refunds.Any(IsActiveRefund))
                {
                    var refund = new Refund
                    {
                        RefundId = GenerateId("REF"),
                        BookingId = booking.BookingId,
                        PaymentId = payment.PaymentId,
                        PaymentProviderId = payment.PaymentProviderId,
                        ShowtimeCancellationId = showtime?.ShowtimeCancellation?.ShowtimeCancellationId,
                        RefundAmount = payment.Amount,
                        RefundStatus = BookingConstants.RefundStatus.Pending,
                        RefundReason = "Payment succeeded after booking or showtime cancellation.",
                        RequestedAt = DateTime.UtcNow
                    };
                    _db.Refunds.Add(refund);
                    if (!string.IsNullOrWhiteSpace(booking.CustomerProfileId))
                    {
                        claimIssue = _refundClaimIssuer.Create(
                            refund.RefundId,
                            booking.CustomerProfileId,
                            DateTime.UtcNow);
                        _db.RefundClaims.Add(claimIssue.Claim);
                    }
                }

                await _db.SaveChangesAsync(cancellationToken);
                await tx.CommitAsync(cancellationToken);
                if (claimIssue is not null
                    && booking.CustomerProfile?.User is not null)
                {
                    await TrySendLatePaymentClaimEmailAsync(
                        booking.CustomerProfile.User.Email,
                        showtime?.Movie.Title ?? "cancelled showtime",
                        claimIssue,
                        cancellationToken);
                }

                return;
            }

            if (string.Equals(booking.BookingStatus, "PENDING_PAYMENT", StringComparison.OrdinalIgnoreCase))
            {
                booking.BookingStatus = "PAID";
            }

            foreach (var bookingSeat in booking.BookingSeats)
            {
                bookingSeat.ShowtimeSeat.SeatStatus = "BOOKED";
                bookingSeat.ShowtimeSeat.LockedUntil = null;
                bookingSeat.ShowtimeSeat.LockedByUserId = null;

                if (bookingSeat.Ticket == null)
                {
                    _db.Tickets.Add(new Ticket
                    {
                        TicketId = GenerateId("TCK"),
                        BookingSeatId = bookingSeat.BookingSeatId,
                        QrCode = GenerateTicketQrCode(booking.BookingId, bookingSeat.BookingSeatId),
                        TicketStatus = "UNUSED",
                        GeneratedAt = DateTime.UtcNow
                    });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string GenerateId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

    private static bool IsCancelledOrRefundFlow(Booking booking, Showtime? showtime)
    {
        return IsStatus(showtime?.Status, BookingConstants.ShowtimeStatus.Cancelled)
            || IsStatus(booking.BookingStatus, BookingConstants.BookingStatus.Cancelled)
            || IsStatus(booking.BookingStatus, BookingConstants.BookingStatus.RefundPending)
            || IsStatus(booking.BookingStatus, BookingConstants.BookingStatus.Refunded);
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

    private async Task TrySendLatePaymentClaimEmailAsync(
        string email,
        string movieTitle,
        RefundClaimIssue issue,
        CancellationToken cancellationToken)
    {
        try
        {
            var link = $"{_refundSettings.FrontendBaseUrl.TrimEnd('/')}/refunds/claim?t={Uri.EscapeDataString(issue.RawToken)}";
            await _emailSender.SendEmailAsync(
                email,
                "Refund information required",
                $"A late payment was received for the cancelled showtime {movieTitle}. "
                + $"Submit bank information before {issue.Token.ExpiresAt:O}: {link}",
                cancellationToken);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Late-payment refund claim email could not be sent.");
        }
    }

    private static string GenerateTicketQrCode(string bookingId, string bookingSeatId) =>
        $"G2C|{bookingId}|{bookingSeatId}|{Guid.NewGuid():N}";

    private static string GenerateTransactionCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var sb = new System.Text.StringBuilder();
        sb.Append('T');
        for (int i = 0; i < 10; i++) sb.Append(chars[RandomNumberGenerator.GetInt32(chars.Length)]);
        return sb.ToString();
    }
}
