namespace CinemaSystem.Contracts.Refunds;

public sealed class AssignManualRefundResponse
{
    public string RefundId { get; init; } = string.Empty;
    public string ManualRefundProcessId { get; init; } = string.Empty;
    public string ProcessStatus { get; init; } = string.Empty;
    public string AssignedToUserId { get; init; } = string.Empty;
    public DateTime AssignedAt { get; init; }
}
