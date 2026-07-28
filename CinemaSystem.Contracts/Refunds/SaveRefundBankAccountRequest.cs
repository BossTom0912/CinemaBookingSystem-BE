using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Refunds;

public sealed class SaveRefundBankAccountRequest
{
    // Kept as BankCode for API compatibility. The value is now a customer-entered
    // bank name and is not matched against BANK_DIRECTORY.
    [Required, StringLength(RefundContractConstants.RefundBankNameMaxLength)]
    public string BankCode { get; init; } = string.Empty;

    [Required, RegularExpression(RefundContractConstants.AccountNumberPattern)]
    public string AccountNumber { get; init; } = string.Empty;

    [Required, StringLength(
        RefundContractConstants.AccountHolderNameMaxLength,
        MinimumLength = RefundContractConstants.AccountHolderNameMinLength)]
    public string AccountHolderName { get; init; } = string.Empty;
}
