namespace CinemaSystem.Domain.Entities;

public sealed class BankDirectory
{
    public string BankCode { get; set; } = null!;
    public string BankBin { get; set; } = null!;
    public string ShortName { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public bool IsActive { get; set; }
    public bool SupportsAccountInquiry { get; set; }
    public bool SupportsPayout { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }

    public ICollection<RefundClaim> RefundClaims { get; set; } = new List<RefundClaim>();
}
