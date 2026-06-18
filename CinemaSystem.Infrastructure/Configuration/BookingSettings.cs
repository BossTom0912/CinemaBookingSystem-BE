namespace CinemaSystem.Infrastructure.Configuration;

public sealed class BookingSettings
{
    public int OnlineSaleCutoffMinutes { get; set; } = 15;

    public int MaxSeatsPerCheckout { get; set; } = 10;

    public int PendingPaymentExpiryMinutes { get; set; } = 10;

    public int PendingPaymentCleanupIntervalSeconds { get; set; } = 60;
}
