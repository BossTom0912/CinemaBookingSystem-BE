using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Movies;

namespace CinemaSystem.Application.Interfaces;

public interface IMovieService
{
    Task<ServiceResult<IReadOnlyList<MovieResponse>>> GetMoviesAsync(string? status, CancellationToken cancellationToken);
}
