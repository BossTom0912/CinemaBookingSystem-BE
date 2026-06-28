using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Auth;

namespace CinemaSystem.Application.Interfaces;

public interface IAuthService
{
    Task<ServiceResult<object>> RegisterCustomerAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<object>> VerifyEmailAsync(VerifyEmailRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<AuthResponse>> GoogleLoginAsync(GoogleLoginRequest request, CancellationToken cancellationToken);


    Task<ServiceResult<TokenResponse>> RefreshTokenAsync(RefreshTokenRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<object>> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<object>> ResendVerificationOtpAsync(ResendVerificationOtpRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<object>> ForgotPasswordAsync(ForgotPasswordRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<object>> ResetPasswordAsync(ResetPasswordRequest request, CancellationToken cancellationToken);
}
