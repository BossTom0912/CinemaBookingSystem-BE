using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Movies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Public movie catalogue HTTP entry point.
/// </summary>
/// <remarks>
/// Requests continue through <see cref="IMovieService"/> to
/// <c>CinemaSystem.Infrastructure.Movies.MovieService</c>. That service queries
/// MOVIE through <c>CinemaDbContext</c> and applies public visibility rules
/// before this controller wraps the result in <c>ApiResponse</c>.
/// </remarks>
[ApiController]
[Route("api/movies")]
public sealed class MoviesController : ControllerBase
{
    private readonly IMovieService _movieService;

    public MoviesController(IMovieService movieService)
    {
        _movieService = movieService ?? throw new ArgumentNullException(nameof(movieService));
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(
        typeof(ApiResponse<IReadOnlyList<MovieResponse>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovies(
        [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        // Bước tiếp theo: IMovieService được DI map sang MovieService tại
        // CinemaSystem.Infrastructure/Movies/MovieService.cs. Service query MOVIE,
        // lọc phim không public và project entity sang DTO.
        var result = await _movieService.GetMoviesAsync(status, cancellationToken);

        // Query hoàn tất thì ServiceResult quay lại đây để tạo HTTP response.
        return ToActionResult(result);
    }

    [HttpGet("{movieId}")]
    [AllowAnonymous]
    [ProducesResponseType(
        typeof(ApiResponse<MovieDetailResponse>),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ApiResponse<MovieDetailResponse>),
        StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMovieById(
        string movieId,
        CancellationToken cancellationToken)
    {
        // Bước tiếp theo: MovieService (Infrastructure/Movies) kiểm movieId cùng
        // rule public visibility trực tiếp trên CinemaDbContext.
        var result = await _movieService.GetMovieByIdAsync(movieId, cancellationToken);

        // DTO hoặc lỗi MOVIE_NOT_FOUND quay lại Controller để chuẩn hóa response.
        return ToActionResult(result);
    }

    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }
}
