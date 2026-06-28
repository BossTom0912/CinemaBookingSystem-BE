using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Reviews;

public class CreateReviewRequest
{
    [Required]
    public string MovieId { get; set; } = null!;

    public string? BookingId { get; set; }

    [Required]
    [Range(0, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}
