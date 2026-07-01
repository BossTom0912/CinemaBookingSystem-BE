using System;

namespace CinemaSystem.Domain.Entities;

public partial class ReviewModerationHistory
{
    public string ModerationHistoryId { get; set; } = null!;
    public string ReviewId { get; set; } = null!;
    public string? OldStatus { get; set; }
    public string NewStatus { get; set; } = null!;
    public string? ModeratorId { get; set; }
    public string? RejectedReason { get; set; }
    public DateTime ModeratedAt { get; set; }

    public virtual Review Review { get; set; } = null!;
}
