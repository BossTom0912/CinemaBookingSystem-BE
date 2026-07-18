namespace CinemaSystem.Contracts.Auth;

public sealed class ProvisionedAccountResponse
{
    public string UserId { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string RoleId { get; init; } = string.Empty;

    public string RoleName { get; init; } = string.Empty;

    public string? CinemaId { get; init; }

    public DateTime InvitationExpiresAt { get; init; }
}
