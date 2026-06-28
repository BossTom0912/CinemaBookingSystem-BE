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
        string? genre,
        bool includeDeleted,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Movies.Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre).AsNoTracking();

        if (!string.IsNullOrWhiteSpace(genre))
        {
            query = query.Where(m => m.MovieGenres.Any(mg => mg.Genre.Name.Contains(genre)));
        }

        if (!includeDeleted)
        {
            query = query.Where(movie => movie.MovieStatus != DomainConstants.EntityStatus.Inactive && movie.AgeRating != DomainConstants.AgeRating.C);
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
                Genres = movie.MovieGenres.Select(mg => mg.Genre.Name).ToList(),
                Duration = movie.DurationMinutes,
                ImagePoster = movie.PosterUrl,
                AvgRating = movie.AverageRating,
                Highlight = movie.Highlight,
                ViewCount = movie.ViewCount,
                AgeRating = movie.AgeRating,
                Director = movie.Director
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
        var query = _dbContext.Movies.Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre).Where(item => item.MovieId == movieId);
        
        if (!isAdmin)
        {
            query = query.Where(item => item.MovieStatus != DomainConstants.EntityStatus.Inactive && item.AgeRating != DomainConstants.AgeRating.C);
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
            Genres = movie.MovieGenres.Select(mg => mg.Genre.Name).ToList(),
            Language = movie.LanguageId,
            ReleaseDate = movie.ReleaseDate,
            AvgRating = movie.AverageRating,
            Description = movie.Description,
            PosterUrl = movie.PosterUrl,
            TrailerUrl = movie.TrailerUrl,
            MovieStatus = movie.MovieStatus,
            ViewCount = movie.ViewCount,
            AgeRating = movie.AgeRating,
            Director = movie.Director
        };
    }

    public async Task<ServiceResult<MovieDetailResponse>> CreateMovieAsync(
        CreateMovieRequest request,
        Stream? posterStream,
        string? posterFileName,
        CancellationToken cancellationToken)
    {
        // 1. Check title duplication
        var exists = await _dbContext.Movies.AnyAsync(m => m.Title == request.Title, cancellationToken);
        if (exists)
        {
            return ServiceResult<MovieDetailResponse>.Fail(400, "A movie with this title already exists.", "MOVIE_TITLE_DUPLICATED");
        }

        // 2. Validate AgeRating
        if (!string.IsNullOrEmpty(request.AgeRating) && !DomainConstants.AgeRating.ValidRatings.Contains(request.AgeRating.ToUpperInvariant()))
        {
            return ServiceResult<MovieDetailResponse>.Fail(400, "Invalid Age Rating. Allowed values: P, K, T13, T16, T18, C.", "INVALID_AGE_RATING");
        }

        // 2.1 Validate Language
        if (!string.IsNullOrEmpty(request.Language))
        {
            var validLanguage = await _dbContext.Languages.AnyAsync(l => l.LanguageId == request.Language.ToUpperInvariant(), cancellationToken);
            if (!validLanguage)
            {
                return ServiceResult<MovieDetailResponse>.Fail(400, "Invalid Language.", "INVALID_LANGUAGE");
            }
        }

        // 2.2 Validate Genres
        if (request.GenreIds != null && request.GenreIds.Any())
        {
            var validGenreCount = await _dbContext.Genres.CountAsync(g => request.GenreIds.Contains(g.GenreId), cancellationToken);
            if (validGenreCount != request.GenreIds.Distinct().Count())
            {
                return ServiceResult<MovieDetailResponse>.Fail(400, "One or more provided GenreIds are invalid.", "INVALID_GENRE_ID");
            }
        }

        // 3. Parse and Validate Date
        DateOnly? releaseDate = null;
        if (!string.IsNullOrWhiteSpace(request.ReleaseDate))
        {
            if (DateOnly.TryParse(request.ReleaseDate, out var pd))
            {
                releaseDate = pd;
            }
            else
            {
                return ServiceResult<MovieDetailResponse>.Fail(400, "Invalid Release Date format. Please use yyyy-MM-dd.", "INVALID_DATE_FORMAT");
            }
        }

        // 4. Calculate Movie Status
        string status = DomainConstants.EntityStatus.NowShowing;
        if (!string.IsNullOrWhiteSpace(request.MovieStatus))
        {
            // If Admin explicitly provides a status, use it
            status = request.MovieStatus;
        }
        else if (releaseDate.HasValue && releaseDate.Value > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            // Otherwise fallback to auto-calculation based on Date
            status = DomainConstants.EntityStatus.ComingSoon;
        }

        var movieId = "MOV_" + Guid.NewGuid().ToString("N");

        string? posterUrl = null;
        if (posterStream != null && !string.IsNullOrWhiteSpace(posterFileName))
        {
            posterUrl = await _fileStorageService.SaveFileAsync(posterStream, posterFileName, "posters", cancellationToken);
        }

        var movie = new Movie
        {
            MovieId = movieId,
            Title = request.Title,
            DurationMinutes = request.DurationMinutes,
            LanguageId = request.Language?.ToUpperInvariant(),
            ReleaseDate = releaseDate,
            AgeRating = request.AgeRating?.ToUpperInvariant(),
            Description = request.Description,
            TrailerUrl = request.TrailerUrl,
            Highlight = request.Highlight,
            PosterUrl = posterUrl,
            Director = request.Director,
            MovieStatus = status
        };

        if (request.GenreIds != null)
        {
            foreach (var genreId in request.GenreIds.Distinct())
            {
                movie.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genreId });
            }
        }

        _dbContext.Movies.Add(movie);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Rollback the uploaded file if database save fails
            if (!string.IsNullOrEmpty(posterUrl))
            {
                await _fileStorageService.DeleteFileAsync(posterUrl, CancellationToken.None);
            }
            throw; // Rethrow to let global error handler catch it
        }

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
        var movie = await _dbContext.Movies.Include(m => m.MovieGenres).ThenInclude(mg => mg.Genre).FirstOrDefaultAsync(m => m.MovieId == movieId, cancellationToken);
        if (movie == null || movie.MovieStatus == DomainConstants.EntityStatus.Inactive)
        {
            return ServiceResult<MovieDetailResponse>.Fail(404, "Movie was not found.", "MOVIE_NOT_FOUND");
        }

        if (!string.IsNullOrEmpty(request.Language))
        {
            var validLanguage = await _dbContext.Languages.AnyAsync(l => l.LanguageId == request.Language.ToUpperInvariant(), cancellationToken);
            if (!validLanguage)
            {
                return ServiceResult<MovieDetailResponse>.Fail(400, "Invalid Language.", "INVALID_LANGUAGE");
            }
        }

        if (request.GenreIds != null && request.GenreIds.Any())
        {
            var validGenreCount = await _dbContext.Genres.CountAsync(g => request.GenreIds.Contains(g.GenreId), cancellationToken);
            if (validGenreCount != request.GenreIds.Distinct().Count())
            {
                return ServiceResult<MovieDetailResponse>.Fail(400, "One or more provided GenreIds are invalid.", "INVALID_GENRE_ID");
            }
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
        movie.LanguageId = request.Language?.ToUpperInvariant();
        movie.ReleaseDate = releaseDate;
        movie.AgeRating = request.AgeRating;
        movie.Description = request.Description;
        movie.TrailerUrl = request.TrailerUrl;
        movie.Highlight = request.Highlight;
        movie.Director = request.Director;
        movie.MovieStatus = request.MovieStatus;

        if (request.GenreIds != null)
        {
            movie.MovieGenres.Clear();
            foreach (var genreId in request.GenreIds.Distinct())
            {
                movie.MovieGenres.Add(new MovieGenre { MovieId = movieId, GenreId = genreId });
            }
        }

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
