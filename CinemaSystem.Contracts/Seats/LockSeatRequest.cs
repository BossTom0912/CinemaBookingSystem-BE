using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Seats;

public sealed class LockSeatRequest
{
    [Required]
    public string ShowtimeId { get; init; } = string.Empty;

    [Required]
    public string SeatId { get; init; } = string.Empty;
}
