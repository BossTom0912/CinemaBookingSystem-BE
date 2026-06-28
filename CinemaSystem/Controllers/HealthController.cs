using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Process-level liveness endpoint.
/// </summary>
/// <remarks>
/// This action returns directly and deliberately does not call an Application
/// service or database. Use <c>DbTestController</c> when database connectivity
/// must also be checked.
/// </remarks>
[ApiController]
[Route("api/health")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "OK",
            service = "CinemaSystem API",
            timestamp = DateTime.UtcNow
        });
    }
}
