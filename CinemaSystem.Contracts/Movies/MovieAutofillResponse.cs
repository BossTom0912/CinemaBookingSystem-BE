using System.Collections.Generic;

namespace CinemaSystem.Contracts.Movies;

public class MovieAutofillResponse
{
    public string Title { get; set; } = string.Empty;
    public int DurationMinutes { get; set; }
    public List<string>? Genres { get; set; }
    public string? Language { get; set; }
    public string? ReleaseDate { get; set; } // yyyy-MM-dd
    public string? AgeRating { get; set; }
    public string? Description { get; set; }
    public string? Director { get; set; }
    public string? TrailerUrl { get; set; }
    public string? PosterUrl { get; set; }
    public string? BannerUrl { get; set; }
}
