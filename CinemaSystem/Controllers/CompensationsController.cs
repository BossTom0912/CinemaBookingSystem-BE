using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Compensations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
public sealed class CompensationsController : ControllerBase
{
    private readonly ICancellationCompensationService _service;
    private readonly IClock _clock;

    public CompensationsController(
        ICancellationCompensationService service,
        IClock clock)
    {
        _service = service;
        _clock = clock;
    }

    [HttpGet("api/customer/compensations")]
    [Authorize(Policy = AuthConstants.Policies.CanBookTicket)]
    public async Task<IActionResult> GetMine(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _service.GetMineAsync(userId, cancellationToken);
        return StatusCode(
            result.StatusCode,
            result.Success
                ? ApiResponse<IReadOnlyList<CompensationResponse>>.Ok(
                    result.Data,
                    result.Message)
                : ApiResponse<IReadOnlyList<CompensationResponse>>.Fail(
                    result.Message,
                    result.ErrorCode,
                    result.Errors));
    }

    [HttpPost("api/staff/compensations/combos/redeem")]
    [Authorize(Policy = AuthConstants.Policies.CanScanTicket)]
    public async Task<IActionResult> RedeemCombo(
        RedeemCompensationComboRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _service.RedeemComboAsync(
            request.VoucherCode,
            userId,
            _clock.UtcNow,
            cancellationToken);
        return StatusCode(
            result.StatusCode,
            result.Success
                ? ApiResponse<RedeemCompensationComboResponse>.Ok(
                    result.Data,
                    result.Message)
                : ApiResponse<RedeemCompensationComboResponse>.Fail(
                    result.Message,
                    result.ErrorCode,
                    result.Errors));
    }

    private string? GetUserId() =>
        User.FindFirst(AuthConstants.Claims.UserId)?.Value
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
}
