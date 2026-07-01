using System;

namespace CinemaSystem.Domain.Entities;

public partial class MovieDailyView
{
    public string MovieId { get; set; } = null!;
    public DateOnly ViewDate { get; set; }
    public int ViewCount { get; set; }

    public virtual Movie Movie { get; set; } = null!;
}
