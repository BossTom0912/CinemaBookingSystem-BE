using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Auth;

public sealed class ResetPasswordRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$", ErrorMessage = "OTP must contain exactly 6 digits.")]
    public string Otp { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(100)]
    public string NewPassword { get; init; } = string.Empty;
}
