using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Refunds;

public sealed class ManualRefundConfirmationRequest
{
    [Required, StringLength(
        RefundContractConstants.BankTransactionCodeMaxLength,
        MinimumLength = RefundContractConstants.BankTransactionCodeMinLength)]
    public string BankTransactionCode { get; init; } = string.Empty;

    [Range(
        typeof(decimal),
        RefundContractConstants.MinimumRefundAmount,
        RefundContractConstants.MaximumRefundAmount)]
    public decimal TransferredAmount { get; init; }

    [Required, StringLength(
        RefundContractConstants.ProofUrlMaxLength,
        MinimumLength = RefundContractConstants.ProofUrlMinLength)]
    public string ProofUrl { get; init; } = string.Empty;

    [StringLength(RefundContractConstants.NoteMaxLength)]
    public string? Note { get; init; }
}
