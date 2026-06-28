using CinemaSystem.Application.Common;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Services;

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
        await CleanupExpiredBookingsAsync(stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalSeconds = Math.Max(10, _settings.Value.PendingPaymentCleanupIntervalSeconds);

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                await CleanupExpiredBookingsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Pending payment cleanup failed.");
            }
        }
    }

    private async Task CleanupExpiredBookingsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var now = DateTime.UtcNow;
        var expiryMinutes = Math.Max(1, _settings.Value.PendingPaymentExpiryMinutes);
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
            .Take(100)
            .ToListAsync(cancellationToken);

        if (expiredBookings.Count == 0)
        {
            return;
        }

        foreach (var booking in expiredBookings)
        {
            foreach (var bookingSeat in booking.BookingSeats.ToList())
            {
                bookingSeat.ShowtimeSeat.SeatStatus = BookingConstants.ShowtimeSeatStatus.Available;
                bookingSeat.ShowtimeSeat.LockedUntil = null;
                bookingSeat.ShowtimeSeat.LockedByUserId = null;
            }

            dbContext.BookingSeats.RemoveRange(booking.BookingSeats);

            if (booking.VoucherUsage is not null)
            {
                booking.VoucherUsage.UsageStatus = "CANCELLED";
            }

            foreach (var payment in booking.Payments)
            {
                if (payment.PaymentStatus == "PENDING")
                {
                    payment.PaymentStatus = "EXPIRED";
                    payment.UpdatedAt = now;
                }
            }

            booking.BookingStatus = BookingConstants.BookingStatus.Cancelled;
            booking.ExpiredAt = now;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Cleaned up {Count} expired pending payment booking(s).",
            expiredBookings.Count);
    }
}
