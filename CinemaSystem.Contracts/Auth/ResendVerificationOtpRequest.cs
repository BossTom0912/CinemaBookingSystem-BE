using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Auth;

public sealed class ResendVerificationOtpRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; init; } = string.Empty;
}
