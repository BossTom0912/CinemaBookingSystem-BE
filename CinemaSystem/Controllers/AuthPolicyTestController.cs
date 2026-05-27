using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/auth-test")]
public sealed class AuthPolicyTestController : ControllerBase
{
    [HttpGet("customer")]
    [Authorize(Policy = AuthConstants.Policies.CanBookTicket)]
    public IActionResult CustomerOnly()
    {
        return Ok(ApiResponse<UserProfileResponse>.Ok(CurrentUser(), "Customer policy accepted."));
    }

    [HttpGet("admin")]
    [Authorize(Policy = AuthConstants.Policies.CanManageSystem)]
    public IActionResult AdminOnly()
    {
        return Ok(ApiResponse<UserProfileResponse>.Ok(CurrentUser(), "Admin policy accepted."));
    }

    private UserProfileResponse CurrentUser()
    {
        return new UserProfileResponse
        {
            UserId = User.FindFirst("userId")?.Value ?? string.Empty,
            Email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty,
            FullName = User.Identity?.Name ?? string.Empty,
            Role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty
        };
    }
}
