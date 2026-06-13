using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Movies;

namespace CinemaSystem.Application.Interfaces;

public interface IMovieService
{
    Task<ServiceResult<IReadOnlyList<MovieResponse>>> GetMoviesAsync(
        string? status,
        CancellationToken cancellationToken);

    Task<ServiceResult<MovieDetailResponse>> GetMovieByIdAsync(
        string movieId,
        CancellationToken cancellationToken);
}
