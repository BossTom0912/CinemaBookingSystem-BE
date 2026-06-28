namespace CinemaSystem.Domain.Entities;

public partial class MovieGenre
{
    public string MovieId { get; set; } = null!;
    public int GenreId { get; set; }

    public virtual Movie Movie { get; set; } = null!;
    public virtual Genre Genre { get; set; } = null!;
}
