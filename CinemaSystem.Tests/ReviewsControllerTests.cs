using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Reviews;
using CinemaSystem.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CinemaSystem.Tests;

public sealed class ReviewsControllerTests
{
    [Fact]
    public async Task CreateReview_WithValidRequest_ReturnsOk()
    {
        var mockService = new Mock<IReviewService>();
        var controller = new ReviewsController(mockService.Object);

        // Setup user claims
        var user = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim("userId", "user123")
        }));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var request = new CreateReviewRequest { MovieId = "MOV1", Rating = 5, Comment = "Good" };
        var responseData = new ReviewResponse { ReviewId = "R1", Status = ReviewConstants.Pending };

        mockService.Setup(s => s.CreateReviewAsync("user123", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<ReviewResponse>.Ok(responseData));

        var result = await controller.CreateReview(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse<ReviewResponse>>(objectResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Equal("R1", apiResponse.Data!.ReviewId);
    }

    [Fact]
    public async Task CreateReview_WhenUnauthorized_ReturnsUnauthorized()
    {
        var mockService = new Mock<IReviewService>();
        var controller = new ReviewsController(mockService.Object);

        // Setup user claims without userId
        var user = new ClaimsPrincipal(new ClaimsIdentity());
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = user }
        };

        var request = new CreateReviewRequest { MovieId = "MOV1", Rating = 5 };

        var result = await controller.CreateReview(request, CancellationToken.None);

        var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(result);
        var apiResponse = Assert.IsType<ApiResponse<object>>(unauthorizedResult.Value);
        Assert.False(apiResponse.Success);
        Assert.Equal("UNAUTHORIZED", apiResponse.ErrorCode);
    }

    [Fact]
    public async Task GetMovieReviews_ReturnsOk()
    {
        var mockService = new Mock<IReviewService>();
        var controller = new ReviewsController(mockService.Object);

        var reviews = new List<ReviewResponse>
        {
            new ReviewResponse { ReviewId = "R1", MovieId = "MOV1", Rating = 5 }
        };

        mockService.Setup(s => s.GetApprovedMovieReviewsAsync("MOV1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<List<ReviewResponse>>.Ok(reviews));

        var result = await controller.GetMovieReviews("MOV1", CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);

        var apiResponse = Assert.IsType<ApiResponse<List<ReviewResponse>>>(objectResult.Value);
        Assert.True(apiResponse.Success);
        Assert.Single(apiResponse.Data!);
        Assert.Equal("R1", apiResponse.Data![0].ReviewId);
    }
}
