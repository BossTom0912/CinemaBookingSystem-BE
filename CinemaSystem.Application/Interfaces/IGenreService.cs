using CinemaSystem.Contracts.Genres;

namespace CinemaSystem.Application.Interfaces;

public interface IGenreService
{
    Task<List<GenreResponse>> GetAllGenresAsync();
}
