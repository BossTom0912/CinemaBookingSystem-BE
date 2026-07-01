using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Reviews;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CinemaSystem.Controllers;

[ApiController]
[Route("api/reviews")]
public sealed class ReviewsController : ControllerBase
{
    private readonly IReviewService _reviewService;

    public ReviewsController(IReviewService reviewService)
    {
        _reviewService = reviewService ?? throw new ArgumentNullException(nameof(reviewService));
    }

    [HttpPost]
    [Authorize(Policy = AuthConstants.Policies.CanReviewAndFeedback)]
    [ProducesResponseType(typeof(ApiResponse<ReviewResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiResponse<ReviewResponse>), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateReview(
        [FromBody] CreateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized", "UNAUTHORIZED"));

        var result = await _reviewService.CreateReviewAsync(userIdClaim.Value, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("movies/{movieId}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ApiResponse<List<ReviewResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovieReviews(
        [FromRoute] string movieId,
        CancellationToken cancellationToken)
    {
        var result = await _reviewService.GetApprovedMovieReviewsAsync(movieId, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("me")]
    [Authorize(Policy = AuthConstants.Policies.CanReviewAndFeedback)]
    [ProducesResponseType(typeof(ApiResponse<List<ReviewResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCustomerReviews(CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized", "UNAUTHORIZED"));

        var result = await _reviewService.GetCustomerReviewsAsync(userIdClaim.Value, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("{reviewId}")]
    [Authorize(Policy = AuthConstants.Policies.CanReviewAndFeedback)]
    [ProducesResponseType(typeof(ApiResponse<ReviewResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> EditReview(
        [FromRoute] string reviewId,
        [FromBody] UpdateReviewRequest request,
        CancellationToken cancellationToken)
    {
        var userIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized", "UNAUTHORIZED"));

        var result = await _reviewService.EditReviewAsync(userIdClaim.Value, reviewId, request, cancellationToken);
        return ToActionResult(result);
    }

    [HttpGet("admin/moderation-queue")]
    [Authorize(Policy = AuthConstants.Policies.CanManageSystem)]
    [ProducesResponseType(typeof(ApiResponse<List<ReviewResponse>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetModerationQueue(CancellationToken cancellationToken)
    {
        var result = await _reviewService.GetModerationQueueAsync(cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("admin/{reviewId}/approve")]
    [Authorize(Policy = AuthConstants.Policies.CanManageSystem)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminApproveReview(
        [FromRoute] string reviewId,
        CancellationToken cancellationToken)
    {
        var adminIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (adminIdClaim == null)
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized", "UNAUTHORIZED"));

        var result = await _reviewService.AdminApproveReviewAsync(reviewId, adminIdClaim.Value, cancellationToken);
        return ToActionResult(result);
    }

    [HttpPut("admin/{reviewId}/reject")]
    [Authorize(Policy = AuthConstants.Policies.CanManageSystem)]
    [ProducesResponseType(typeof(ApiResponse<bool>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AdminRejectReview(
        [FromRoute] string reviewId,
        CancellationToken cancellationToken)
    {
        var adminIdClaim = User.FindFirst("userId") ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (adminIdClaim == null)
            return Unauthorized(ApiResponse<object>.Fail("Unauthorized", "UNAUTHORIZED"));

        var result = await _reviewService.UpdateReviewStatusAsync(reviewId, ReviewConstants.Rejected, cancellationToken);
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
