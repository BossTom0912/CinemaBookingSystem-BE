using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Cinemas;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Cinema management & public catalogue API endpoints.
/// </summary>
[ApiController]
[Route("api/cinemas")]
public sealed class CinemasController : ControllerBase
{
    private readonly ICinemaService _cinemaService;

    public CinemasController(ICinemaService cinemaService)
    {
        _cinemaService = cinemaService ?? throw new ArgumentNullException(nameof(cinemaService));
    }

    /// <summary>
    /// Danh sách tất cả rạp chiếu (Public).
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCinemas(CancellationToken cancellationToken)
    {
        var result = await _cinemaService.GetCinemasAsync(cancellationToken);
        return ToActionResult(result.MapDataTo<IReadOnlyList<CinemaResponse>, IReadOnlyList<CinemaResponse>>());
    }

    /// <summary>
    /// Lấy thông tin chi tiết một rạp chiếu theo ID (Public).
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCinemaById(string id, CancellationToken cancellationToken)
    {
        var result = await _cinemaService.GetCinemaByIdAsync(id, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Tạo rạp chiếu mới (Dành cho Admin).
    /// </summary>
    [HttpPost]
    [Authorize(Roles = AuthConstants.Roles.Admin + ",admin,ROLE_ADMIN")]
    public async Task<IActionResult> CreateCinema([FromBody] CreateCinemaRequest request, CancellationToken cancellationToken)
    {
        var result = await _cinemaService.CreateCinemaAsync(request, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Cập nhật thông tin rạp chiếu (Dành cho Admin).
    /// </summary>
    [HttpPut("{id}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + ",admin,ROLE_ADMIN")]
    public async Task<IActionResult> UpdateCinema(string id, [FromBody] UpdateCinemaRequest request, CancellationToken cancellationToken)
    {
        var result = await _cinemaService.UpdateCinemaAsync(id, request, cancellationToken);
        return ToActionResult(result);
    }

    /// <summary>
    /// Xóa hoặc Tạm dừng rạp chiếu (Dành cho Admin).
    /// </summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + ",admin,ROLE_ADMIN")]
    public async Task<IActionResult> DeleteCinema(string id, CancellationToken cancellationToken)
    {
        var result = await _cinemaService.DeleteCinemaAsync(id, cancellationToken);
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
