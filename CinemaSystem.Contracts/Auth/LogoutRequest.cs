using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Auth;

public sealed class LogoutRequest
{
    [Required]
    public string RefreshToken { get; init; } = string.Empty;
}
