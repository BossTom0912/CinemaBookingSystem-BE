using System.ComponentModel.DataAnnotations;

namespace CinemaSystem.Contracts.Movies;

public class CreateMovieRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = null!;

    [Required]
    [Range(1, 500)]
    public int DurationMinutes { get; set; }

    public List<int>? GenreIds { get; set; }

    [MaxLength(50)]
    public string? Language { get; set; }

    public string? ReleaseDate { get; set; } // yyyy-MM-dd

    [MaxLength(10)]
    public string? AgeRating { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? TrailerUrl { get; set; }

    [MaxLength(1000)]
    public string? Highlight { get; set; }

    [MaxLength(200)]
    public string? Director { get; set; }

    [MaxLength(50)]
    public string? MovieStatus { get; set; }
}
