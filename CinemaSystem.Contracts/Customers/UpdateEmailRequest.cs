using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Customers;

public sealed class UpdateEmailRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(255)]
    public string NewEmail { get; init; } = string.Empty;
}
