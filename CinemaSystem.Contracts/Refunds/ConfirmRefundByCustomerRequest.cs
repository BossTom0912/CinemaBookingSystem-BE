using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Refunds;

public sealed class ConfirmRefundByCustomerRequest
{
    [Required]
    [StringLength(512)]
    public string Token { get; init; } = string.Empty;
}
