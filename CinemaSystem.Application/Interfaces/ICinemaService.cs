using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Cinemas;

namespace CinemaSystem.Application.Interfaces;

public interface ICinemaService
{
    Task<ServiceResult<IReadOnlyList<CinemaResponse>>> GetCinemasAsync(CancellationToken cancellationToken);

    Task<ServiceResult<CinemaResponse>> GetCinemaByIdAsync(string cinemaId, CancellationToken cancellationToken);

    Task<ServiceResult<CinemaResponse>> CreateCinemaAsync(CreateCinemaRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<CinemaResponse>> UpdateCinemaAsync(string cinemaId, UpdateCinemaRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<bool>> DeleteCinemaAsync(string cinemaId, CancellationToken cancellationToken);
}
