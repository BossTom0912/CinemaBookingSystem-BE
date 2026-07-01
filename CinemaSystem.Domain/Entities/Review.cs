using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class Review
{
    public string ReviewId { get; set; } = null!;

    public string CustomerProfileId { get; set; } = null!;

    public string MovieId { get; set; } = null!;

    public string? BookingId { get; set; }

    public int Rating { get; set; }

    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; }

    public string Status { get; set; } = null!;

    public int EditCount { get; set; }

    public string? RejectedReason { get; set; }

    public string? ModeratedBy { get; set; }

    public virtual Booking? Booking { get; set; }

    public virtual CustomerProfile CustomerProfile { get; set; } = null!;

    public virtual Movie Movie { get; set; } = null!;

    public virtual ICollection<ReviewEditHistory> EditHistories { get; set; } = new List<ReviewEditHistory>();

    public virtual ICollection<ReviewModerationHistory> ModerationHistories { get; set; } = new List<ReviewModerationHistory>();
}
