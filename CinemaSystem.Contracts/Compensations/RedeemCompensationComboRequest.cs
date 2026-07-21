using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Compensations;

public sealed class RedeemCompensationComboRequest
{
    [Required]
    [MaxLength(100)]
    public string VoucherCode { get; init; } = string.Empty;
}

public sealed class RedeemCompensationComboResponse
{
    public string CompensationComboId { get; init; } = string.Empty;

    public string VoucherCode { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public string CinemaId { get; init; } = string.Empty;

    public DateTime RedeemedAt { get; init; }
}
