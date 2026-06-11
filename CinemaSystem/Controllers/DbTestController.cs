using CinemaSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/db-test")]
public class DbTestController : ControllerBase
{
    private readonly ICinemaDiagnosticsService _diagnosticsService;

    public DbTestController(ICinemaDiagnosticsService diagnosticsService)
    {
        _diagnosticsService = diagnosticsService;
    }

    [HttpGet("movies-count")]
    public async Task<IActionResult> GetMoviesCount()
    {
        var count = await _diagnosticsService.GetMoviesCountAsync(HttpContext.RequestAborted);

        return Ok(new
        {
            table = "MOVIE",
            count
        });
    }
}
