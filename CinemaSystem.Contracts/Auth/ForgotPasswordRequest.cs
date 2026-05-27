using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Auth;

public sealed class ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; init; } = string.Empty;
}
