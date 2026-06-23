namespace CinemaSystem.Contracts.Showtimes;

public sealed class CancelShowtimeResponse
{
    public string ShowtimeId { get; init; } = string.Empty;

    public string ShowtimeStatus { get; init; } = string.Empty;

    public string ShowtimeCancellationId { get; init; } = string.Empty;

    public int PaidBookingsMovedToRefundPending { get; init; }

    public int UnpaidBookingsCancelled { get; init; }

    public int RefundsCreated { get; init; }

    public decimal TotalRefundAmount { get; init; }
}
