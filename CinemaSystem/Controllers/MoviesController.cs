using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Movies;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/movies")]
public sealed class MoviesController : ControllerBase
{
    private readonly IMovieService _movieService;

    public MoviesController(IMovieService movieService)
    {
        _movieService = movieService;
    }

    [HttpGet]
    public async Task<IActionResult> GetMovies([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var result = await _movieService.GetMoviesAsync(status, cancellationToken);
        
        var response = ApiResponse<IReadOnlyList<MovieResponse>>.Ok(result.Data, result.Message);
        
        return StatusCode(result.StatusCode, response);
    }
}
