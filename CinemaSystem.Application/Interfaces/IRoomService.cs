using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Rooms;

namespace CinemaSystem.Application.Interfaces;

public interface IRoomService
{
    Task<ServiceResult<IReadOnlyList<RoomResponse>>> GetRoomsAsync(bool includeInactive, CancellationToken cancellationToken);


    Task<ServiceResult<RoomResponse>> GetRoomByIdAsync(string roomId, bool includeInactive, CancellationToken cancellationToken);

    Task<ServiceResult<RoomResponse>> CreateRoomAsync(
        string cinemaId,
        CreateRoomRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<RoomResponse>> UpdateRoomAsync(
        string roomId,
        UpdateRoomRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<object>> DeleteRoomAsync(string roomId, CancellationToken cancellationToken);
    Task<ServiceResult<object>> GenerateSeatsAsync(
    string roomId,
    GenerateSeatsRequest request,
    CancellationToken cancellationToken);
}
