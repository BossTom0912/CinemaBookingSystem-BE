using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class Movie
{
    public string MovieId { get; set; } = null!;

    public string Title { get; set; } = null!;

    public int DurationMinutes { get; set; }

    public string? Genre { get; set; }

    public string? Language { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    public string? AgeRating { get; set; }

    public string? Description { get; set; }

    public string? PosterUrl { get; set; }

    public string? TrailerUrl { get; set; }

    public string MovieStatus { get; set; } = null!;

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();
}
