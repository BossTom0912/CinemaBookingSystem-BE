namespace CinemaSystem.Infrastructure.Configuration;

public sealed class BookingSettings
{
    public const string SectionName = "BookingSettings";

    public int OnlineSaleCutoffMinutes { get; set; } = 15;

    public int MaxSeatsPerCheckout { get; set; } = 10;

    public int PendingPaymentExpiryMinutes { get; set; } = 10;

    public int PendingPaymentCleanupIntervalSeconds { get; set; } = 60;

    public int PendingPaymentCleanupBatchSize { get; set; } = 100;
}
