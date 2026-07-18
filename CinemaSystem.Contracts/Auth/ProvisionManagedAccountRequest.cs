using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Auth;

public sealed class ProvisionManagedAccountRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string FullName { get; init; } = string.Empty;

    [MaxLength(30)]
    public string? PhoneNumber { get; init; }

    [Required]
    [MaxLength(50)]
    public string RoleId { get; init; } = string.Empty;

    [MaxLength(50)]
    public string? CinemaId { get; init; }
}
