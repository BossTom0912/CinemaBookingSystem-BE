using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Payments;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CinemaSystem.Infrastructure.Configuration;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace CinemaSystem.Infrastructure.Services;

public class PaymentService : IPaymentService
{
    private readonly CinemaDbContext _db;
    private readonly SepaySettings _sepaySettings;
    private static readonly Regex TransactionCodeRegex = new(@"T[A-Z0-9]{10}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public PaymentService(CinemaDbContext db, IOptions<SepaySettings> sepayOptions)
    {
        _db = db;
        _sepaySettings = sepayOptions.Value;
    }

    // Create payment record for a booking and return bank info + transaction code
    public async Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, CancellationToken cancellationToken = default)
    {
        var bookingId = request.BookingId.Trim();
        var paymentProviderId = request.PaymentProviderId.Trim();

        if (string.IsNullOrWhiteSpace(bookingId))
            throw new ArgumentException("BookingId must be provided.", nameof(request));
        if (string.IsNullOrWhiteSpace(paymentProviderId))
            throw new ArgumentException("PaymentProviderId must be provided.", nameof(request));

        // Find booking
        var booking = await _db.Bookings
            .Include(item => item.Payments)
            .SingleOrDefaultAsync(b => b.BookingId == bookingId, cancellationToken);
        if (booking == null)
            throw new InvalidOperationException($"Booking {bookingId} not found.");

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

        var pendingPayment = booking.Payments
            .Where(item =>
                item.PaymentProviderId == provider.PaymentProviderId
                && string.Equals(item.PaymentStatus, "PENDING", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefault();
        if (pendingPayment != null)
        {
            return ToCreatePaymentResponse(pendingPayment);
        }

        // Create payment
        var payment = new Payment
        {
            PaymentId = GenerateId("PAY"),
            BookingId = booking.BookingId,
            PaymentProviderId = provider.PaymentProviderId,
            Amount = booking.TotalAmount,
            TransactionCode = GenerateTransactionCode(),
            PaymentStatus = "PENDING",
            CreatedAt = DateTime.UtcNow,
            PaymentMethod = "SEPAY"
        };

        _db.Payments.Add(payment);
        await _db.SaveChangesAsync(cancellationToken);

        return ToCreatePaymentResponse(payment);
    }

    private CreatePaymentResponse ToCreatePaymentResponse(Payment payment)
    {
        return new CreatePaymentResponse
        {
            PaymentId = payment.PaymentId,
            Amount = payment.Amount,
            TransactionCode = payment.TransactionCode ?? string.Empty,
            BankName = _sepaySettings.BankName,
            BankAccount = _sepaySettings.BankAccount
        };
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
            .Include(p => p.Booking)
            .SingleOrDefaultAsync(p => p.TransactionCode == transactionCode, cancellationToken);

        if (payment == null)
            throw new InvalidOperationException($"Payment with transaction code {transactionCode} not found.");

        // Validate amount
        if (payment.Amount != amount)
            throw new InvalidOperationException($"Payment amount mismatch. Expected {payment.Amount} got {amount}.");

        // If already success - idempotent
        if (string.Equals(payment.PaymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            return;

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

            if (string.Equals(booking.BookingStatus, "PENDING_PAYMENT", StringComparison.OrdinalIgnoreCase))
            {
                booking.BookingStatus = "PAID";
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

    private static string GenerateTransactionCode()
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var sb = new System.Text.StringBuilder();
        sb.Append('T');
        for (int i = 0; i < 10; i++) sb.Append(chars[RandomNumberGenerator.GetInt32(chars.Length)]);
        return sb.ToString();
    }
}
