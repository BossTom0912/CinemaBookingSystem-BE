using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Seat CRUD, seat-map and temporary seat-lock HTTP entry point.
/// </summary>
/// <remarks>
/// Processing continues through <see cref="ISeatService"/> to
/// <c>CinemaSystem.Infrastructure.Services.SeatService</c>. CRUD and seat-map
/// state use <c>CinemaDbContext</c>; lock/unlock also calls
/// <c>ISeatLockStore</c>, resolved to Redis when configured and otherwise to the
/// in-process lock store. Booking/checkout consumes the resulting lock state.
/// </remarks>
[ApiController]
[Route("api/seats")]
public sealed class SeatsController : ControllerBase
{
    private readonly ISeatService _seatService;

    public SeatsController(ISeatService seatService)
    {
        _seatService = seatService
            ?? throw new ArgumentNullException(nameof(seatService));
    }

    /// <summary>
    /// Create seat — applied immediately. Admin and Manager may create.
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Admin)]
    [ProducesResponseType(
        typeof(ApiResponse<bool>),
        StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateSeatRequest(
        [FromBody] CreateSeatRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();

        // Bước tiếp theo: ISeatService được DI map sang SeatService tại
        // CinemaSystem.Infrastructure/Services/SeatService.cs. Service kiểm
        // ROOM/SEAT_TYPE/trùng seat code rồi ghi SEAT bằng CinemaDbContext.
        var result = await _seatService.CreateSeatAsync(
            request.MapTo<Contracts.Seats.CreateSeatRequest>(),
            userId,
            cancellationToken);

        // SEAT lưu xong hoặc lỗi rule quay lại Controller để map ApiResponse.
        return ToActionResult(result);
    }

    /// <summary>
    /// Update seat — applied immediately. Admin and Manager may update.
    /// </summary>
    [HttpPut("{seatId}")]
    [Authorize(Roles = AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Admin)]
    [ProducesResponseType(
        typeof(ApiResponse<bool>),
        StatusCodes.Status201Created)]
    public async Task<IActionResult> UpdateSeat(
        string seatId,
        [FromBody] UpdateSeatRequest request,
        CancellationToken cancellationToken)
    {
        request.SeatId = seatId;

        // Bước tiếp theo: SeatService (Infrastructure/Services) kiểm seat tồn tại,
        // seat code mới không trùng rồi cập nhật SEAT.
        var result = await _seatService.UpdateSeatAsync(
            request.MapTo<Contracts.Seats.UpdateSeatRequest>(),
            GetUserId(),
            cancellationToken);

        // Kết quả SaveChangesAsync quay lại API layer tại đây.
        return ToActionResult(result);
    }

    /// <summary>
    /// Delete seat — applied immediately. Admin and Manager may delete.
    /// </summary>
    [HttpDelete("{seatId}")]
    [Authorize(Roles = AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Admin)]
    [ProducesResponseType(
        typeof(ApiResponse<bool>),
        StatusCodes.Status201Created)]
    public async Task<IActionResult> DeleteSeat(
        string seatId,
        CancellationToken cancellationToken)
    {
        // Bước tiếp theo: SeatService kiểm SHOWTIME_SEAT tương lai; nếu an toàn
        // thì soft-delete bằng SEAT.isActive=false thay vì xóa vật lý.
        var result = await _seatService.DeleteSeatAsync(
            seatId,
            GetUserId(),
            cancellationToken);

        // ServiceResult quay lại Controller để trả success/conflict/not-found.
        return ToActionResult(result);
    }

    /// <summary>
    /// Manager / Admin can view room seat layout.
    /// </summary>
    [HttpGet("room/{roomId}")]
    [Authorize(
        Roles = $"{AuthConstants.Roles.Staff},{AuthConstants.Roles.Manager},{AuthConstants.Roles.Admin}")]
    [ProducesResponseType(
        typeof(ApiResponse<IEnumerable<SeatResponse>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSeatsByRoom(
        string roomId,
        CancellationToken cancellationToken)
    {
        // Bước tiếp theo: SeatService query SEAT theo roomId qua CinemaDbContext,
        // sắp theo row/number rồi project sang DTO.
        var result = await _seatService.GetSeatsByRoomAsync(
            roomId,
            cancellationToken);

        // Danh sách ghế quay lại đây để bọc ApiResponse.
        return ToActionResult(result);
    }

    [HttpGet]
    [Authorize(Roles = AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Admin)]
    [ProducesResponseType(
        typeof(ApiResponse<PagedList<SeatResponse>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSeats(
        [FromQuery] string? roomId,
        [FromQuery] bool? isActive,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var result = await _seatService.GetSeatsAsync(
            roomId,
            isActive,
            pageIndex,
            pageSize,
            cancellationToken);

        return ToActionResult(result.MapDataTo<PagedList<Contracts.Seats.SeatResponse>, PagedList<SeatResponse>>());
    }

    [HttpGet("{seatId}")]
    [Authorize(Roles = AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Admin)]
    [ProducesResponseType(
        typeof(ApiResponse<SeatResponse>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSeatById(
        string seatId,
        CancellationToken cancellationToken)
    {
        var result = await _seatService.GetSeatByIdAsync(seatId, cancellationToken);

        return ToActionResult(result.MapDataTo<Contracts.Seats.SeatResponse, SeatResponse>());
    }

    // Admin approval endpoints removed - approval flow centralized under admin requests controller.

    [Authorize(Policy = AuthConstants.Policies.CanBookTicket)]
    [HttpPost("lock")]
    [ProducesResponseType(
        typeof(ApiResponse<LockSeatResponse>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> LockSeat(
        [FromBody] LockSeatRequest request,
        CancellationToken cancellationToken)
    {
        // Bước tiếp theo 1: SeatService kiểm SHOWTIME_SEAT/BOOKING_SEAT trong DB.
        // Bước tiếp theo 2: SeatService gọi ISeatLockStore tại Infrastructure/
        // Services (RedisSeatLockStore nếu cấu hình Redis, nếu không dùng
        // InMemorySeatLockStore) để chống hai client cùng giữ một ghế.
        // Bước tiếp theo 3: lock thành công mới ghi LOCKED/lockedUntil/userId vào DB.
        var result = await _seatService.LockSeatAsync(
            request.MapTo<Contracts.Seats.LockSeatRequest>(),
            GetUserId(),
            cancellationToken);

        // Cả lock store và DB hoàn tất thì LockSeatResponse mới quay lại Controller.
        return ToActionResult(result.MapDataTo<Contracts.Seats.LockSeatResponse, LockSeatResponse>());
    }

    [Authorize(Policy = AuthConstants.Policies.CanBookTicket)]
    [HttpPost("unlock")]
    [ProducesResponseType(
        typeof(ApiResponse<UnlockSeatResponse>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> UnlockSeat(
        [FromBody] UnlockSeatRequest request,
        CancellationToken cancellationToken)
    {
        // Bước tiếp theo: SeatService xác nhận user là chủ lock, release
        // ISeatLockStore rồi trả SHOWTIME_SEAT về AVAILABLE trong database.
        var result = await _seatService.UnlockSeatAsync(
            request.MapTo<Contracts.Seats.UnlockSeatRequest>(),
            GetUserId(),
            cancellationToken);

        // Trạng thái sau unlock quay lại Controller để map response.
        return ToActionResult(result.MapDataTo<Contracts.Seats.UnlockSeatResponse, UnlockSeatResponse>());
    }

    [Authorize(Policy = AuthConstants.Policies.CanSelectSeat)]
    [HttpGet("showtimes/{showtimeId}/map")]
    [ProducesResponseType(
        typeof(ApiResponse<SeatMapResponse>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSeatMap(
        string showtimeId,
        CancellationToken cancellationToken)
    {
        // Bước tiếp theo: SeatService trước hết release lock hết hạn, sau đó query
        // SHOWTIME_SEAT + SEAT + SEAT_TYPE + BOOKING_SEAT và chia thành ba nhóm
        // available/locked/sold.
        var result = await _seatService.GetSeatMapAsync(
            showtimeId,
            cancellationToken);

        // Seat map đã tính xong quay lại Controller để map Contracts -> API DTO.
        return ToActionResult(result.MapDataTo<Contracts.Seats.SeatMapResponse, SeatMapResponse>());
    }

    private string GetUserId()
    {
        return User.FindFirst("userId")?.Value
            ?? User.FindFirst("sub")?.Value
            ?? string.Empty;
    }

    private ObjectResult ToActionResult<T>(
        ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(
                result.Data,
                result.Message)
            : ApiResponse<T>.Fail(
                result.Message,
                result.ErrorCode,
                result.Errors);

        return StatusCode(
            result.StatusCode,
            response);
    }
}
