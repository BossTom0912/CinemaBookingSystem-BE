using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Genres;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

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
        var genres = await _genreService.GetAllGenresAsync();
        return Ok(ApiResponse<List<GenreResponse>>.Ok(genres));
    }
}
