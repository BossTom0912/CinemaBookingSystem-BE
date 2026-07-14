using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Điểm vào HTTP của Customer cho tạo đơn, xem đơn, xác nhận đổi giờ và hủy đơn.
/// </summary>
/// <remarks>
/// Luồng tiếp theo: <see cref="IBookingService"/> -> <c>BookingService</c> tại
/// <c>CinemaSystem.Infrastructure/Services/BookingService.cs</c> -> các bảng
/// BOOKING, BOOKING_SEAT, SHOWTIME_SEAT, TICKET, PAYMENT và REFUND. Controller
/// chỉ lấy userId từ JWT, gọi service và đóng gói <c>ApiResponse</c>.
/// </remarks>
[ApiController]
[Route("api/bookings")]
[Authorize(Policy = AuthConstants.Policies.CanBookTicket)]
public sealed class BookingsController : ControllerBase
{
    private const string IdempotencyKeyHeaderName = "Idempotency-Key";
    private readonly IBookingService _bookingService;
    public BookingsController(IBookingService bookingService)
    {
        _bookingService = bookingService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateBooking(
        CreateBookingRequest request,
        [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (!string.IsNullOrWhiteSpace(idempotencyKey)
            && !Guid.TryParse(idempotencyKey, out _))
        {
            return BadRequest(ApiResponse<BookingResponse>.Fail(
                "Idempotency-Key must be a valid UUID.",
                "INVALID_IDEMPOTENCY_KEY"));
        }

        var result = await _bookingService.CreateBookingAsync(
            request,
            userId,
            Guid.TryParse(idempotencyKey, out var clientRequestId) ? clientRequestId : null,
            cancellationToken);

        var response = result.Success
            ? ApiResponse<BookingResponse>.Ok(result.Data, result.Message)
            : ApiResponse<BookingResponse>.Fail(
                result.Message,
                result.ErrorCode,
                result.Errors);

        return StatusCode(result.StatusCode, response);
    }

    [HttpGet("checkout-recovery")]
    public async Task<IActionResult> GetCheckoutRecovery(
        [FromHeader(Name = IdempotencyKeyHeaderName)] string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        if (!Guid.TryParse(idempotencyKey, out var clientRequestId))
        {
            return BadRequest(ApiResponse<CheckoutRecoveryResponse>.Fail(
                "Idempotency-Key must be a valid UUID.",
                "INVALID_IDEMPOTENCY_KEY"));
        }

        var result = await _bookingService.GetCheckoutRecoveryAsync(
            userId,
            clientRequestId,
            cancellationToken);

        var response = result.Success
            ? ApiResponse<CheckoutRecoveryResponse>.Ok(result.Data, result.Message)
            : ApiResponse<CheckoutRecoveryResponse>.Fail(
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

    [HttpPost("{bookingId}/cancel")]
    public async Task<IActionResult> CancelBooking(
        string bookingId,
        CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var result = await _bookingService.CancelPendingBookingAsync(
            bookingId,
            userId,
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
        return User.FindFirst(AuthConstants.Claims.UserId)?.Value
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
