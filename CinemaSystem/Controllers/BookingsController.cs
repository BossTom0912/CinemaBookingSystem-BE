using System.Security.Claims;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Contracts.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Customer booking and checkout HTTP entry point.
/// </summary>
/// <remarks>
/// Create/detail/history calls continue through <see cref="IBookingService"/>
/// to <c>CinemaSystem.Infrastructure.Services.BookingService</c>. The richer
/// checkout route continues through <see cref="ICheckoutService"/> to
/// <c>CinemaSystem.Infrastructure.Bookings.CheckoutService</c>, where seat,
/// food, voucher and booking rows are validated and committed in a transaction.
/// Payment is a separate next step handled by <c>PaymentController</c>.
/// </remarks>
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

        // Bước tiếp theo: IBookingService được DI map sang BookingService tại
        // CinemaSystem.Infrastructure/Services/BookingService.cs. Service kiểm
        // customer/showtime/seat, tính giá và tạo BOOKING PENDING_PAYMENT.
        var result = await _bookingService.CreateBookingAsync(
            request,
            userId,
            cancellationToken);

        // Luồng quay về: BookingService trả BookingResponse/ServiceResult; phần
        // dưới chỉ chuyển nó thành response HTTP, không xử lý thêm nghiệp vụ.
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

        // Bước tiếp theo: BookingService (Infrastructure/Services) load toàn bộ
        // aggregate BOOKING -> SHOWTIME/MOVIE/ROOM/CINEMA/SEAT/TICKET/F&B và
        // kiểm user hiện tại có phải chủ booking không.
        var result = await _bookingService.GetBookingDetailsAsync(
            bookingId,
            userId,
            cancellationToken);

        // Aggregate được project thành DTO rồi quay lại đây để trả client.
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

        // Bước tiếp theo: BookingService query booking theo userId lấy từ JWT;
        // service nằm ở Infrastructure/Services vì cần EF Core/CinemaDbContext.
        var result = await _bookingService.GetMyBookingsAsync(
            userId,
            cancellationToken);

        // Danh sách đã map quay lại Controller để bọc ApiResponse.
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

        // Bước tiếp theo: ICheckoutService được DI map sang CheckoutService tại
        // CinemaSystem.Infrastructure/Bookings/CheckoutService.cs. Service mở
        // SQL transaction, kiểm lock ghế/F&B/voucher và tạo booking đầy đủ.
        var result = await _checkoutService.CheckoutAsync(
            userId,
            request,
            cancellationToken);

        // Checkout chỉ tạo PENDING_PAYMENT. Sau response này, client tiếp tục gọi
        // PaymentController.CreatePayment; việc thanh toán không chạy tự động ở đây.
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
