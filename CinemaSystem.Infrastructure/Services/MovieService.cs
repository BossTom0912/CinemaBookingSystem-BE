using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Movies;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Services;

public sealed class MovieService : IMovieService
{
    private readonly CinemaDbContext _dbContext;

    public MovieService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ServiceResult<IReadOnlyList<MovieResponse>>> GetMoviesAsync(string? status, CancellationToken cancellationToken)
    {
        var query = _dbContext.Movies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(m => m.MovieStatus == normalizedStatus);
        }

        var movies = await query
            .OrderByDescending(m => m.ReleaseDate)
            .Select(m => ToResponse(m))
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<MovieResponse>>.Ok(movies, "Movies retrieved successfully.");
    }

    private static MovieResponse ToResponse(Movie m)
    {
        return new MovieResponse
        {
            Id = m.MovieId,
            MovieNameVn = m.Title,
            Genre = m.Genre,
            Duration = m.DurationMinutes,
            ImagePoster = m.PosterUrl,
            AgeRating = m.AgeRating,
            Highlight = m.Highlight
        };
    }
}
