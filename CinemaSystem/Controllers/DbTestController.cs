using CinemaSystem.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Diagnostic endpoint used to prove that the API can query the movie table.
/// </summary>
/// <remarks>
/// The call continues through <see cref="ICinemaDiagnosticsService"/> to
/// <c>CinemaSystem.Infrastructure.Persistence.CinemaDiagnosticsService</c>,
/// which executes the MOVIE count through <c>CinemaDbContext</c>. This is not a
/// business use case endpoint.
/// </remarks>
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
