using System.ComponentModel.DataAnnotations;
using System;

namespace CinemaSystem.Contracts.Movies;

public class UpdateMovieRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; set; } = null!;

    [Required]
    [Range(1, 500)]
    public int DurationMinutes { get; set; }

    [MaxLength(100)]
    public string? Genre { get; set; }

    [MaxLength(50)]
    public string? Language { get; set; }

    public DateOnly? ReleaseDate { get; set; }

    [MaxLength(10)]
    public string? AgeRating { get; set; }

    [MaxLength(2000)]
    public string? Description { get; set; }

    [MaxLength(2000)]
    public string? PosterUrl { get; set; }

    [MaxLength(2000)]
    public string? TrailerUrl { get; set; }

    [MaxLength(1000)]
    public string? Highlight { get; set; }

    [Required]
    [MaxLength(50)]
    public string MovieStatus { get; set; } = null!; // ACTIVE, INACTIVE, DELETED
}
