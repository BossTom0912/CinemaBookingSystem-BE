using CinemaSystem.Application.Auth;

namespace CinemaSystem.Application.Interfaces;

public interface IJwtTokenService
{
    GeneratedToken GenerateAccessToken(string userId, string email, string role);

    string GenerateRefreshToken();
}
