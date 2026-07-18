using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Admin-only HTTP entry point for data-driven account provisioning.
/// </summary>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthConstants.Policies.CanManageUserAndRole)]
public sealed class AdminController : ControllerBase
{
    private readonly IAccountProvisioningService _accountProvisioningService;

    public AdminController(IAccountProvisioningService accountProvisioningService)
    {
        _accountProvisioningService = accountProvisioningService;
    }

    [HttpGet("account-provisioning/roles")]
    public async Task<IActionResult> GetAssignableAccountRoles(CancellationToken cancellationToken)
    {
        var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                "Authentication user ID was not found.",
                "USER_ID_NOT_FOUND"));
        }

        var result = await _accountProvisioningService.GetAssignableRolesAsync(
            actorUserId,
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("users")]
    public async Task<IActionResult> ProvisionAccount(
        ProvisionManagedAccountRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", "VALIDATION_ERROR"));
        }

        var actorUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(actorUserId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                "Authentication user ID was not found.",
                "USER_ID_NOT_FOUND"));
        }

        var result = await _accountProvisioningService.ProvisionAsync(
            actorUserId,
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
}
