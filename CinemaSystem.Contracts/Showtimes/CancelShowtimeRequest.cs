using System.ComponentModel.DataAnnotations;
using CinemaSystem.Contracts.Refunds;

namespace CinemaSystem.Contracts.Showtimes;

public sealed class CancelShowtimeRequest
{
    [Required]
    [MaxLength(RefundContractConstants.CancellationReasonMaxLength)]
    public string Reason { get; init; } = string.Empty;
}
