using System.ComponentModel.DataAnnotations;
using CinemaSystem.Domain.Constants;

namespace CinemaSystem.Contracts.Showtimes;

public sealed class UpdateShowtimeRequest
{
    [Required]
    [MaxLength(50)]
    public string MovieId { get; init; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string RoomId { get; init; } = string.Empty;

    [Required]
    public DateTime StartTime { get; init; }

    [Range(0, 999999999)]
    public decimal BasePrice { get; init; }

    [MaxLength(30)]
    public string Status { get; init; } = DomainConstants.ShowtimeStatus.Open;

    public string? CompensationVoucherCode { get; init; }
    public string? CompensationNote { get; init; }
}
