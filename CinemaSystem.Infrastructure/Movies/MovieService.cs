using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Movies;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Infrastructure.Movies;

public sealed class MovieService : IMovieService
{
    private const string InactiveStatus = "INACTIVE";
    private const string DeletedStatus = "DELETED";
    private const string ProhibitedAgeRating = "C";

    private readonly CinemaDbContext _dbContext;
    private readonly IFileStorageService _fileStorageService;

    public MovieService(CinemaDbContext dbContext, IFileStorageService fileStorageService)
    {
        _dbContext = dbContext;
        _fileStorageService = fileStorageService;
    }

    public async Task<ServiceResult<PagedList<MovieResponse>>> GetMoviesAsync(
        string? status,
        int pageIndex,
        int pageSize,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Movies
            .AsNoTracking()
            .Where(movie => movie.AgeRating != ProhibitedAgeRating);

        if (!includeDeleted)
        {
            query = query.Where(movie => movie.MovieStatus != DeletedStatus && movie.MovieStatus != InactiveStatus);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var normalizedStatus = status.Trim().ToUpperInvariant();
            query = query.Where(movie => movie.MovieStatus == normalizedStatus);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var movies = await query
            .OrderByDescending(movie => movie.ReleaseDate)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .Select(movie => new MovieResponse
            {
                Id = movie.MovieId,
                MovieNameVn = movie.Title,
                Genre = movie.Genre,
                Duration = movie.DurationMinutes,
                ImagePoster = movie.PosterUrl,
                AvgRating = movie.AverageRating,
                Highlight = movie.Highlight,
                ViewCount = movie.ViewCount
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<MovieResponse>(movies, totalCount, pageIndex, pageSize);

        return ServiceResult<PagedList<MovieResponse>>.Ok(
            pagedList,
            "Movies retrieved successfully.");
    }

    public async Task<ServiceResult<MovieDetailResponse>> GetMovieByIdAsync(
        string movieId,
        CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies
            .Where(item =>
                item.MovieId == movieId &&
                item.AgeRating != ProhibitedAgeRating)
            .FirstOrDefaultAsync(cancellationToken);

        if (movie is null)
        {
            return ServiceResult<MovieDetailResponse>.Fail(
                404,
                "Movie was not found.",
                "MOVIE_NOT_FOUND");
        }

        movie.ViewCount += 1;

        var maxViews = await _dbContext.Movies.MaxAsync(m => (int?)m.ViewCount, cancellationToken) ?? 0;

        if (movie.ViewCount >= maxViews && movie.ViewCount > 0)
        {
            var previousPopular = await _dbContext.Movies
                .Where(m => m.Highlight == "POPULAR" && m.MovieId != movie.MovieId)
                .ToListAsync(cancellationToken);

            foreach (var p in previousPopular)
            {
                p.Highlight = p.ViewCount >= 1000 ? "HOT" : (p.ViewCount >= 500 ? "TRENDING" : null);
            }

            movie.Highlight = "POPULAR";
        }
        else if (movie.Highlight != "POPULAR")
        {
            if (movie.ViewCount >= 1000)
            {
                movie.Highlight = "HOT";
            }
            else if (movie.ViewCount >= 500)
            {
                movie.Highlight = "TRENDING";
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var response = new MovieDetailResponse
        {
            MovieId = movie.MovieId,
            Title = movie.Title,
            DurationMinutes = movie.DurationMinutes,
            Genre = movie.Genre,
            Language = movie.Language,
            ReleaseDate = movie.ReleaseDate,
            AvgRating = movie.AverageRating,
            Description = movie.Description,
            PosterUrl = movie.PosterUrl,
            TrailerUrl = movie.TrailerUrl,
            MovieStatus = movie.MovieStatus,
            ViewCount = movie.ViewCount
        };

        return ServiceResult<MovieDetailResponse>.Ok(
            response,
            "Movie retrieved successfully.");
    }

    public async Task<ServiceResult<MovieDetailResponse>> CreateMovieAsync(
        CreateMovieRequest request,
        Stream? posterStream,
        string? posterFileName,
        CancellationToken cancellationToken)
    {
        var movieId = "MOV_" + Guid.NewGuid().ToString("N");

        string? posterUrl = null;
        if (posterStream != null && !string.IsNullOrWhiteSpace(posterFileName))
        {
            posterUrl = await _fileStorageService.SaveFileAsync(posterStream, posterFileName, "posters", cancellationToken);
        }

        DateOnly? releaseDate = null;
        if (DateOnly.TryParse(request.ReleaseDate, out var pd))
        {
            releaseDate = pd;
        }

        var movie = new Movie
        {
            MovieId = movieId,
            Title = request.Title,
            DurationMinutes = request.DurationMinutes,
            Genre = request.Genre,
            Language = request.Language,
            ReleaseDate = releaseDate,
            AgeRating = request.AgeRating,
            Description = request.Description,
            TrailerUrl = request.TrailerUrl,
            Highlight = request.Highlight,
            PosterUrl = posterUrl,
            MovieStatus = "ACTIVE"
        };

        _dbContext.Movies.Add(movie);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetMovieByIdAsync(movieId, cancellationToken))!;
    }

    public async Task<ServiceResult<MovieDetailResponse>> UpdateMovieAsync(
        string movieId,
        UpdateMovieRequest request,
        Stream? posterStream,
        string? posterFileName,
        CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);
        if (movie == null)
        {
            return ServiceResult<MovieDetailResponse>.Fail(404, "Movie was not found.", "MOVIE_NOT_FOUND");
        }

        if (posterStream != null && !string.IsNullOrWhiteSpace(posterFileName))
        {
            // Delete old poster if exists
            if (!string.IsNullOrEmpty(movie.PosterUrl))
            {
                await _fileStorageService.DeleteFileAsync(movie.PosterUrl, cancellationToken);
            }
            movie.PosterUrl = await _fileStorageService.SaveFileAsync(posterStream, posterFileName, "posters", cancellationToken);
        }

        DateOnly? releaseDate = null;
        if (DateOnly.TryParse(request.ReleaseDate, out var pd))
        {
            releaseDate = pd;
        }

        movie.Title = request.Title;
        movie.DurationMinutes = request.DurationMinutes;
        movie.Genre = request.Genre;
        movie.Language = request.Language;
        movie.ReleaseDate = releaseDate;
        movie.AgeRating = request.AgeRating;
        movie.Description = request.Description;
        movie.TrailerUrl = request.TrailerUrl;
        movie.Highlight = request.Highlight;
        movie.MovieStatus = request.MovieStatus;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return (await GetMovieByIdAsync(movieId, cancellationToken))!;
    }

    public async Task<ServiceResult<object>> DeleteMovieAsync(
        string movieId,
        CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);
        if (movie == null)
        {
            return ServiceResult<object>.Fail(404, "Movie was not found.", "MOVIE_NOT_FOUND");
        }

        movie.MovieStatus = DeletedStatus;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object>.Ok(new { MovieId = movieId, Deleted = true }, "Movie deleted successfully.");
    }


    public async Task UpdateMovieRatingAsync(string movieId, int ratingDiff, int reviewCountDiff, CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);
        if (movie == null) return;

        decimal currentTotalScore = movie.AverageRating * movie.TotalReviews;
        decimal newTotalScore = currentTotalScore + ratingDiff;
        
        movie.TotalReviews += reviewCountDiff;
        movie.AverageRating = movie.TotalReviews > 0 ? Math.Round(newTotalScore / movie.TotalReviews, 2) : 0;

        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
