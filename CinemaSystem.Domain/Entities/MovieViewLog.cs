using System;

namespace CinemaSystem.Domain.Entities;

public partial class MovieViewLog
{
    public string MovieViewLogId { get; set; } = null!;

    public string MovieId { get; set; } = null!;

    public string? UserId { get; set; }

    public DateTime ViewedAt { get; set; }

    public string? IpAddress { get; set; }

    public virtual Movie Movie { get; set; } = null!;
}
