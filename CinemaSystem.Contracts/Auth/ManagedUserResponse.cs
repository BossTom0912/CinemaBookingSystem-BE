using System;

namespace CinemaSystem.Contracts.Auth;

public sealed class ManagedUserResponse
{
    public string UserId { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string? PhoneNumber { get; init; }
    public string RoleId { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public string? CinemaId { get; init; }
    public string? CinemaName { get; init; }
    public string Status { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public bool IsOnline { get; init; }
}
