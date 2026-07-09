namespace CinemaSystem.Contracts.Refunds;

public sealed class RefundClaimResponse
{
    public string RefundClaimId { get; init; } = string.Empty;
    public string RefundId { get; init; } = string.Empty;
    public string BookingId { get; init; } = string.Empty;
    public string ClaimStatus { get; init; } = string.Empty;
    public string RefundStatus { get; init; } = string.Empty;
    public decimal RefundAmount { get; init; }
    public string MovieTitle { get; init; } = string.Empty;
    public string CinemaName { get; init; } = string.Empty;
    public DateTime ShowtimeStartTime { get; init; }
    public string? BankCode { get; init; }
    public string? BankName { get; init; }
    public string? MaskedAccountNumber { get; init; }
    public string? AccountHolderName { get; init; }
    public DateTime ExpiresAt { get; init; }
    public DateTime? SubmittedAt { get; init; }
}
