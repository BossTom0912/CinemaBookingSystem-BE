using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Room query, room CRUD and bulk seat-generation HTTP entry point.
/// </summary>
/// <remarks>
/// Each action hands processing to <see cref="IRoomService"/>. Runtime DI maps
/// it to <c>CinemaSystem.Infrastructure.Rooms.RoomService</c>, which validates
/// CINEMA/ROOM/SEAT state and commits changes through <c>CinemaDbContext</c>.
/// Role checks happen before the action; cinema-scope checks are not currently
/// implemented by this controller or service.
/// </remarks>
[ApiController]
[Route("api/rooms")]
public sealed class RoomsController : ControllerBase
{
    private readonly IRoomService _roomService;

    public RoomsController(IRoomService roomService)
    {
        _roomService = roomService ?? throw new ArgumentNullException(nameof(roomService));
    }

    [HttpGet("rooms")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Staff)]
    public async Task<IActionResult> GetRooms(CancellationToken cancellationToken)
    {
        // Bước tiếp theo: IRoomService -> RoomService tại
        // CinemaSystem.Infrastructure/Rooms/RoomService.cs để query ROOM kèm
        // CINEMA/SEAT. Role đã được middleware kiểm trước khi vào action.
        var result = await _roomService.GetRoomsAsync(cancellationToken);

        // Service trả Contracts DTO; Controller map thành API DTO.
        return ToActionResult(result.MapDataTo<IReadOnlyList<Contracts.Rooms.RoomResponse>, IReadOnlyList<RoomResponse>>());
    }

    [HttpGet("rooms/{roomId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Staff)]
    public async Task<IActionResult> GetRoomById(string roomId, CancellationToken cancellationToken)
    {
        // Bước tiếp theo: RoomService (Infrastructure/Rooms) đọc room/cinema/seats
        // và áp rule ẩn room INACTIVE.
        var result = await _roomService.GetRoomByIdAsync(roomId, cancellationToken);

        // RoomResponse hoặc ROOM_NOT_FOUND quay lại đây để tạo HTTP status.
        return ToActionResult(result.MapDataTo<Contracts.Rooms.RoomResponse, RoomResponse>());
    }

    [HttpPost("cinemas/{cinemaId}/rooms")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> CreateRoom(
        string cinemaId,
        CreateRoomRequest request,
        CancellationToken cancellationToken)
    {
        // Bước tiếp theo: RoomService kiểm CINEMA tồn tại + room status rồi ghi
        // ROOM qua CinemaDbContext. Tách sang service để Controller không chứa
        // validation nghiệp vụ hoặc SaveChangesAsync.
        var result = await _roomService.CreateRoomAsync(
            cinemaId,
            request.MapTo<Contracts.Rooms.CreateRoomRequest>(),
            cancellationToken);

        // ROOM đã lưu và map DTO xong mới quay lại Controller.
        return ToActionResult(result.MapDataTo<Contracts.Rooms.RoomResponse, RoomResponse>());
    }

    [HttpPut("rooms/{roomId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> UpdateRoom(
        string roomId,
        UpdateRoomRequest request,
        CancellationToken cancellationToken)
    {
        // Bước tiếp theo: RoomService (Infrastructure/Rooms) kiểm tên trùng,
        // capacity, số ghế hiện có và status trước khi cập nhật ROOM.
        var result = await _roomService.UpdateRoomAsync(
            roomId,
            request.MapTo<Contracts.Rooms.UpdateRoomRequest>(),
            cancellationToken);

        // Kết quả persistence quay lại đây để map API response.
        return ToActionResult(result.MapDataTo<Contracts.Rooms.RoomResponse, RoomResponse>());
    }

    [HttpDelete("rooms/{roomId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> DeleteRoom(string roomId, CancellationToken cancellationToken)
    {
        // Bước tiếp theo: RoomService thực hiện soft delete trong
        // Infrastructure/Rooms bằng roomStatus=INACTIVE; đây không phải xóa vật lý.
        var result = await _roomService.DeleteRoomAsync(roomId, cancellationToken);

        // Service lưu DB xong thì kết quả quay lại Controller.
        return ToActionResult(result);
    }
    [HttpPost("{roomId}/generate-seats")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GenerateSeats(
        string roomId,
        [FromBody] GenerateSeatsRequest request,
        CancellationToken cancellationToken)
    {
        // Bước tiếp theo: RoomService kiểm room chưa có ghế và giới hạn ma trận,
        // sau đó sinh nhiều SEAT + cập nhật ROOM.capacity trong Infrastructure.
        var result = await _roomService.GenerateSeatsAsync(
            roomId,
            request.MapTo<Contracts.Rooms.GenerateSeatsRequest>(),
            cancellationToken);

        // Số ghế đã tạo hoặc lỗi validation quay lại API layer tại đây.
        return ToActionResult(result);
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }
}
