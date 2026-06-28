using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Showtimes;

namespace CinemaSystem.Application.Interfaces;

public interface IShowtimeCancellationService
{
    Task<ServiceResult<CancelShowtimeResponse>> CancelShowtimeAsync(
        string showtimeId,
        string userId,
        CancelShowtimeRequest request,
        CancellationToken cancellationToken);
}
