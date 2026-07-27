using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Refunds;

public sealed class UpsertBankDirectoryRequest
{
    [Required]
    [StringLength(RefundContractConstants.BankBinMaxLength)]
    public string BankBin { get; init; } = string.Empty;

    [Required]
    [StringLength(RefundContractConstants.BankShortNameMaxLength)]
    public string ShortName { get; init; } = string.Empty;

    [Required]
    [StringLength(RefundContractConstants.BankFullNameMaxLength)]
    public string FullName { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;

    public bool SupportsAccountInquiry { get; init; }

    public bool SupportsPayout { get; init; }
}
