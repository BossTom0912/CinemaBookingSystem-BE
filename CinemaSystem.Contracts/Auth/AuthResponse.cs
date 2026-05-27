namespace CinemaSystem.Contracts.Auth;

public sealed class AuthResponse
{
    public string AccessToken { get; init; } = string.Empty;

    public string RefreshToken { get; init; } = string.Empty;

    public DateTime ExpiresAt { get; init; }

    public string UserId { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;
}
