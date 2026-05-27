namespace CinemaSystem.Contracts.Auth;

public sealed class UserProfileResponse
{
    public string UserId { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string FullName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;
}
