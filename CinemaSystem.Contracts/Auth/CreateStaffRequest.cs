using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Auth;

public sealed class CreateStaffRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; init; } = string.Empty;

    [MaxLength(255)]
    public string? FullName { get; init; }
}
