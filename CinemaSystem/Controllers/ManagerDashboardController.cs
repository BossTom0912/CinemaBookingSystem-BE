using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/manager/dashboard")]
[Authorize(Policy = AuthConstants.Policies.CanViewBranchDashboard)]
public sealed class ManagerDashboardController : ControllerBase
{
    private readonly IManagerDashboardService _managerDashboardService;
    private readonly ICinemaScopeAuthorizationService _cinemaScopeAuthorizationService;

    public ManagerDashboardController(
        IManagerDashboardService managerDashboardService,
        ICinemaScopeAuthorizationService cinemaScopeAuthorizationService)
    {
        _managerDashboardService = managerDashboardService;
        _cinemaScopeAuthorizationService = cinemaScopeAuthorizationService;
    }

    [HttpGet]
    [ProducesResponseType(
        typeof(ApiResponse<ManagerDashboardResponse>),
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
    public async Task<IActionResult> GetDashboard(
        [FromQuery] ManagerDashboardQueryRequest request,
        CancellationToken cancellationToken)
    {
        var scope = await _cinemaScopeAuthorizationService.GetUserCinemaScopeAsync(
            User,
            cancellationToken);
        if (!scope.Allowed)
        {
            return ToActionResult(scope);
        }

        var result = await _managerDashboardService.GetDashboardAsync(
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
