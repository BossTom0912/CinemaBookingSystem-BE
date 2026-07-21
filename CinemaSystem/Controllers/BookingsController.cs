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

        // Nếu truy cập từ Trình duyệt web (Click link từ Email), trả về giao diện HTML phản hồi chuyên nghiệp
        if (Request.Headers["Accept"].ToString().Contains("text/html") || !Request.Headers.ContainsKey("X-Requested-With"))
        {
            string htmlContent;
            if (result.Success)
            {
                if (result.Message != null && result.Message.StartsWith("ALREADY_ACCEPTED:"))
                {
                    var cleanMsg = result.Message.Replace("ALREADY_ACCEPTED:", "").Trim();
                    htmlContent = GetConfirmationSuccessHtml(bookingId, "BẠN ĐÃ XÁC NHẬN ĐỒNG Ý TRƯỚC ĐÓ", cleanMsg);
                }
                else if (result.Message != null && result.Message.StartsWith("ALREADY_REFUNDED:"))
                {
                    var cleanMsg = result.Message.Replace("ALREADY_REFUNDED:", "").Trim();
                    htmlContent = GetConfirmationSuccessHtml(bookingId, "BẠN ĐÃ YÊU CẦU HOÀN TIỀN 100% TRƯỚC ĐÓ", cleanMsg);
                }
                else if (accept)
                {
                    var msg = "Cảm ơn Quý khách đã xác nhận tham dự suất chiếu mới! Đơn hàng của bạn đã được cập nhật trạng thái hợp lệ. Các quyền lợi đền bù (Voucher / Nâng hạng ghế) đã được đính kèm vào tài khoản của bạn. Chúc bạn có trải nghiệm xem phim vui vẻ tại CinemaSystem!";
                    htmlContent = GetConfirmationSuccessHtml(bookingId, "XÁC NHẬN GIỮ VÉ & XEM SUẤT CHIẾU MỚI THÀNH CÔNG", msg);
                }
                else
                {
                    var msg = "Yêu cầu hoàn tiền của Quý khách cho đơn hàng đã được tiếp nhận thành công. Đơn hàng của bạn đã được chuyển sang trạng thái chờ xử lý hoàn 100% tiền vé tự động về tài khoản thanh toán ban đầu. CinemaSystem chân thành cảm ơn sự đồng hành của Quý khách!";
                    htmlContent = GetConfirmationSuccessHtml(bookingId, "ĐÃ TIẾP NHẬN YÊU CẦU HOÀN TIỀN 100%", msg);
                }
            }
            else
            {
                htmlContent = GetConfirmationErrorHtml(bookingId, result.Message);
            }
            return Content(htmlContent, "text/html; charset=utf-8");
        }

        var response = result.Success
            ? ApiResponse<bool>.Ok(result.Data, result.Message)
            : ApiResponse<bool>.Fail(
                result.Message,
                result.ErrorCode,
                result.Errors);

        return StatusCode(result.StatusCode, response);
    }

    private static string GetConfirmationSuccessHtml(string bookingId, string title, string message)
    {
        return $$"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>{{title}} - CinemaSystem</title>
                <style>
                    body { font-family: Arial, sans-serif; background-color: #0f172a; color: #f8fafc; margin: 0; padding: 40px 20px; display: flex; justify-content: center; align-items: center; min-height: 80vh; }
                    .card { background-color: #1e293b; border-radius: 16px; border: 1px solid #334155; padding: 40px; max-width: 500px; text-align: center; box-shadow: 0 20px 40px rgba(0,0,0,0.5); }
                    .icon { width: 70px; height: 70px; background: #059669; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 36px; margin: 0 auto 20px auto; }
                    h2 { color: #ffffff; margin-top: 0; font-size: 20px; }
                    p { color: #94a3b8; font-size: 14px; line-height: 1.6; }
                    .booking-badge { display: inline-block; background-color: #334155; color: #38bdf8; font-family: monospace; font-size: 15px; font-weight: bold; padding: 6px 14px; border-radius: 8px; margin: 15px 0; }
                    .btn { display: inline-block; background: linear-gradient(135deg, #2563eb, #3b82f6); color: white; text-decoration: none; padding: 12px 28px; border-radius: 10px; font-weight: bold; margin-top: 25px; transition: all 0.3s; }
                    .btn:hover { opacity: 0.9; }
                </style>
            </head>
            <body>
                <div class="card">
                    <div class="icon">✓</div>
                    <h2>{{title}}</h2>
                    <div class="booking-badge">Mã đơn hàng: #{{bookingId}}</div>
                    <p>{{message}}</p>
                    <a href="http://localhost:5173" class="btn">Quay lại Trang Chủ CinemaSystem</a>
                </div>
            </body>
            </html>
            """;
    }

    private static string GetConfirmationErrorHtml(string bookingId, string errorMessage)
    {
        return $$"""
            <!DOCTYPE html>
            <html lang="vi">
            <head>
                <meta charset="utf-8">
                <meta name="viewport" content="width=device-width, initial-scale=1.0">
                <title>Thông Báo Từ Hệ Thống - CinemaSystem</title>
                <style>
                    body { font-family: Arial, sans-serif; background-color: #0f172a; color: #f8fafc; margin: 0; padding: 40px 20px; display: flex; justify-content: center; align-items: center; min-height: 80vh; }
                    .card { background-color: #1e293b; border-radius: 16px; border: 1px solid #334155; padding: 40px; max-width: 500px; text-align: center; box-shadow: 0 20px 40px rgba(0,0,0,0.5); }
                    .icon { width: 70px; height: 70px; background: #dc2626; color: white; border-radius: 50%; display: flex; align-items: center; justify-content: center; font-size: 36px; margin: 0 auto 20px auto; }
                    h2 { color: #ffffff; margin-top: 0; font-size: 20px; }
                    p { color: #94a3b8; font-size: 14px; line-height: 1.6; }
                    .booking-badge { display: inline-block; background-color: #334155; color: #f87171; font-family: monospace; font-size: 15px; font-weight: bold; padding: 6px 14px; border-radius: 8px; margin: 15px 0; }
                    .btn { display: inline-block; background: linear-gradient(135deg, #2563eb, #3b82f6); color: white; text-decoration: none; padding: 12px 28px; border-radius: 10px; font-weight: bold; margin-top: 25px; transition: all 0.3s; }
                    .btn:hover { opacity: 0.9; }
                </style>
            </head>
            <body>
                <div class="card">
                    <div class="icon">✕</div>
                    <h2>THÔNG BÁO TỪ HỆ THỐNG</h2>
                    <div class="booking-badge">Mã đơn hàng: #{{bookingId}}</div>
                    <p>{{errorMessage}}</p>
                    <a href="http://localhost:5173" class="btn">Quay lại Trang Chủ CinemaSystem</a>
                </div>
            </body>
            </html>
            """;
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

    [HttpPost("counter")]
    [Authorize(Policy = AuthConstants.Policies.CanScanTicket)]
    public async Task<IActionResult> CreateCounterBooking(
        CreateCounterBookingRequest request,
        CancellationToken cancellationToken)
    {
        var staffProfileId = GetStaffProfileId() ?? GetUserId() ?? CinemaSystem.Domain.Constants.DomainConstants.Staff.Unknown;
        var staffCinemaId = GetStaffCinemaId() ?? string.Empty;

        var result = await _bookingService.CreateCounterBookingAsync(
            request,
            staffProfileId,
            staffCinemaId,
            cancellationToken);

        var response = result.Success
            ? ApiResponse<BookingDetailsResponse>.Ok(result.Data, result.Message)
            : ApiResponse<BookingDetailsResponse>.Fail(
                result.Message,
                result.ErrorCode,
                result.Errors);

        return StatusCode(result.StatusCode, response);
    }

    private string? GetStaffProfileId() => User.FindFirst("staffProfileId")?.Value;
    private string? GetStaffCinemaId() => User.FindFirst("cinemaId")?.Value;

    private string? GetUserId()
    {
        return User.FindFirst(AuthConstants.Claims.UserId)?.Value
            ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
    }
}
