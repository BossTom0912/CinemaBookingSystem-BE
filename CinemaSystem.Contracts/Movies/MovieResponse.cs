using System;

namespace CinemaSystem.Contracts.Movies;

public sealed class MovieResponse
{
    public string Id { get; init; } = string.Empty;

    public string MovieNameVn { get; init; } = string.Empty;

    public List<string>? Genres { get; init; }

    public int Duration { get; init; }

    public string? ImagePoster { get; init; }

    public decimal AvgRating { get; init; }

    public string? Highlight { get; init; }

    public int ViewCount { get; init; }

    public string? AgeRating { get; init; }

    public string? Director { get; init; }
}
