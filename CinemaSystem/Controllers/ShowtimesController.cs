using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Public showtime query and Manager/Admin showtime CRUD HTTP entry point.
/// </summary>
/// <remarks>
/// Actions continue through <see cref="IShowtimeService"/> to
/// <c>CinemaSystem.Infrastructure.Showtimes.ShowtimeService</c>. The service
/// validates movie/room availability and overlap, then reads or writes
/// SHOWTIME and SHOWTIME_SEAT through <c>CinemaDbContext</c>. Delete here is not
/// the cancel-showtime/refund use case.
/// </remarks>
[ApiController]
[Route("api/showtimes")]
public sealed class ShowtimesController : ControllerBase
{
    private readonly IShowtimeService _showtimeService;
    private readonly ICinemaScopeAuthorizationService _cinemaScopeAuthorizationService;

    public ShowtimesController(
        IShowtimeService showtimeService,
        ICinemaScopeAuthorizationService cinemaScopeAuthorizationService)
    {
        _showtimeService = showtimeService;
        _cinemaScopeAuthorizationService = cinemaScopeAuthorizationService
            ?? throw new ArgumentNullException(nameof(cinemaScopeAuthorizationService));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetShowtimes(CancellationToken cancellationToken)
    {
        // Bước tiếp theo: IShowtimeService -> ShowtimeService tại
        // CinemaSystem.Infrastructure/Showtimes/ShowtimeService.cs để query
        // SHOWTIME cùng MOVIE/ROOM/CINEMA và số SHOWTIME_SEAT.
        var result = await _showtimeService.GetShowtimesAsync(cancellationToken);

        // Contracts DTO quay lại Controller để map API DTO.
        return ToActionResult(result.MapDataTo<IReadOnlyList<Contracts.Showtimes.ShowtimeResponse>, IReadOnlyList<ShowtimeResponse>>());
    }

    [HttpGet("{showtimeId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetShowtimeById(string showtimeId, CancellationToken cancellationToken)
    {
        // Bước tiếp theo: ShowtimeService (Infrastructure/Showtimes) query chi
        // tiết showtime qua CinemaDbContext.
        var result = await _showtimeService.GetShowtimeByIdAsync(showtimeId, cancellationToken);

        // DTO hoặc SHOWTIME_NOT_FOUND quay lại đây để trả HTTP response.
        return ToActionResult(result.MapDataTo<Contracts.Showtimes.ShowtimeResponse, ShowtimeResponse>());
    }

    [HttpPost]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> CreateShowtime(
        CreateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        // Bước tiếp theo: ShowtimeService kiểm MOVIE/ROOM/CINEMA, thời gian tương
        // lai và overlap; sau đó tạo SHOWTIME + SHOWTIME_SEAT trong Infrastructure.
        var scope = await _cinemaScopeAuthorizationService.AuthorizeRoomAsync(User, request.RoomId, cancellationToken);
        if (!scope.Allowed)
        {
            return ToActionResult(scope);
        }

        var result = await _showtimeService.CreateShowtimeAsync(
            request.MapTo<Contracts.Showtimes.CreateShowtimeRequest>(),
            cancellationToken);

        // Transaction EF hoàn tất thì showtime đã tạo quay lại Controller.
        return ToActionResult(result.MapDataTo<Contracts.Showtimes.ShowtimeResponse, ShowtimeResponse>());
    }

    [HttpPut("{showtimeId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> UpdateShowtime(
        string showtimeId,
        UpdateShowtimeRequest request,
        CancellationToken cancellationToken)
    {
        // Bước tiếp theo: ShowtimeService kiểm booking hiện có và chạy lại rule
        // overlap. Nếu đổi room, service xóa/sinh lại SHOWTIME_SEAT.
        var currentScope = await _cinemaScopeAuthorizationService.AuthorizeShowtimeAsync(User, showtimeId, cancellationToken);
        if (!currentScope.Allowed)
        {
            return ToActionResult(currentScope);
        }

        var targetRoomScope = await _cinemaScopeAuthorizationService.AuthorizeRoomAsync(User, request.RoomId, cancellationToken);
        if (!targetRoomScope.Allowed)
        {
            return ToActionResult(targetRoomScope);
        }

        var result = await _showtimeService.UpdateShowtimeAsync(
            showtimeId,
            request.MapTo<Contracts.Showtimes.UpdateShowtimeRequest>(),
            cancellationToken);

        // Kết quả cập nhật hoặc lỗi rule quay lại Controller để map response.
        return ToActionResult(result.MapDataTo<Contracts.Showtimes.ShowtimeResponse, ShowtimeResponse>());
    }

    [HttpDelete("{showtimeId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    public async Task<IActionResult> DeleteShowtime(string showtimeId, CancellationToken cancellationToken)
    {
        // Bước tiếp theo: ShowtimeService chỉ hard-delete khi chưa có booking/
        // refund history. Đây KHÔNG phải luồng cancel showtime + refund UC003.
        var scope = await _cinemaScopeAuthorizationService.AuthorizeShowtimeAsync(User, showtimeId, cancellationToken);
        if (!scope.Allowed)
        {
            return ToActionResult(scope);
        }

        var result = await _showtimeService.DeleteShowtimeAsync(showtimeId, cancellationToken);

        // Xóa DB xong hoặc bị chặn bởi rule thì ServiceResult quay lại đây.
        return ToActionResult(result);
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }

    private ObjectResult ToActionResult(CinemaScopeAuthorizationResult result)
    {
        var response = ApiResponse<object>.Fail(result.Message, result.ErrorCode);
        return StatusCode(result.StatusCode, response);
    }
}
