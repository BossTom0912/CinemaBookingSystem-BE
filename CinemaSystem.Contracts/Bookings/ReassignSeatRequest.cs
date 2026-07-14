using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Bookings;

public class ReassignSeatRequest
{
    [Required]
    public string BookingId { get; set; } = null!;

    [Required]
    public string OldShowtimeSeatId { get; set; } = null!;

    [Required]
    public string NewShowtimeSeatId { get; set; } = null!;
}
