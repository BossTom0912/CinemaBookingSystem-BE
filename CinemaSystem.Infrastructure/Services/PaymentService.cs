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

namespace CinemaSystem.Infrastructure.Services;

/// <summary>
/// Runtime SePay payment and payment-confirmation implementation reached from
/// <c>PaymentController</c> through <see cref="IPaymentService"/>.
/// </summary>
/// <remarks>
/// CreatePayment validates booking ownership/provider state and writes PAYMENT.
/// ConfirmPayment is called after <c>PaymentWebhookService</c> verifies the
/// webhook; in one database transaction it marks PAYMENT successful, marks the
/// BOOKING paid, converts SHOWTIME_SEAT locks to booked seats and creates one
/// TICKET per booking seat. The confirmation path is idempotent for an already
/// successful payment.
/// </remarks>
public class PaymentService : IPaymentService
{
    private readonly CinemaDbContext _db;
    private readonly SepaySettings _sepaySettings;
    private const int PaymentExpiryMinutes = 10;
    private static readonly Regex TransactionCodeRegex = new(@"T[A-Z0-9]{10}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public PaymentService(CinemaDbContext db, IOptions<SepaySettings> sepayOptions)
    {
        _db = db;
        _sepaySettings = sepayOptions.Value;
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

        // Không gọi cổng thanh toán ở đây. DTO quay về PaymentController để
        // frontend hiển thị thông tin chuyển khoản; chặng tiếp theo đến từ SePay
        // qua PaymentWebhookService khi giao dịch thực sự phát sinh.
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
            .Include(p => p.Booking)
                .ThenInclude(b => b.BookingSeats)
                    .ThenInclude(bs => bs.ShowtimeSeat)
            .Include(p => p.Booking)
                .ThenInclude(b => b.BookingSeats)
                    .ThenInclude(bs => bs.Ticket)
            .SingleOrDefaultAsync(p => p.TransactionCode == transactionCode, cancellationToken);

        if (payment == null)
            throw new InvalidOperationException($"Payment with transaction code {transactionCode} not found.");

        // Validate amount
        if (payment.Amount != amount)
            throw new InvalidOperationException($"Payment amount mismatch. Expected {payment.Amount} got {amount}.");

        // If already success - idempotent
        if (string.Equals(payment.PaymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase))
            return;

        // Chặng tiếp theo là transaction EF Core tại Persistence/CinemaDbContext.
        // Một callback phải cập nhật payment, booking, seat và ticket nguyên tử;
        // lỗi ở bất kỳ bước nào sẽ rollback toàn bộ trạng thái.
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

            // Commit xong, luồng quay về PaymentWebhookService rồi
            // PaymentController để ACK webhook. Không có email/e-ticket sender
            // kế tiếp trên main; đó là phần use case còn thiếu đã ghi trong docs.
        }
        catch
        {
            await tx.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static string GenerateId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

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
