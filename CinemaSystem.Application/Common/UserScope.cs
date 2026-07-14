namespace CinemaSystem.Application.Common;

public sealed record UserScope(
    string UserId,
    string Role,
    string? CinemaId);
