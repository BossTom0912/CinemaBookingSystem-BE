namespace CinemaSystem.Contracts.Refunds;

public sealed class RefundQueryRequest
{
    public string? Status { get; init; }

    public string? ShowtimeId { get; init; }

    public DateTime? From { get; init; }

    public DateTime? To { get; init; }
}
