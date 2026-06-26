using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Refunds;

public sealed class ManualRefundConfirmationRequest
{
    [Required, StringLength(255, MinimumLength = 3)]
    public string BankTransactionCode { get; init; } = string.Empty;

    [Range(typeof(decimal), "0.01", "9999999999999999")]
    public decimal TransferredAmount { get; init; }

    [Required, StringLength(1000, MinimumLength = 3)]
    public string ProofUrl { get; init; } = string.Empty;

    [StringLength(1000)]
    public string? Note { get; init; }
}
