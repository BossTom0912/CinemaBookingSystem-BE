using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Refunds;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/manager/refunds")]
[Authorize(Policy = AuthConstants.Policies.CanCancelShowtimeAndRefund)]
public sealed class ManagerRefundsController : ControllerBase
{
    private readonly IRefundService _refundService;
    private readonly ICinemaScopeAuthorizationService _cinemaScopeAuthorizationService;

    public ManagerRefundsController(
        IRefundService refundService,
        ICinemaScopeAuthorizationService cinemaScopeAuthorizationService)
    {
        _refundService = refundService;
        _cinemaScopeAuthorizationService = cinemaScopeAuthorizationService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<RefundResponse>>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetRefunds(
        [FromQuery] RefundQueryRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await _cinemaScopeAuthorizationService.GetUserCinemaScopeAsync(
            User,
            cancellationToken);
        if (!scope.Allowed)
        {
            return ToActionResult(scope);
        }

        var result = await _refundService.GetRefundsAsync(
            scope.CinemaId,
            request,
            cancellationToken);

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
