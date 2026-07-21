namespace CinemaSystem.Domain.Entities;

public sealed class ManualRefundProcess
{
    public string ManualRefundProcessId { get; set; } = null!;
    public string RefundId { get; set; } = null!;
    public string RefundClaimId { get; set; } = null!;
    public string? AssignedToUserId { get; set; }
    public string ProcessStatus { get; set; } = null!;
    public string? BankTransactionCode { get; set; }
    public decimal? TransferredAmount { get; set; }
    public string? ProofUrl { get; set; }
    public string? AdminNote { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Refund Refund { get; set; } = null!;
    public RefundClaim RefundClaim { get; set; } = null!;
    public User? AssignedToUser { get; set; }
    public RefundCustomerConfirmation? CustomerConfirmation { get; set; }
}
