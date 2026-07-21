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

    public int RefundsSucceeded { get; init; }

    public int RefundsManualRequired { get; init; }

    public int RefundsPending { get; init; }

    public int PaidBookingsCompensated { get; init; }

    public int TicketVouchersIssued { get; init; }

    public int ComboVouchersIssued { get; init; }
}
