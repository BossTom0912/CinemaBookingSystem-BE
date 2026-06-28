using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Refunds;

public sealed class SaveRefundBankAccountRequest
{
    [Required, StringLength(20)]
    public string BankCode { get; init; } = string.Empty;

    [Required, RegularExpression(@"^[0-9]{6,20}$")]
    public string AccountNumber { get; init; } = string.Empty;

    [Required, StringLength(255, MinimumLength = 2)]
    public string AccountHolderName { get; init; } = string.Empty;
}
