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
        bool includeDeleted,
        CancellationToken cancellationToken);

    Task<ServiceResult<MovieDetailResponse>> GetMovieByIdAsync(
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
        CancellationToken cancellationToken);

    Task<ServiceResult<object>> DeleteMovieAsync(
        string movieId,
        CancellationToken cancellationToken);

    Task UpdateMovieRatingAsync(
        string movieId,
        int ratingDiff,
        int reviewCountDiff,
        CancellationToken cancellationToken);
}
