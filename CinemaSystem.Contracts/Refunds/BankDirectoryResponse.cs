namespace CinemaSystem.Contracts.Refunds;

public sealed class BankDirectoryResponse
{
    public string BankCode { get; init; } = string.Empty;
    public string BankBin { get; init; } = string.Empty;
    public string ShortName { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public bool SupportsAccountInquiry { get; init; }
    public bool SupportsPayout { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
}
