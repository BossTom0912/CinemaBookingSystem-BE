using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Refunds;

public sealed class ResolveRefundClaimRequest
{
    [Required, StringLength(500, MinimumLength = 20)]
    public string Token { get; init; } = string.Empty;
}
