using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Refunds;

public sealed class SaveRefundBankAccountRequest
{
    [Required, StringLength(RefundContractConstants.BankCodeMaxLength)]
    public string BankCode { get; init; } = string.Empty;

    [Required, RegularExpression(RefundContractConstants.AccountNumberPattern)]
    public string AccountNumber { get; init; } = string.Empty;

    [Required, StringLength(
        RefundContractConstants.AccountHolderNameMaxLength,
        MinimumLength = RefundContractConstants.AccountHolderNameMinLength)]
    public string AccountHolderName { get; init; } = string.Empty;
}
