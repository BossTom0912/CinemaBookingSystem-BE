namespace CinemaSystem.Contracts.Movies;

public sealed class MovieDetailResponse
{
    public string MovieId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public int DurationMinutes { get; init; }

    public List<string>? Genres { get; init; }

    public string? Language { get; init; }

    public DateOnly? ReleaseDate { get; init; }

    public decimal AvgRating { get; init; }

    public string? Description { get; init; }

    public string? PosterUrl { get; init; }

    public string? TrailerUrl { get; init; }

    public string? BannerUrl { get; init; }

    public string MovieStatus { get; init; } = string.Empty;

    public int ViewCount { get; init; }

    public string? AgeRating { get; init; }

    public string? Director { get; init; }
}
