using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Movies;

public class MovieAutofillRequest
{
    [Required]
    [Url]
    public string Url { get; set; } = null!;
}
