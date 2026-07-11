using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/dashboard/staff")]
[Authorize(Policy = AuthConstants.Policies.CanViewStaffShiftReport)]
public sealed class StaffDashboardController : ControllerBase
{
    private readonly IStaffShiftReportService _staffShiftReportService;
    private readonly ICinemaScopeAuthorizationService _cinemaScopeAuthorizationService;

    public StaffDashboardController(
        IStaffShiftReportService staffShiftReportService,
        ICinemaScopeAuthorizationService cinemaScopeAuthorizationService)
    {
        _staffShiftReportService = staffShiftReportService;
        _cinemaScopeAuthorizationService = cinemaScopeAuthorizationService;
    }

    [HttpGet("shift-report")]
    [ProducesResponseType(typeof(ApiResponse<StaffShiftReportResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetShiftReport(
        [FromQuery] StaffShiftReportQueryRequest request,
        CancellationToken cancellationToken)
    {
        var cinemaScope = await _cinemaScopeAuthorizationService.GetUserCinemaScopeAsync(
            User,
            cancellationToken);
        if (!cinemaScope.Allowed)
        {
            return StatusCode(
                cinemaScope.StatusCode,
                ApiResponse<object>.Fail(cinemaScope.Message, cinemaScope.ErrorCode));
        }

        var userId = GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail("User is required.", "USER_REQUIRED"));
        }

        var role = GetRole(User);
        var result = await _staffShiftReportService.GetShiftReportAsync(
            new UserScope(userId, role, cinemaScope.CinemaId),
            request,
            cancellationToken);

        if (result.Success)
        {
            return Ok(result);
        }

        return StatusCode(ToStatusCode(result.ErrorCode), result);
    }

    private static string GetUserId(ClaimsPrincipal user)
    {
        return user.FindFirst(AuthConstants.Claims.UserId)?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
            ?? string.Empty;
    }

    private static string GetRole(ClaimsPrincipal user)
    {
        foreach (var role in new[]
                 {
                     AuthConstants.Roles.Admin,
                     AuthConstants.Roles.Manager,
                     AuthConstants.Roles.Staff,
                     AuthConstants.Roles.Customer
                 })
        {
            if (user.IsInRole(role))
            {
                return role;
            }
        }

        var roleClaim = user.Claims.FirstOrDefault(claim =>
            claim.Type == ClaimTypes.Role || claim.Type == "role");
        return AuthConstants.Roles.Normalize(roleClaim?.Value);
    }

    private static int ToStatusCode(string? errorCode)
    {
        return errorCode switch
        {
            BookingConstants.StaffShiftReportErrorCode.DateRangeRequired
                or BookingConstants.StaffShiftReportErrorCode.InvalidDateRange
                or BookingConstants.StaffShiftReportErrorCode.StaffCinemaMismatch => StatusCodes.Status400BadRequest,
            BookingConstants.StaffShiftReportErrorCode.StaffProfileNotFound
                or BookingConstants.StaffShiftReportErrorCode.CinemaNotFound => StatusCodes.Status404NotFound,
            BookingConstants.StaffShiftReportErrorCode.StaffScopeForbidden
                or BookingConstants.StaffShiftReportErrorCode.CinemaScopeForbidden
                or BookingConstants.StaffShiftReportErrorCode.RoleForbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest
        };
    }
}
