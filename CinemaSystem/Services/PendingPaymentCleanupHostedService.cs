using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Services;

/// <summary>
/// Background continuation of the booking lifecycle for abandoned payments.
/// </summary>
/// <remarks>
/// Registered by <c>Program.cs</c>. On startup and at the configured interval,
/// it creates a DI scope, resolves <c>CinemaDbContext</c>, finds expired
/// PENDING_PAYMENT bookings, releases SHOWTIME_SEAT locks and removes dependent
/// voucher/F&amp;B/payment/booking rows in one EF unit of work.
/// </remarks>
public sealed class PendingPaymentCleanupHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<BookingSettings> _settings;
    private readonly ILogger<PendingPaymentCleanupHostedService> _logger;

    public PendingPaymentCleanupHostedService(
        IServiceScopeFactory scopeFactory,
        IOptions<BookingSettings> settings,
        ILogger<PendingPaymentCleanupHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _settings = settings;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await TryCleanupExpiredBookingsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalSeconds = _settings.Value.PendingPaymentCleanupIntervalSeconds;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                await TryCleanupExpiredBookingsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task TryCleanupExpiredBookingsAsync(CancellationToken cancellationToken)
    {
        try
        {
            await CleanupExpiredBookingsAsync(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Pending payment cleanup failed.");
        }
    }

    private async Task CleanupExpiredBookingsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<IClock>();
        var now = clock.UtcNow;
        var expiryMinutes = _settings.Value.PendingPaymentExpiryMinutes;
        var createdBefore = now.AddMinutes(-expiryMinutes);

        var expiredBookings = await dbContext.Bookings
            .Include(item => item.BookingSeats)
                .ThenInclude(item => item.ShowtimeSeat)
            .Include(item => item.BookingFbItems)
            .Include(item => item.Payments)
            .Include(item => item.VoucherUsage)
            .Where(item =>
                item.BookingStatus == BookingConstants.BookingStatus.PendingPayment &&
                ((item.ExpiredAt.HasValue && item.ExpiredAt <= now) ||
                 (!item.ExpiredAt.HasValue && item.CreatedAt <= createdBefore)))
            .OrderBy(item => item.ExpiredAt ?? item.CreatedAt)
            .Take(_settings.Value.PendingPaymentCleanupBatchSize)
            .ToListAsync(cancellationToken);

        if (expiredBookings.Count == 0)
        {
            return;
        }

        foreach (var booking in expiredBookings)
        {
            foreach (var bookingSeat in booking.BookingSeats)
            {
                bookingSeat.ShowtimeSeat.SeatStatus = BookingConstants.ShowtimeSeatStatus.Available;
                bookingSeat.ShowtimeSeat.LockedUntil = null;
                bookingSeat.ShowtimeSeat.LockedByUserId = null;
            }

            if (booking.VoucherUsage is not null)
            {
                booking.VoucherUsage.UsageStatus = DomainConstants.VoucherUsageStatus.Cancelled;
            }

            foreach (var payment in booking.Payments)
            {
                if (payment.PaymentStatus == DomainConstants.PaymentStatus.Pending)
                {
                    payment.PaymentStatus = DomainConstants.PaymentStatus.Expired;
                }
            }

            booking.BookingStatus = DomainConstants.BookingStatus.Cancelled;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Chu kỳ xử lý kết thúc tại đây và quay về ExecuteAsync để đợi interval
        // tiếp theo. Không đi qua Controller vì đây là background use case do
        // Program.cs khởi động, không phải HTTP request.
        _logger.LogInformation(
            "Cleaned up {Count} expired pending payment booking(s).",
            expiredBookings.Count);
    }
}
