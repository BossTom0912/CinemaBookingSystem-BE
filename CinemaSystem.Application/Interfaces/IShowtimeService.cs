using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Showtimes;

namespace CinemaSystem.Application.Interfaces;

public interface IShowtimeService
{
    Task<ServiceResult<IReadOnlyList<ShowtimeResponse>>> GetShowtimesAsync(CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<ShowtimeResponse>>> GetShowtimesByCinemaAsync(
        string? cinemaId,
        CancellationToken cancellationToken);

    Task<ServiceResult<ShowtimeResponse>> GetShowtimeByIdAsync(string showtimeId, CancellationToken cancellationToken);

    Task<ServiceResult<ShowtimeResponse>> CreateShowtimeAsync(
        CreateShowtimeRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ShowtimeResponse>> UpdateShowtimeAsync(
        string showtimeId,
        UpdateShowtimeRequest request,
        bool force,
        CancellationToken cancellationToken);

    Task<ServiceResult<object>> DeleteShowtimeAsync(string showtimeId, CancellationToken cancellationToken);

    Task<ServiceResult<ShowtimeResponse>> ChangeRoomAsync(
        string showtimeId,
        ChangeRoomRequest request,
        CancellationToken cancellationToken);
}
