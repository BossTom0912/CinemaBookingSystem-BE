using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Genres;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Điểm vào HTTP để lấy danh mục thể loại phim dùng cho bộ lọc/chọn thể loại ở client.
/// Đi tiếp: IGenreService (Application/Interfaces) được DI ánh xạ tới
/// GenreService (Infrastructure/Services), sau đó đọc CinemaDbContext.Genres.
/// </summary>
[Route("api/[controller]")]
[ApiController]
public class GenresController : ControllerBase
{
    private readonly IGenreService _genreService;

    public GenresController(IGenreService genreService)
    {
        _genreService = genreService;
    }

    [HttpGet]
    public async Task<IActionResult> GetGenres()
    {
        // Controller chỉ đóng gói HTTP response; truy vấn và ánh xạ DTO nằm trong GenreService.
        var genres = await _genreService.GetAllGenresAsync();
        return Ok(ApiResponse<List<GenreResponse>>.Ok(genres));
    }
}
