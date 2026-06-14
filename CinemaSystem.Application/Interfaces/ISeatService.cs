using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Seats;

namespace CinemaSystem.Application.Interfaces;

public interface ISeatService
{
    Task<ServiceResult<IEnumerable<SeatResponse>>> GetSeatsByRoomAsync(
        string roomId,
        CancellationToken cancellationToken);
    Task<ServiceResult<bool>> CreateSeatAsync(
        CreateSeatRequest request,
        string userId,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> UpdateSeatAsync(
        UpdateSeatRequest request,
        string userId,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> DeleteSeatAsync(
        string seatId,
        string userId,
        CancellationToken cancellationToken);

    // Approve/Reject handled centrally by AdminRequestsController via ChangeRequest workflow

    Task<ServiceResult<LockSeatResponse>>
        LockSeatAsync(
            LockSeatRequest request,
            string userId,
            CancellationToken cancellationToken);

    Task<ServiceResult<UnlockSeatResponse>>
        UnlockSeatAsync(
            UnlockSeatRequest request,
            string userId,
            CancellationToken cancellationToken);

    Task<ServiceResult<SeatMapResponse>>
        GetSeatMapAsync(
            string showtimeId,
            CancellationToken cancellationToken);
}
