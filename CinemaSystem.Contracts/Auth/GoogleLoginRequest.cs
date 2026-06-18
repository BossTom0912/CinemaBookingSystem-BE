using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Auth;

public class GoogleLoginRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}
