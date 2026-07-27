using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Auth;

public sealed class UpdateUserRoleCinemaRequest
{
    [Required]
    [MaxLength(50)]
    public string RoleId { get; init; } = string.Empty;

    [MaxLength(50)]
    public string? CinemaId { get; init; }
}
