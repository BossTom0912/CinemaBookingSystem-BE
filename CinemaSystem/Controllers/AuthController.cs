using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// HTTP entry point for registration, OTP verification, login, token rotation,
/// logout and password recovery.
/// </summary>
/// <remarks>
/// Processing continues through <see cref="IAuthService"/>. Runtime DI maps that
/// interface to <c>CinemaSystem.Infrastructure.Auth.AuthService</c>, which uses
/// <c>CinemaDbContext</c>, the password/OTP services, JWT generation and email.
/// Every action maps the returned <c>ServiceResult</c> to the shared
/// <c>ApiResponse</c>; business rules must stay in the service, not here.
/// </remarks>
[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken cancellationToken)
    {
        // Bước tiếp theo: chuyển DTO sang IAuthService.RegisterCustomerAsync.
        // DI sẽ gọi AuthService tại CinemaSystem.Infrastructure/Auth/AuthService.cs
        // vì việc kiểm tra email/password, tạo USER/CUSTOMER_PROFILE/OTP và gửi email
        // là nghiệp vụ + hạ tầng, không thuộc trách nhiệm nhận/trả HTTP của Controller.
        var result = await _authService.RegisterCustomerAsync(
            request.MapTo<Contracts.Auth.RegisterRequest>(),
            cancellationToken);

        // Luồng quay về: AuthService trả ServiceResult; Controller chỉ đổi nó thành
        // ApiResponse và HTTP status tương ứng để gửi lại client.
        return ToActionResult(result);
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        // Bước tiếp theo: AuthService (Infrastructure/Auth) đọc USER và
        // EMAIL_VERIFICATION_TOKEN, xác minh OTP hash rồi kích hoạt tài khoản.
        var result = await _authService.VerifyEmailAsync(
            request.MapTo<Contracts.Auth.VerifyEmailRequest>(),
            cancellationToken);

        // Sau khi service lưu DB xong, kết quả quay lại đây để chuẩn hóa response.
        return ToActionResult(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        // Bước tiếp theo: IAuthService được DI map sang AuthService trong
        // CinemaSystem.Infrastructure/Auth. AuthService kiểm USER/ROLE/password,
        // sau đó gọi JwtTokenService trong Infrastructure/Identity để tạo JWT và
        // lưu refresh-token hash qua CinemaDbContext.
        var result = await _authService.LoginAsync(
            request.MapTo<Contracts.Auth.LoginRequest>(),
            cancellationToken);

        // Luồng quay về: AuthService -> ServiceResult<AuthResponse> -> mapping DTO
        // API tại Controller -> client nhận access token, refresh token và role.
        return ToActionResult(result.MapDataTo<Contracts.Auth.AuthResponse, AuthResponse>());
    }

    [HttpPost("google-login")]
    [AllowAnonymous]
    public async Task<IActionResult> GoogleLogin(GoogleLoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.GoogleLoginAsync(
            request.MapTo<Contracts.Auth.GoogleLoginRequest>(),
            cancellationToken);
        return ToActionResult(result.MapDataTo<Contracts.Auth.AuthResponse, AuthResponse>());
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        // Bước tiếp theo: AuthService (Infrastructure/Auth) hash token client gửi,
        // đối chiếu REFRESH_TOKEN, revoke token cũ rồi gọi JwtTokenService tạo cặp
        // access/refresh token mới. Controller không tự thao tác token hoặc DB.
        var result = await _authService.RefreshTokenAsync(
            request.MapTo<Contracts.Auth.RefreshTokenRequest>(),
            cancellationToken);

        // Kết quả quay lại Controller để map Contracts DTO sang API DTO.
        return ToActionResult(result.MapDataTo<Contracts.Auth.TokenResponse, TokenResponse>());
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        // Bước tiếp theo: AuthService tại Infrastructure/Auth tìm refresh-token
        // hash trong DB và đánh dấu revoke; lý do là logout cần persistence chứ
        // không chỉ xóa dữ liệu ở Controller.
        var result = await _authService.LogoutAsync(
            request.MapTo<Contracts.Auth.LogoutRequest>(),
            cancellationToken);

        // Sau khi revoke hoàn tất, ServiceResult quay lại để tạo HTTP response.
        return ToActionResult(result);
    }

    [HttpPost("resend-verification-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerificationOtp(
        ResendVerificationOtpRequest request,
        CancellationToken cancellationToken)
    {
        // Bước tiếp theo: AuthService kiểm trạng thái tài khoản/cooldown/send limit,
        // tạo OTP bằng Infrastructure/Security, lưu OTP hash và gọi IEmailSender
        // trong Infrastructure/Email. Controller không được nhìn thấy OTP thô.
        var result = await _authService.ResendVerificationOtpAsync(
            request.MapTo<Contracts.Auth.ResendVerificationOtpRequest>(),
            cancellationToken);

        // Email/DB xử lý xong thì kết quả quay lại đây để trả client.
        return ToActionResult(result);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        // Bước tiếp theo: AuthService (Infrastructure/Auth) kiểm USER, tạo token
        // purpose PASSWORD_RESET và chuyển việc gửi OTP sang Infrastructure/Email.
        var result = await _authService.ForgotPasswordAsync(
            request.MapTo<Contracts.Auth.ForgotPasswordRequest>(),
            cancellationToken);

        // Controller chỉ trả kết quả thành công/lỗi, không trả OTP.
        return ToActionResult(result);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        // Bước tiếp theo: AuthService xác minh OTP reset trong DB, gọi
        // Pbkdf2PasswordHasher tại Infrastructure/Security để hash password mới
        // và revoke toàn bộ refresh token còn hiệu lực của user.
        var result = await _authService.ResetPasswordAsync(
            request.MapTo<Contracts.Auth.ResetPasswordRequest>(),
            cancellationToken);

        // Khi transaction nghiệp vụ hoàn tất, kết quả quay lại Controller.
        return ToActionResult(result);
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }
}
