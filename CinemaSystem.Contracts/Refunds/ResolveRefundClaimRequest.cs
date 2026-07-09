using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Refunds;

public sealed class ResolveRefundClaimRequest
{
    [Required, StringLength(
        RefundContractConstants.ClaimTokenMaxLength,
        MinimumLength = RefundContractConstants.ClaimTokenMinLength)]
    public string Token { get; init; } = string.Empty;
}
