namespace CinemaSystem.Contracts.Refunds;

public sealed class RefundProcessingResponse
{
    public string RefundId { get; init; } = string.Empty;

    public string RefundStatus { get; init; } = string.Empty;

    public string BookingStatus { get; init; } = string.Empty;

    public string? ProviderRefundCode { get; init; }

    public string? FailureReason { get; init; }

    public int RewardPointsReverted { get; init; }

    public bool AlreadyProcessed { get; init; }
}
