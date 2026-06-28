using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Admin-only HTTP entry point for account administration.
/// </summary>
/// <remarks>
/// The current route hands staff provisioning to <see cref="IAdminService"/>.
/// Runtime DI resolves it to
/// <c>CinemaSystem.Infrastructure.Auth.AdminService</c>, which writes USER,
/// STAFF_PROFILE and EMAIL_VERIFICATION_TOKEN before sending the invitation.
/// Authorization is evaluated before this class through the
/// <c>CanManageUserAndRole</c> policy configured in <c>Program.cs</c>.
/// </remarks>
[ApiController]
[Route("api/admin")]
[Authorize(Policy = AuthConstants.Policies.CanManageUserAndRole)]
public sealed class AdminController : ControllerBase
{
    private readonly IAdminService _adminService;

    public AdminController(IAdminService adminService)
    {
        _adminService = adminService;
    }

    [HttpPost("staff")]
    public async Task<IActionResult> CreateStaff(CreateStaffRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ApiResponse<object>.Fail("Validation failed.", "VALIDATION_ERROR"));
        }

        var result = await _adminService.CreateStaffAsync(
            request.MapTo<Contracts.Auth.CreateStaffRequest>(),
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
