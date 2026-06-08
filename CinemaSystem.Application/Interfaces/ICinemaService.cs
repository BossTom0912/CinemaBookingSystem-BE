using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Cinemas;

namespace CinemaSystem.Application.Interfaces;

public interface ICinemaService
{
    Task<ServiceResult<IReadOnlyList<CinemaResponse>>> GetCinemasAsync(CancellationToken cancellationToken);
}
