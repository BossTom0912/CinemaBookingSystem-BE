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
        var result = await _authService.RegisterCustomerAsync(
            request.MapTo<Contracts.Auth.RegisterRequest>(),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail(VerifyEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.VerifyEmailAsync(
            request.MapTo<Contracts.Auth.VerifyEmailRequest>(),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LoginAsync(
            request.MapTo<Contracts.Auth.LoginRequest>(),
            cancellationToken);
        return ToActionResult(result.MapDataTo<Contracts.Auth.AuthResponse, AuthResponse>());
    }

    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.RefreshTokenAsync(
            request.MapTo<Contracts.Auth.RefreshTokenRequest>(),
            cancellationToken);
        return ToActionResult(result.MapDataTo<Contracts.Auth.TokenResponse, TokenResponse>());
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public async Task<IActionResult> Logout(LogoutRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.LogoutAsync(
            request.MapTo<Contracts.Auth.LogoutRequest>(),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("resend-verification-otp")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerificationOtp(
        ResendVerificationOtpRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _authService.ResendVerificationOtpAsync(
            request.MapTo<Contracts.Auth.ResendVerificationOtpRequest>(),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ForgotPasswordAsync(
            request.MapTo<Contracts.Auth.ForgotPasswordRequest>(),
            cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest request, CancellationToken cancellationToken)
    {
        var result = await _authService.ResetPasswordAsync(
            request.MapTo<Contracts.Auth.ResetPasswordRequest>(),
            cancellationToken);
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
