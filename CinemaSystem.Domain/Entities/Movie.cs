using System;
using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class Movie
{
    public string MovieId { get; set; } = null!;

    public string Title { get; set; } = null!;

    public int DurationMinutes { get; set; }

    public string? LanguageId { get; set; }

    public virtual Language? Language { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    public string? AgeRating { get; set; }

    public string? Description { get; set; }

    public string? PosterUrl { get; set; }

    public string? TrailerUrl { get; set; }

    public string? BannerUrl { get; set; }

    public string? Director { get; set; }

    public string? Highlight { get; set; }

    public string MovieStatus { get; set; } = null!;

    public int ViewCount { get; set; }

    public decimal AverageRating { get; set; }

    public int TotalReviews { get; set; }

    public int TotalViews { get; set; }

    public int DailyViews { get; set; }

    public virtual ICollection<Review> Reviews { get; set; } = new List<Review>();

    public virtual ICollection<Showtime> Showtimes { get; set; } = new List<Showtime>();

    public virtual ICollection<MovieViewLog> MovieViewLogs { get; set; } = new List<MovieViewLog>();

    public virtual ICollection<MovieDailyView> MovieDailyViews { get; set; } = new List<MovieDailyView>();

    public virtual ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
}
