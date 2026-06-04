using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Seats;

namespace CinemaSystem.Application.Interfaces;

public interface ISeatService
{
    /// <summary>
    /// Retrieves all seats for a specific room.
    /// </summary>
    /// <param name="roomId">The room ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ServiceResult containing list of seats.</returns>
    Task<ServiceResult<IEnumerable<SeatResponse>>> GetSeatsByRoomAsync(
        string roomId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates the status of multiple seats in a single batch operation.
    /// Optimized to use AddRangeAsync and SaveChangesAsync only once for all seats.
    /// </summary>
    /// <param name="request">Batch update request containing seat IDs and new status.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>ServiceResult containing updated seat count.</returns>
    Task<ServiceResult<int>> BatchUpdateSeatStatusAsync(
        BatchUpdateSeatStatusRequest request,
        CancellationToken cancellationToken);
}
