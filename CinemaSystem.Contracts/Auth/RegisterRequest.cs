using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Auth;

public sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string FullName { get; init; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneNumber { get; init; }
}
