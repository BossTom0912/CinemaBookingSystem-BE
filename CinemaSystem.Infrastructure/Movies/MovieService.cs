using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Movies;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Movies;

/// <summary>
/// Runtime movie-query implementation reached from <c>MoviesController</c> and
/// reused by <c>GeminiChatbotService</c>.
/// </summary>
/// <remarks>
/// Queries MOVIE with no tracking, applies public visibility rules, projects to
/// Contracts DTOs, and returns the result to callers through
/// <c>ServiceResult</c>. Movie administration is not implemented here.
/// </remarks>
public sealed class MovieService : IMovieService
{
    private const string InactiveStatus = "INACTIVE";
    private const string ProhibitedAgeRating = "C";

    private readonly CinemaDbContext _dbContext;

    public MovieService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<IReadOnlyList<MovieResponse>>> GetMoviesAsync(
        string? status,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Movies
            .AsNoTracking()
            .Where(movie =>
                movie.MovieStatus != InactiveStatus &&
                movie.AgeRating != ProhibitedAgeRating);

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(movie => movie.MovieStatus == normalizedStatus);
        }

        var movies = await query
            .OrderByDescending(movie => movie.ReleaseDate)
            .Select(movie => new MovieResponse
            {
                Id = movie.MovieId,
                MovieNameVn = movie.Title,
                Genre = movie.Genre,
                Duration = movie.DurationMinutes,
                ImagePoster = movie.PosterUrl,
                AgeRating = movie.AgeRating,
                Highlight = movie.Highlight
            })
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<MovieResponse>>.Ok(
            movies,
            "Movies retrieved successfully.");
    }

    public async Task<ServiceResult<MovieDetailResponse>> GetMovieByIdAsync(
        string movieId,
        CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies
            .AsNoTracking()
            .Where(item =>
                item.MovieId == movieId &&
                item.MovieStatus != InactiveStatus &&
                item.AgeRating != ProhibitedAgeRating)
            .Select(item => new MovieDetailResponse
            {
                MovieId = item.MovieId,
                Title = item.Title,
                DurationMinutes = item.DurationMinutes,
                Genre = item.Genre,
                Language = item.Language,
                ReleaseDate = item.ReleaseDate,
                AgeRating = item.AgeRating,
                Description = item.Description,
                PosterUrl = item.PosterUrl,
                TrailerUrl = item.TrailerUrl,
                MovieStatus = item.MovieStatus
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (movie is null)
        {
            return ServiceResult<MovieDetailResponse>.Fail(
                404,
                "Movie was not found.",
                "MOVIE_NOT_FOUND");
        }

        return ServiceResult<MovieDetailResponse>.Ok(
            movie,
            "Movie retrieved successfully.");
    }
}
