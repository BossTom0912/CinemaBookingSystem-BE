using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Payments;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CinemaSystem.Infrastructure.Configuration;
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
        // Find booking
        var booking = await _db.Bookings.SingleOrDefaultAsync(b => b.BookingId == request.BookingId, cancellationToken);
        if (booking == null)
            throw new InvalidOperationException($"Booking {request.BookingId} not found.");

        // Ensure payment provider exists
        var provider = await _db.PaymentProviders.SingleOrDefaultAsync(p => p.PaymentProviderId == request.PaymentProviderId, cancellationToken);
        if (provider == null)
            throw new InvalidOperationException($"Payment provider {request.PaymentProviderId} not found.");

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

        return new CreatePaymentResponse
        {
            PaymentId = payment.PaymentId,
            Amount = payment.Amount,
            TransactionCode = payment.TransactionCode ?? string.Empty,
            BankName = _sepaySettings.BankName,
            BankAccount = _sepaySettings.BankAccount
        };
    }

    public async Task ConfirmPaymentAsync(string transactionContent, decimal amount, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(transactionContent))
            throw new ArgumentException("Transaction content must be provided.", nameof(transactionContent));

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
        // Simple transaction code generator matching the example pattern
        var rng = Random.Shared;
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        var sb = new System.Text.StringBuilder();
        sb.Append('T');
        for (int i = 0; i < 10; i++) sb.Append(chars[rng.Next(chars.Length)]);
        return sb.ToString();
    }
}
