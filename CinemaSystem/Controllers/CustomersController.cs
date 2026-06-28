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

        // Bước tiếp theo: CustomerService tại Infrastructure/Services đọc
        // USER + CUSTOMER_PROFILE qua CinemaDbContext; Controller chỉ lấy userId
        // từ JWT và không truy vấn DB trực tiếp.
        var result = await _customerService.GetProfileAsync(userId, cancellationToken);

        // Dữ liệu profile quay lại đây để bọc ApiResponse.
        return ToActionResult(result);
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // Bước tiếp theo: CustomerService (Infrastructure/Services) áp dụng thay
        // đổi lên USER/CUSTOMER_PROFILE và SaveChangesAsync.
        var result = await _customerService.UpdateProfileAsync(userId, request, cancellationToken);

        // Entity sau cập nhật được service map thành DTO rồi trả về Controller.
        return ToActionResult(result);
    }

    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // Bước tiếp theo: CustomerService kiểm password cũ, gọi password hasher
        // tại Infrastructure/Security và lưu password hash mới vào USER.
        var result = await _customerService.ChangePasswordAsync(userId, request, cancellationToken);

        // Kết quả đổi password quay lại đây; Controller không xử lý secret.
        return ToActionResult(result);
    }

    [HttpPost("request-email-change")]
    public async Task<IActionResult> RequestEmailChange(UpdateEmailRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // Bước tiếp theo: CustomerService kiểm email trùng, tạo OTP hash trong
        // EMAIL_VERIFICATION_TOKEN và gọi IEmailSender tại Infrastructure/Email.
        var result = await _customerService.RequestEmailUpdateAsync(userId, request, cancellationToken);

        // Email gửi xong hoặc lỗi sẽ được biểu diễn bằng ServiceResult tại đây.
        return ToActionResult(result);
    }

    [HttpPost("verify-email-change")]
    public async Task<IActionResult> VerifyEmailChange(VerifyEmailUpdateRequest request, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // Bước tiếp theo: CustomerService xác minh OTP hash và expiry trong DB,
        // sau đó cập nhật USER.email; không tin trực tiếp email/OTP từ request.
        var result = await _customerService.VerifyEmailUpdateAsync(userId, request, cancellationToken);

        // Kết quả persistence quay lại Controller để tạo HTTP response.
        return ToActionResult(result);
    }

    [HttpGet("bookings")]
    public async Task<IActionResult> GetBookingHistory(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        // Bước tiếp theo: CustomerService tại Infrastructure/Services đi từ
        // CUSTOMER_PROFILE -> BOOKING -> SHOWTIME/MOVIE/ROOM/CINEMA/SEAT để dựng
        // lịch sử; EF query không đặt trong Controller để giữ API layer mỏng.
        var result = await _customerService.GetBookingHistoryAsync(userId, cancellationToken);

        // Danh sách DTO quay lại đây để chuẩn hóa ApiResponse.
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
