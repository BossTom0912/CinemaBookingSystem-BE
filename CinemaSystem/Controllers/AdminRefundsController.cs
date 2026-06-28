using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Refunds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/admin/refunds")]
[Authorize(Policy = AuthConstants.Policies.CanManageSystem)]
public sealed class AdminRefundsController : ControllerBase
{
    private readonly IAdminRefundService _adminRefundService;
    private readonly IManualRefundService _manualRefundService;

    public AdminRefundsController(
        IAdminRefundService adminRefundService,
        IManualRefundService manualRefundService)
    {
        _adminRefundService = adminRefundService;
        _manualRefundService = manualRefundService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(ApiResponse<PagedList<RefundDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRefunds(
        [FromQuery] string status = "PENDING",
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
        => ToActionResult(await _adminRefundService.GetRefundsAsync(status, pageIndex, pageSize, cancellationToken));

    [HttpPost("{bookingId}/confirm")]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> ConfirmRefund(
        [FromRoute] string bookingId,
        CancellationToken cancellationToken)
        => await WithAdminId(userId => _adminRefundService.ConfirmRefundAsync(bookingId, userId, cancellationToken));

    [HttpGet("manual")]
    public async Task<IActionResult> GetManualRefunds(CancellationToken cancellationToken)
        => ToActionResult(await _manualRefundService.GetPendingAsync(cancellationToken));

    [HttpPost("{refundId}/assign")]
    public async Task<IActionResult> Assign(
        string refundId,
        CancellationToken cancellationToken)
        => await WithAdminId(userId => _manualRefundService.AssignAsync(refundId, userId, cancellationToken));

    [HttpPost("{refundId}/manual-confirm")]
    public async Task<IActionResult> Confirm(
        string refundId,
        ManualRefundConfirmationRequest request,
        CancellationToken cancellationToken)
        => await WithAdminId(userId => _manualRefundService.ConfirmAsync(refundId, userId, request, cancellationToken));

    private async Task<IActionResult> WithAdminId<T>(
        Func<string, Task<ServiceResult<T>>> operation)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized.", "UNAUTHORIZED"));
        }

        return ToActionResult(await operation(userId));
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);
        return StatusCode(result.StatusCode, response);
    }
}
