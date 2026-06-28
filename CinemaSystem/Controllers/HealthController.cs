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
        // Luồng dừng tại Controller: health chỉ kiểm API process còn sống nên
        // không chuyển sang Application/Infrastructure hoặc truy cập database.
        // Muốn kiểm DB, request phải đi qua DbTestController thay vì endpoint này.
        return Ok(new
        {
            status = "OK",
            service = "CinemaSystem API",
            timestamp = DateTime.UtcNow
        });
    }
}
