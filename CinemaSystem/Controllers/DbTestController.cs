using CinemaSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/db-test")]
public class DbTestController : ControllerBase
{
    private readonly CinemaDbContext _context;

    public DbTestController(CinemaDbContext context)
    {
        _context = context;
    }

    [HttpGet("movies-count")]
    public async Task<IActionResult> GetMoviesCount()
    {
        var count = await _context.Movies.CountAsync();

        return Ok(new
        {
            table = "MOVIE",
            count
        });
    }
}