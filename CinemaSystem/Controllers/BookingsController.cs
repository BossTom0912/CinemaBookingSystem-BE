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
    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
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

    [HttpGet("{bookingId}/confirm-time-change")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmTimeChange(
        string bookingId,
        [FromQuery] bool accept,
        [FromQuery] string token,
        CancellationToken cancellationToken)
    {
        var result = await _bookingService.ConfirmTimeChangeAsync(
            bookingId,
            accept,
            token,
            cancellationToken);

        var response = result.Success
            ? ApiResponse<bool>.Ok(result.Data, result.Message)
            : ApiResponse<bool>.Fail(
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
