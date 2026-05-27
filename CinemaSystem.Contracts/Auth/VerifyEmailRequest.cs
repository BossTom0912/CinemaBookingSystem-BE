using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Auth;

public sealed class VerifyEmailRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must contain exactly 6 digits.")]
    public string Otp { get; init; } = string.Empty;
}
