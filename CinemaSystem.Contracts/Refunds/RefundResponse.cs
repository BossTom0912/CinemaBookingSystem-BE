namespace CinemaSystem.Contracts.Refunds;

public sealed class RefundResponse
{
    public string RefundId { get; init; } = string.Empty;

    public string BookingId { get; init; } = string.Empty;

    public string PaymentId { get; init; } = string.Empty;

    public string ShowtimeId { get; init; } = string.Empty;

    public string MovieTitle { get; init; } = string.Empty;

    public string CinemaId { get; init; } = string.Empty;

    public string CinemaName { get; init; } = string.Empty;

    public decimal RefundAmount { get; init; }

    public string RefundStatus { get; init; } = string.Empty;

    public string? RefundReason { get; init; }

    public string? FailureReason { get; init; }

    public DateTime RequestedAt { get; init; }

    public DateTime? RefundedAt { get; init; }
}
