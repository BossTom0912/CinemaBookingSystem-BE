namespace CinemaSystem.Domain.Entities;

public sealed class RefundClaimToken
{
    public string RefundClaimTokenId { get; set; } = null!;
    public string RefundClaimId { get; set; } = null!;
    public string TokenHash { get; set; } = null!;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public DateTime? RevokedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public RefundClaim RefundClaim { get; set; } = null!;
}
