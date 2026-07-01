using System;

namespace CinemaSystem.Contracts.Reviews;

public class ReviewResponse
{
    public string ReviewId { get; set; } = null!;
    public string CustomerProfileId { get; set; } = null!;
    public string MovieId { get; set; } = null!;
    public string? BookingId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = null!;
    public string? CustomerName { get; set; }
    public string? MovieTitle { get; set; }
    public string? RejectedReason { get; set; }
    public string? ModeratedBy { get; set; }
}
