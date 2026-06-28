using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Customers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace CinemaSystem.Controllers;

/// <summary>
/// Authenticated user profile, credential and booking-history HTTP entry point.
/// </summary>
/// <remarks>
/// Actions extract the user id from the JWT and hand processing to
/// <see cref="ICustomerService"/>. Runtime DI maps it to
/// <c>CinemaSystem.Infrastructure.Services.CustomerService</c>, which accesses
/// USER, CUSTOMER_PROFILE, EMAIL_VERIFICATION_TOKEN and booking relations.
/// </remarks>
[ApiController]
[Route("api/customer")]
[Authorize]
public sealed class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _customerService.GetProfileAsync(userId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _customerService.UpdateProfileAsync(userId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _customerService.ChangePasswordAsync(userId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("request-email-change")]
    public async Task<IActionResult> RequestEmailChange(UpdateEmailRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _customerService.RequestEmailUpdateAsync(userId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("verify-email-change")]
    public async Task<IActionResult> VerifyEmailChange(VerifyEmailUpdateRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _customerService.VerifyEmailUpdateAsync(userId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookingHistory(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _customerService.GetBookingHistoryAsync(userId, cancellationToken);
        return ToActionResult(result);
    }

    private string? GetUserId()
    {
        return User.FindFirstValue(ClaimTypes.NameIdentifier);
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }
}
