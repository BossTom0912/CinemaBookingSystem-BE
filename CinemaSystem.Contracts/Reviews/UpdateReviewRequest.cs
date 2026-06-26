using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Reviews;

public class UpdateReviewRequest
{
    [Range(0, 5)]
    public int Rating { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }
}
