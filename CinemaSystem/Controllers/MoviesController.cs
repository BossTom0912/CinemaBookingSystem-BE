using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Movies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

/// <summary>
/// Điểm vào HTTP cho tra cứu phim công khai và CRUD phim của Manager/Admin.
/// </summary>
/// <remarks>
/// Luồng tiếp theo: <see cref="IMovieService"/> -> <c>MovieService</c> tại
/// <c>CinemaSystem.Infrastructure/Movies/MovieService.cs</c> -> MOVIE,
/// MOVIE_GENRE, GENRE, LANGUAGE và file poster qua <c>IFileStorageService</c>.
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
        typeof(ApiResponse<PagedList<MovieResponse>>),
        StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovies(
        [FromQuery] string? status,
        [FromQuery] string? genre,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        bool includeDeleted = User?.IsInRole(AuthConstants.Roles.Admin) == true || User?.IsInRole(AuthConstants.Roles.Manager) == true;
        var result = await _movieService.GetMoviesAsync(status, pageIndex, pageSize, genre, includeDeleted, cancellationToken);
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
        bool isAdmin = User?.IsInRole(AuthConstants.Roles.Admin) == true || User?.IsInRole(AuthConstants.Roles.Manager) == true;
        var result = await _movieService.GetMovieByIdAsync(movieId, isAdmin, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost("{movieId}/view")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> IncrementMovieView(
        string movieId,
        CancellationToken cancellationToken)
    {
        var result = await _movieService.IncrementMovieViewAsync(movieId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPost]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<MovieDetailResponse>), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateMovie(
        [FromForm] CreateMovieRequest request,
        IFormFile? posterFile,
        CancellationToken cancellationToken)
    {
        using var stream = posterFile?.OpenReadStream();
        var result = await _movieService.CreateMovieAsync(request, stream, posterFile?.FileName, cancellationToken);
        if (result.Success)
        {
            return StatusCode(StatusCodes.Status201Created, ApiResponse<MovieDetailResponse>.Ok(result.Data, result.Message));
        }
        return ToActionResult(result);
    }

    [HttpPut("{movieId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ApiResponse<MovieDetailResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMovie(
        string movieId,
        [FromForm] UpdateMovieRequest request,
        IFormFile? posterFile,
        CancellationToken cancellationToken)
    {
        using var stream = posterFile?.OpenReadStream();
        var result = await _movieService.UpdateMovieAsync(movieId, request, stream, posterFile?.FileName, GetUserId(), cancellationToken);
        return ToActionResult(result);
    }

    [HttpDelete("{movieId}")]
    [Authorize(Roles = AuthConstants.Roles.Admin + "," + AuthConstants.Roles.Manager)]
    [ProducesResponseType(typeof(ApiResponse<object>), StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteMovie(
        string movieId,
        CancellationToken cancellationToken)
    {
        var result = await _movieService.DeleteMovieAsync(movieId, GetUserId(), cancellationToken);
        return ToActionResult(result);
    }



    private ObjectResult ToActionResult<T>(ServiceResult<T> result)
    {
        var response = result.Success
            ? ApiResponse<T>.Ok(result.Data, result.Message)
            : ApiResponse<T>.Fail(result.Message, result.ErrorCode, result.Errors);

        return StatusCode(result.StatusCode, response);
    }

    private string GetUserId()
    {
        return User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("userId")?.Value
            ?? string.Empty;
    }
}
