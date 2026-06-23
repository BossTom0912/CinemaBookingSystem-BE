using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Showtimes;

public sealed class CancelShowtimeRequest
{
    [Required]
    [MaxLength(1000)]
    public string Reason { get; init; } = string.Empty;
}
