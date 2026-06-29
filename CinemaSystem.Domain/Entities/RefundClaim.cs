namespace CinemaSystem.Domain.Entities;

public sealed class RefundClaim
{
    public string RefundClaimId { get; set; } = null!;
    public string RefundId { get; set; } = null!;
    public string CustomerProfileId { get; set; } = null!;
    public string? BankCode { get; set; }
    public string ClaimStatus { get; set; } = null!;
    public string AccountValidationStatus { get; set; } = null!;
    public byte[]? BankAccountEncrypted { get; set; }
    public string? BankAccountLast4 { get; set; }
    public byte[]? AccountHolderNameEncrypted { get; set; }
    public byte[]? VerifiedAccountHolderNameEncrypted { get; set; }
    public string? VerificationProvider { get; set; }
    public string? VerificationReferenceCode { get; set; }
    public string? VerificationFailureReason { get; set; }
    public DateTime ExpiresAt { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ProcessingAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public byte[] RowVersion { get; set; } = Array.Empty<byte>();

    public Refund Refund { get; set; } = null!;
    public CustomerProfile CustomerProfile { get; set; } = null!;
    public BankDirectory? Bank { get; set; }
    public ICollection<RefundClaimToken> Tokens { get; set; } = new List<RefundClaimToken>();
    public ManualRefundProcess? ManualRefundProcess { get; set; }
}
