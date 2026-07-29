using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Seats;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/seat-types")]
[Authorize(Roles = AuthConstants.Roles.Manager + "," + AuthConstants.Roles.Admin)]
public sealed class SeatTypesController : ControllerBase
{
    private readonly ISeatTypeCatalogService _service;

    public SeatTypesController(ISeatTypeCatalogService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken cancellationToken = default)
        => ToActionResult(await _service.GetAllAsync(includeInactive, cancellationToken));

    [HttpPost]
    public async Task<IActionResult> Create(
        UpsertSeatTypeRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await _service.CreateAsync(request, cancellationToken));

    [HttpPut("{seatTypeId}")]
    public async Task<IActionResult> Update(
        string seatTypeId,
        UpsertSeatTypeRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await _service.UpdateAsync(
            seatTypeId,
            request,
            cancellationToken));

    [HttpDelete("{seatTypeId}")]
    public async Task<IActionResult> Delete(
        string seatTypeId,
        CancellationToken cancellationToken)
        => ToActionResult(await _service.DeleteAsync(
            seatTypeId,
            cancellationToken));

    [HttpPost("{seatTypeId}/merge")]
    public async Task<IActionResult> Merge(
        string seatTypeId,
        MergeSeatTypeRequest request,
        CancellationToken cancellationToken)
        => ToActionResult(await _service.MergeAsync(
            seatTypeId,
            request.ReplacementSeatTypeId,
            cancellationToken));

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(
                result.Message,
                result.ErrorCode,
                result.Errors);
        return StatusCode(result.StatusCode, response);
    }
}
