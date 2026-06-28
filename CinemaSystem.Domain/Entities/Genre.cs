using System.Collections.Generic;

namespace CinemaSystem.Domain.Entities;

public partial class Genre
{
    public int GenreId { get; set; }
    public string Name { get; set; } = null!;
    public virtual ICollection<MovieGenre> MovieGenres { get; set; } = new List<MovieGenre>();
}
