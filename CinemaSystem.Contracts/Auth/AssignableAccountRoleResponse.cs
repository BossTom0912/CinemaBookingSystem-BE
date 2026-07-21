namespace CinemaSystem.Contracts.Auth;

public sealed class AssignableAccountRoleResponse
{
    public string RoleId { get; init; } = string.Empty;

    public string RoleName { get; init; } = string.Empty;

    public string? Description { get; init; }

    public string ProfileKind { get; init; } = string.Empty;

    public bool RequiresCinema { get; init; }
}
