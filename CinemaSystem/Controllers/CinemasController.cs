using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Cinemas;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Mapping;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Public cinema catalogue HTTP entry point.
/// </summary>
/// <remarks>
/// Processing continues through <see cref="ICinemaService"/> to
/// <c>CinemaSystem.Infrastructure.Cinemas.CinemaService</c>, which reads CINEMA
/// with <c>CinemaDbContext</c>. This controller only maps the contract response.
/// </remarks>
[ApiController]
[Route("api/cinemas")]
public sealed class CinemasController : ControllerBase
{
    private readonly ICinemaService _cinemaService;

    public CinemasController(ICinemaService cinemaService)
    {
        _cinemaService = cinemaService ?? throw new ArgumentNullException(nameof(cinemaService));
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCinemas(CancellationToken cancellationToken)
    {
        // Bước tiếp theo: ICinemaService được DI map sang CinemaService tại
        // CinemaSystem.Infrastructure/Cinemas/CinemaService.cs để query CINEMA
        // và project sang Contracts DTO; Controller không phụ thuộc EF Core.
        var result = await _cinemaService.GetCinemasAsync(cancellationToken);

        // Contracts DTO quay lại API layer, được map sang response DTO rồi trả client.
        return ToActionResult(result.MapDataTo<IReadOnlyList<Contracts.Cinemas.CinemaResponse>, IReadOnlyList<CinemaResponse>>());
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }
}
