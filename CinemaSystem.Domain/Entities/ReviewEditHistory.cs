using System;

namespace CinemaSystem.Domain.Entities;

public partial class ReviewEditHistory
{
    public string ReviewEditHistoryId { get; set; } = null!;
    public string ReviewId { get; set; } = null!;
    public int OldRating { get; set; }
    public int NewRating { get; set; }
    public string? OldComment { get; set; }
    public string? NewComment { get; set; }
    public DateTime EditedAt { get; set; }

    public virtual Review Review { get; set; } = null!;
}
