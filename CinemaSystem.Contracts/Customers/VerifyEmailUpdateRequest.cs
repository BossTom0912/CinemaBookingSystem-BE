using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Customers;

public sealed class VerifyEmailUpdateRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string NewEmail { get; init; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string Otp { get; init; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string OldEmailOtp { get; init; } = string.Empty;
}
