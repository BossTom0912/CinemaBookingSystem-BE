using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;

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
        try
        {
            await CleanupExpiredBookingsAsync(stoppingToken);
            await CleanupExpiredUnstableBookingsAsync(stoppingToken);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Initial pending payment cleanup failed.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            var intervalSeconds = _settings.Value.PendingPaymentCleanupIntervalSeconds;

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
                await CleanupExpiredBookingsAsync(stoppingToken);
                await CleanupExpiredUnstableBookingsAsync(stoppingToken);
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

    private async Task CleanupExpiredUnstableBookingsAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var now = DateTime.UtcNow;
        var twoHoursLater = now.AddHours(2);

        // Lấy tất cả Booking đang ở trạng thái ProcessingUnstable và thời gian chiếu bắt đầu trong vòng 2 tiếng nữa
        var unstableBookings = await dbContext.Bookings
            .Include(item => item.BookingSeats)
                .ThenInclude(item => item.ShowtimeSeat)
            .Include(item => item.Showtime)
                .ThenInclude(s => s.Movie)
            .Include(item => item.Payments)
            .Include(item => item.CustomerProfile)
                .ThenInclude(cp => cp.User)
            .Where(item =>
                item.BookingStatus == "ProcessingUnstable" &&
                item.Showtime.StartTime <= twoHoursLater)
            .ToListAsync(cancellationToken);

        if (unstableBookings.Count == 0)
        {
            return;
        }

        foreach (var booking in unstableBookings)
        {
            // Giải phóng ghế
            foreach (var bookingSeat in booking.BookingSeats)
            {
                bookingSeat.ShowtimeSeat.SeatStatus = "AVAILABLE";
                bookingSeat.ShowtimeSeat.LockedUntil = null;
                bookingSeat.ShowtimeSeat.LockedByUserId = null;
            }

            // Tạo yêu cầu hoàn tiền tự động (Refund)
            var payment = booking.Payments.FirstOrDefault(p => p.PaymentStatus == "SUCCESS") ?? booking.Payments.FirstOrDefault();
            if (payment != null)
            {
                var refund = new Refund
                {
                    RefundId = "REF_" + Guid.NewGuid().ToString("N"),
                    BookingId = booking.BookingId,
                    PaymentId = payment.PaymentId,
                    PaymentProviderId = payment.PaymentProviderId,
                    RefundAmount = booking.TotalAmount,
                    RefundStatus = "PENDING",
                    RefundReason = "Token expired 2 hours before showtime. System auto-refund initiated.",
                    RequestedAt = now
                };
                dbContext.Refunds.Add(refund);
            }

            booking.BookingStatus = "CANCELLED";
            
            // Gửi email thông báo hủy vé tự động cho người dùng thông qua IEmailService (sử dụng Hangfire)
            var emailService = scope.ServiceProvider.GetService<IEmailService>();
            if (emailService != null)
            {
                var customerEmail = booking.CustomerProfile?.User?.Email ?? booking.GuestEmail;
                if (!string.IsNullOrEmpty(customerEmail))
                {
                    var subject = "Thông báo hủy vé và hoàn tiền tự động / Auto Ticket Cancellation and Refund Notice";
                    var body = $"[VI] Suất chiếu của phim {booking.Showtime.Movie?.Title ?? "phim đã đặt"} sắp bắt đầu nhưng chúng tôi chưa nhận được xác nhận thay đổi của bạn. Vé của bạn đã được hủy tự động và đang tiến hành hoàn tiền.\n\n[EN] The showtime for {booking.Showtime.Movie?.Title ?? "your booking"} is starting soon but we have not received your confirmation. Your ticket has been automatically cancelled and a refund is being processed.";
                    
                    var bgJobClient = scope.ServiceProvider.GetService<IBackgroundJobClient>();
                    if (bgJobClient != null)
                    {
                        bgJobClient.Enqueue<IEmailService>(email => email.SendEmailAsync(customerEmail, subject, body, CancellationToken.None));
                    }
                }
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Cleaned up {Count} expired unstable booking(s) and initiated auto-refunds.", unstableBookings.Count);
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
