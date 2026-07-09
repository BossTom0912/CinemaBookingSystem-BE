using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Test-only style endpoints that demonstrate role-policy enforcement.
/// </summary>
/// <remarks>
/// Authorization is completed by ASP.NET before these actions execute. The
/// methods do not hand off to an Infrastructure service; they only return the
/// claims produced by <c>JwtTokenService</c>.
/// </remarks>
[ApiController]
[Route("api/auth-test")]
public sealed class AuthPolicyTestController : ControllerBase
{
    [HttpGet("customer")]
    [Authorize(Policy = AuthConstants.Policies.CanBookTicket)]
    public IActionResult CustomerOnly()
    {
        // Trước khi vào đây, AuthorizationMiddleware trong Program.cs đã đọc
        // role claim do JwtTokenService tạo và kiểm policy CanBookTicket.
        // Luồng dừng tại Controller vì endpoint này chỉ chứng minh policy hoạt động.
        return Ok(ApiResponse<UserProfileResponse>.Ok(CurrentUser(), "Customer policy accepted."));
    }

    [HttpGet("admin")]
    [Authorize(Policy = AuthConstants.Policies.CanManageSystem)]
    public IActionResult AdminOnly()
    {
        // Trước khi vào đây, AuthorizationMiddleware đã yêu cầu role ADMIN.
        // Không gọi service/DB vì đây là endpoint test policy, không phải use case.
        return Ok(ApiResponse<UserProfileResponse>.Ok(CurrentUser(), "Admin policy accepted."));
    }

    private UserProfileResponse CurrentUser()
    {
        return new UserProfileResponse
        {
            UserId = User.FindFirst(AuthConstants.Claims.UserId)?.Value ?? string.Empty,
            Email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value ?? string.Empty,
            FullName = User.Identity?.Name ?? string.Empty,
            Role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty
        };
    }
}
