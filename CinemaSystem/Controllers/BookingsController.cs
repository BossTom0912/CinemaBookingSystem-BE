using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/bookings")]
[Authorize(Policy = AuthConstants.Policies.CanBookTicket)]
public sealed class BookingsController : ControllerBase
{
    private readonly IBookingService _bookingService;
    private readonly ICheckoutService _checkoutService;

    public BookingsController(
        IBookingService bookingService,
        ICheckoutService checkoutService)
    {
        _bookingService = bookingService;
        _checkoutService = checkoutService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateBooking(
        CreateBookingRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _bookingService.CreateBookingAsync(
            request,
            userId,
            cancellationToken);

        var response = result.Success
            ? ApiResponse<BookingResponse>.Ok(result.Data, result.Message)
            : ApiResponse<BookingResponse>.Fail(
                result.Message,
                result.ErrorCode,
                result.Errors);

        return StatusCode(result.StatusCode, response);
    }

    [HttpGet("{bookingId}")]
    public async Task<IActionResult> GetBookingDetails(
        string bookingId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _bookingService.GetBookingDetailsAsync(
            bookingId,
            userId,
            cancellationToken);

        var response = result.Success
            ? ApiResponse<BookingDetailsResponse>.Ok(result.Data, result.Message)
            : ApiResponse<BookingDetailsResponse>.Fail(
                result.Message,
                result.ErrorCode,
                result.Errors);

        return StatusCode(result.StatusCode, response);
    }

    [HttpGet("my-bookings")]
    public async Task<IActionResult> GetMyBookings(
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _bookingService.GetMyBookingsAsync(
            userId,
            cancellationToken);

        var response = result.Success
            ? ApiResponse<IReadOnlyList<BookingResponse>>.Ok(
                result.Data,
                result.Message)
            : ApiResponse<IReadOnlyList<BookingResponse>>.Fail(
                result.Message,
                result.ErrorCode,
                result.Errors);

        return StatusCode(result.StatusCode, response);
    }

    [HttpPost("checkout")]
    [ProducesResponseType(
        typeof(ApiResponse<CheckoutResponse>),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status403Forbidden)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ApiResponse<object>),
        StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Checkout(
        CheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized(ApiResponse<object>.Fail(
                "Unauthorized.",
                BookingConstants.ErrorCodes.Unauthorized));
        }

        var result = await _checkoutService.CheckoutAsync(
            userId,
            request,
            cancellationToken);

        var response = result.Success
            ? ApiResponse<CheckoutResponse>.Ok(result.Data, result.Message)
            : ApiResponse<CheckoutResponse>.Fail(
                result.Message,
                result.ErrorCode,
                result.Errors);

        return StatusCode(result.StatusCode, response);
    }

    private string? GetUserId()
    {
        return User.FindFirst("userId")?.Value
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
