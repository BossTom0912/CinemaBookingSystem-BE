using System.IO;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Movies;

namespace CinemaSystem.Application.Interfaces;

public interface IMovieService
{
    Task<ServiceResult<PagedList<MovieResponse>>> GetMoviesAsync(
        string? status,
        int pageIndex,
        int pageSize,
        string? genre,
        bool includeDeleted,
        CancellationToken cancellationToken);

    Task<ServiceResult<MovieDetailResponse>> GetMovieByIdAsync(
        string movieId,
        bool isAdmin,
        CancellationToken cancellationToken);

    Task<ServiceResult<object>> IncrementMovieViewAsync(
        string movieId,
        CancellationToken cancellationToken);

    Task<ServiceResult<MovieDetailResponse>> CreateMovieAsync(
        CreateMovieRequest request,
        Stream? posterStream,
        string? posterFileName,
        CancellationToken cancellationToken);

    Task<ServiceResult<MovieDetailResponse>> UpdateMovieAsync(
        string movieId,
        UpdateMovieRequest request,
        Stream? posterStream,
        string? posterFileName,
        string actionUserId,
        CancellationToken cancellationToken);

    Task<ServiceResult<object>> DeleteMovieAsync(
        string movieId,
        string actionUserId,
        CancellationToken cancellationToken);

    Task<ServiceResult<MovieAutofillResponse>> AutofillMovieFromUrlAsync(
        MovieAutofillRequest request,
        CancellationToken cancellationToken);

    Task UpdateMovieRatingAsync(
        string movieId,
        int ratingDiff,
        int reviewCountDiff,
        CancellationToken cancellationToken);
}
