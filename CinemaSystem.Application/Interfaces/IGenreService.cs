using CinemaSystem.Contracts.Genres;

namespace CinemaSystem.Application.Interfaces;

/// <summary>
/// Hợp đồng use case đọc danh mục thể loại.
/// Được GenresController gọi và DI ánh xạ sang
/// CinemaSystem.Infrastructure/Services/GenreService.cs.
/// </summary>
public interface IGenreService
{
    Task<List<GenreResponse>> GetAllGenresAsync();
}
