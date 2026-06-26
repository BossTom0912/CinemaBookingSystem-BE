using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Movies;
using CinemaSystem.Domain.Constants;
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
    private const string ProhibitedAgeRating = "C";

    private readonly CinemaDbContext _dbContext;
    private readonly IAdminRefundService _refundService;
    private readonly IFileStorageService _fileStorageService;

    public MovieService(CinemaDbContext dbContext, IAdminRefundService refundService, IFileStorageService fileStorageService)
    {
        _dbContext = dbContext;
        _refundService = refundService;
        _fileStorageService = fileStorageService;
    }

    public async Task<ServiceResult<PagedList<MovieResponse>>> GetMoviesAsync(
        string? status,
        int pageIndex,
        int pageSize,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Movies.AsNoTracking();

        if (!includeDeleted)
        {
            query = query.Where(movie => movie.MovieStatus != DomainConstants.EntityStatus.Inactive && movie.AgeRating != ProhibitedAgeRating);
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
                ViewCount = movie.ViewCount,
                AgeRating = movie.AgeRating
            })
            .ToListAsync(cancellationToken);

        var pagedList = new PagedList<MovieResponse>(movies, totalCount, pageIndex, pageSize);

        return ServiceResult<PagedList<MovieResponse>>.Ok(
            pagedList,
            "Movies retrieved successfully.");
    }

    public async Task<ServiceResult<MovieDetailResponse>> GetMovieByIdAsync(
        string movieId,
        bool isAdmin,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Movies.Where(item => item.MovieId == movieId);

        if (!isAdmin)
        {
            query = query.Where(item => item.MovieStatus != DomainConstants.EntityStatus.Inactive && item.AgeRating != ProhibitedAgeRating);
        }

        var movie = await query.FirstOrDefaultAsync(cancellationToken);

        if (movie is null)
        {
            return ServiceResult<MovieDetailResponse>.Fail(
                404,
                "Movie was not found.",
                "MOVIE_NOT_FOUND");
        }

        var response = ToDetailResponse(movie);

        return ServiceResult<MovieDetailResponse>.Ok(
            response,
            "Movie retrieved successfully.");
    }

    public async Task<ServiceResult<object>> IncrementMovieViewAsync(
        string movieId,
        CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(item => item.MovieId == movieId, cancellationToken);
        if (movie is null)
        {
            return ServiceResult<object>.Fail(404, "Movie was not found.", "MOVIE_NOT_FOUND");
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

        return ServiceResult<object>.Ok(new { MovieId = movieId, ViewCount = movie.ViewCount }, "Movie view incremented.");
    }

    private static MovieDetailResponse ToDetailResponse(Movie movie)
    {
        return new MovieDetailResponse
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
            ViewCount = movie.ViewCount,
            AgeRating = movie.AgeRating
        };
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
            MovieStatus = DomainConstants.EntityStatus.Active
        };

        _dbContext.Movies.Add(movie);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<MovieDetailResponse>.Ok(ToDetailResponse(movie), "Movie created successfully.");
    }

    public async Task<ServiceResult<MovieDetailResponse>> UpdateMovieAsync(
        string movieId,
        UpdateMovieRequest request,
        Stream? posterStream,
        string? posterFileName,
        string actionUserId,
        CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);
        if (movie == null || movie.MovieStatus == DomainConstants.EntityStatus.Inactive)
        {
            return ServiceResult<MovieDetailResponse>.Fail(404, "Movie was not found.", "MOVIE_NOT_FOUND");
        }

        if (movie.DurationMinutes != request.DurationMinutes)
        {
            var openShowtimes = await _dbContext.Showtimes
                .Where(s => s.MovieId == movieId && s.Status == DomainConstants.EntityStatus.Open)
                .Select(s => s.ShowtimeId)
                .ToArrayAsync(cancellationToken);

            if (openShowtimes.Any())
            {
                var refundResult = await _refundService.CancelShowtimesAndRefundAsync(openShowtimes, "Movie " + movie.Title + " duration changed.", false, actionUserId, cancellationToken);
                if (!refundResult.Success)
                {
                    return ServiceResult<MovieDetailResponse>.Fail(refundResult.StatusCode, refundResult.Message, refundResult.ErrorCode!);
                }
            }
        }

        if (posterStream != null && !string.IsNullOrWhiteSpace(posterFileName))
        {
            if (!string.IsNullOrEmpty(movie.PosterUrl))
            {
                await _fileStorageService.DeleteFileAsync(movie.PosterUrl, cancellationToken);
            }
            movie.PosterUrl = await _fileStorageService.SaveFileAsync(posterStream, posterFileName, "posters", cancellationToken);
        }
        else if (request.PosterUrl != null)
        {
             movie.PosterUrl = request.PosterUrl;
        }

        DateOnly? releaseDate = null;
        if (request.ReleaseDate.HasValue)
        {
            releaseDate = request.ReleaseDate.Value;
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

        return ServiceResult<MovieDetailResponse>.Ok(ToDetailResponse(movie), "Movie updated successfully.");
    }

    public async Task<ServiceResult<object>> DeleteMovieAsync(
        string movieId,
        string actionUserId,
        CancellationToken cancellationToken)
    {
        var movie = await _dbContext.Movies.FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);
        if (movie == null || movie.MovieStatus == DomainConstants.EntityStatus.Inactive)
        {
            return ServiceResult<object>.Fail(404, "Movie not found.", "MOVIE_NOT_FOUND");
        }

        var openShowtimes = await _dbContext.Showtimes
            .Where(s => s.MovieId == movieId && s.Status == DomainConstants.EntityStatus.Open)
            .Select(s => s.ShowtimeId)
            .ToArrayAsync(cancellationToken);

        if (openShowtimes.Any())
        {
            var refundResult = await _refundService.CancelShowtimesAndRefundAsync(openShowtimes, "Movie " + movie.Title + " was deleted.", true, actionUserId, cancellationToken);
            if (!refundResult.Success)
            {
                return ServiceResult<object>.Fail(refundResult.StatusCode, refundResult.Message, refundResult.ErrorCode!);
            }
        }

        movie.MovieStatus = DomainConstants.EntityStatus.Inactive;
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<object>.Ok(new { MovieId = movieId, Status = movie.MovieStatus }, "Movie softly deleted successfully.");
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
