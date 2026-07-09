namespace CinemaSystem.Contracts.Refunds;

public sealed class ManualRefundResponse
{
    public string RefundId { get; init; } = string.Empty;
    public string BookingId { get; init; } = string.Empty;
    public string RefundClaimId { get; init; } = string.Empty;
    public string RefundStatus { get; init; } = string.Empty;
    public string ClaimStatus { get; init; } = string.Empty;
    public string ProcessStatus { get; init; } = string.Empty;
    public decimal RefundAmount { get; init; }
    public string MovieTitle { get; init; } = string.Empty;
    public string CinemaName { get; init; } = string.Empty;
    public DateTime ShowtimeStartTime { get; init; }
    public string BankCode { get; init; } = string.Empty;
    public string BankName { get; init; } = string.Empty;
    public string AccountNumber { get; init; } = string.Empty;
    public string AccountHolderName { get; init; } = string.Empty;
    public string? AssignedToUserId { get; init; }
    public string? BankTransactionCode { get; init; }
    public string? ProofUrl { get; init; }
    public DateTime RequestedAt { get; init; }
    public DateTime? ConfirmedAt { get; init; }
}
