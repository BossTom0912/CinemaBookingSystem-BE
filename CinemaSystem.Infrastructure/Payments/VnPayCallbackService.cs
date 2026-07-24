using System.Globalization;
using System.Text.Json;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Payments;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CinemaSystem.Infrastructure.Payments;

public sealed class VnPayCallbackService : IVnPayCallbackService
{
    private const decimal AmountDivisor = 100m;
    private const string SuccessCode = "00";

    private readonly CinemaDbContext _db;
    private readonly VnPayGateway _gateway;
    private readonly IPaymentService _paymentService;
    private readonly ISeatLockStore _seatLockStore;
    private readonly IClock _clock;
    private readonly ILogger<VnPayCallbackService> _logger;

    public VnPayCallbackService(
        CinemaDbContext db,
        VnPayGateway gateway,
        IPaymentService paymentService,
        ISeatLockStore seatLockStore,
        IClock clock,
        ILogger<VnPayCallbackService> logger)
    {
        _db = db;
        _gateway = gateway;
        _paymentService = paymentService;
        _seatLockStore = seatLockStore;
        _clock = clock;
        _logger = logger;
    }

    public bool HasValidSignature(IReadOnlyDictionary<string, string> parameters) =>
        _gateway.HasValidSignature(parameters);

    public async Task<VnPayIpnResponse> HandleIpnAsync(
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default)
    {
        if (!HasValidSignature(parameters))
        {
            return Response("97", "Invalid checksum");
        }

        if (!TryGetRequired(parameters, "vnp_TxnRef", out var transactionCode)
            || !TryGetAmount(parameters, out var amount))
        {
            return Response("99", "Invalid request");
        }

        var payment = await _db.Payments
            .Include(item => item.PaymentProvider)
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.TransactionCode == transactionCode,
                cancellationToken);

        if (payment is null
            || !string.Equals(
                payment.PaymentProvider.ProviderName,
                DomainConstants.PaymentProvider.VnPayName,
                StringComparison.OrdinalIgnoreCase))
        {
            return Response("01", "Order not found");
        }

        if (payment.Amount != amount)
        {
            return Response("04", "Invalid amount");
        }

        if (string.Equals(
            payment.PaymentStatus,
            DomainConstants.PaymentStatus.Success,
            StringComparison.OrdinalIgnoreCase))
        {
            return Response("02", "Order already confirmed");
        }

        var responseCode = parameters.GetValueOrDefault("vnp_ResponseCode");
        var transactionStatus = parameters.GetValueOrDefault("vnp_TransactionStatus");
        var rawPayload = JsonSerializer.Serialize(parameters);

        if (!string.Equals(responseCode, SuccessCode, StringComparison.Ordinal)
            || !string.Equals(transactionStatus, SuccessCode, StringComparison.Ordinal))
        {
            await MarkFailedAndReleaseBookingAsync(
                transactionCode,
                responseCode ?? transactionStatus ?? "UNKNOWN",
                rawPayload,
                cancellationToken);
            return Response(SuccessCode, "Confirm success");
        }

        try
        {
            await _paymentService.ConfirmPaymentAsync(
                transactionCode,
                amount,
                parameters.GetValueOrDefault("vnp_TransactionNo"),
                rawPayload,
                cancellationToken);
            return Response(SuccessCode, "Confirm success");
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "VNPAY IPN processing failed for payment transaction {TransactionCode}.",
                transactionCode);
            return Response("99", "Unknown error");
        }
    }

    private async Task MarkFailedAndReleaseBookingAsync(
        string transactionCode,
        string providerResponseCode,
        string rawPayload,
        CancellationToken cancellationToken)
    {
        var payment = await _db.Payments
            .Include(item => item.Booking)
                .ThenInclude(item => item.BookingSeats)
                    .ThenInclude(item => item.ShowtimeSeat)
            .Include(item => item.Booking)
                .ThenInclude(item => item.VoucherUsage)
            .AsSplitQuery()
            .SingleAsync(
                item => item.TransactionCode == transactionCode,
                cancellationToken);

        if (!string.Equals(
            payment.PaymentStatus,
            DomainConstants.PaymentStatus.Pending,
            StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var now = _clock.UtcNow;
        payment.PaymentStatus = DomainConstants.PaymentStatus.Failed;
        payment.FailureReason = $"VNPAY response code: {providerResponseCode}";
        payment.RawCallbackPayload = rawPayload;
        payment.UpdatedAt = now;

        var booking = payment.Booking;
        booking.BookingStatus = DomainConstants.EntityStatus.Cancelled;
        booking.ExpiredAt = now;

        if (booking.VoucherUsage is not null
            && string.Equals(
                booking.VoucherUsage.UsageStatus,
                DomainConstants.VoucherUsageStatus.Applied,
                StringComparison.OrdinalIgnoreCase))
        {
            booking.VoucherUsage.UsageStatus = DomainConstants.VoucherUsageStatus.Cancelled;
        }

        foreach (var bookingSeat in booking.BookingSeats)
        {
            var showtimeSeat = bookingSeat.ShowtimeSeat;
            showtimeSeat.SeatStatus = DomainConstants.ShowtimeSeatStatus.Available;
            showtimeSeat.LockedUntil = null;
            showtimeSeat.LockedByUserId = null;

            await _seatLockStore.ReleaseAsync(
                DomainConstants.SeatLock.BuildKey(
                    showtimeSeat.ShowtimeId,
                    showtimeSeat.SeatId),
                cancellationToken);
        }

        _db.BookingSeats.RemoveRange(booking.BookingSeats);
        await _db.SaveChangesAsync(cancellationToken);
    }

    private static bool TryGetRequired(
        IReadOnlyDictionary<string, string> parameters,
        string key,
        out string value)
    {
        value = parameters.GetValueOrDefault(key)?.Trim() ?? string.Empty;
        return value.Length > 0;
    }

    private static bool TryGetAmount(
        IReadOnlyDictionary<string, string> parameters,
        out decimal amount)
    {
        amount = 0;
        return parameters.TryGetValue("vnp_Amount", out var rawAmount)
            && decimal.TryParse(
                rawAmount,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var providerAmount)
            && providerAmount > 0
            && (amount = providerAmount / AmountDivisor) > 0;
    }

    private static VnPayIpnResponse Response(string code, string message) =>
        new()
        {
            ResponseCode = code,
            Message = message
        };
}
