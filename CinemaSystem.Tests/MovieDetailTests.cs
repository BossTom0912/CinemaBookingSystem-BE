using System.Text.Json;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Movies;
using CinemaSystem.Controllers;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Movies;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Tests;

public sealed class MovieDetailTests
{
    [Fact]
    public async Task GetMovieById_ExistingPublicMovie_ReturnsMovieDetail()
    {
        // Arrange
        await using var fixture = TestFixture.Create();
        fixture.DbContext.Movies.Add(CreateMovie(
            movieId: "MOV_001",
            movieStatus: "NOW_SHOWING",
            ageRating: "T13"));
        await fixture.DbContext.SaveChangesAsync();

        // Act
        var actionResult = await fixture.Controller.GetMovieById(
            "MOV_001",
            CancellationToken.None);

        // Assert
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(200, objectResult.StatusCode);

        var response = Assert.IsType<ApiResponse<MovieDetailResponse>>(objectResult.Value);
        Assert.True(response.Success);
        Assert.Equal("Movie retrieved successfully.", response.Message);
        Assert.Null(response.ErrorCode);
        Assert.Null(response.Errors);

        var movie = Assert.IsType<MovieDetailResponse>(response.Data);
        Assert.Equal("MOV_001", movie.MovieId);
        Assert.Equal("Dune: Part Two", movie.Title);
        Assert.Equal(166, movie.DurationMinutes);
        Assert.Equal("Sci-Fi, Adventure", movie.Genre);
        Assert.Equal("English", movie.Language);
        Assert.Equal(new DateOnly(2026, 6, 15), movie.ReleaseDate);
        Assert.Equal("T13", movie.AgeRating);
        Assert.Equal("Movie description", movie.Description);
        Assert.Equal("https://example.com/poster.jpg", movie.PosterUrl);
        Assert.Equal("https://youtube.com/watch?v=test", movie.TrailerUrl);
        Assert.Equal("NOW_SHOWING", movie.MovieStatus);

        var json = JsonSerializer.Serialize(
            response,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.Contains("\"releaseDate\":\"2026-06-15\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetMovieById_UnknownMovie_ReturnsNotFound()
    {
        // Arrange
        await using var fixture = TestFixture.Create();

        // Act
        var actionResult = await fixture.Controller.GetMovieById(
            "MOV_999",
            CancellationToken.None);

        // Assert
        AssertMovieNotFound(actionResult);
    }

    [Fact]
    public async Task GetMovieById_InactiveMovie_ReturnsNotFound()
    {
        // Arrange
        await using var fixture = TestFixture.Create();
        fixture.DbContext.Movies.Add(CreateMovie(
            movieId: "MOV_002",
            movieStatus: "INACTIVE",
            ageRating: "T16"));
        await fixture.DbContext.SaveChangesAsync();

        // Act
        var actionResult = await fixture.Controller.GetMovieById(
            "MOV_002",
            CancellationToken.None);

        // Assert
        AssertMovieNotFound(actionResult);
    }

    [Fact]
    public async Task GetMovieById_ProhibitedAgeRatingMovie_ReturnsNotFound()
    {
        // Arrange
        await using var fixture = TestFixture.Create();
        fixture.DbContext.Movies.Add(CreateMovie(
            movieId: "MOV_003",
            movieStatus: "NOW_SHOWING",
            ageRating: "C"));
        await fixture.DbContext.SaveChangesAsync();

        // Act
        var actionResult = await fixture.Controller.GetMovieById(
            "MOV_003",
            CancellationToken.None);

        // Assert
        AssertMovieNotFound(actionResult);
    }

    private static void AssertMovieNotFound(IActionResult actionResult)
    {
        var objectResult = Assert.IsType<ObjectResult>(actionResult);
        Assert.Equal(404, objectResult.StatusCode);

        var response = Assert.IsType<ApiResponse<MovieDetailResponse>>(objectResult.Value);
        Assert.False(response.Success);
        Assert.Equal("Movie was not found.", response.Message);
        Assert.Equal("MOVIE_NOT_FOUND", response.ErrorCode);
        Assert.Null(response.Data);
        Assert.Null(response.Errors);
    }

    private static Movie CreateMovie(
        string movieId,
        string movieStatus,
        string ageRating)
    {
        return new Movie
        {
            MovieId = movieId,
            Title = "Dune: Part Two",
            DurationMinutes = 166,
            Genre = "Sci-Fi, Adventure",
            Language = "English",
            ReleaseDate = new DateOnly(2026, 6, 15),
            AgeRating = ageRating,
            Description = "Movie description",
            PosterUrl = "https://example.com/poster.jpg",
            TrailerUrl = "https://youtube.com/watch?v=test",
            MovieStatus = movieStatus
        };
    }

    private sealed class TestFixture : IAsyncDisposable
    {
        private TestFixture(
            CinemaDbContext dbContext,
            MoviesController controller)
        {
            DbContext = dbContext;
            Controller = controller;
        }

        public CinemaDbContext DbContext { get; }

        public MoviesController Controller { get; }

        public static TestFixture Create()
        {
            var options = new DbContextOptionsBuilder<CinemaDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var dbContext = new CinemaDbContext(options);
            var service = new MovieService(dbContext, new Moq.Mock<CinemaSystem.Application.Interfaces.IAdminRefundService>().Object);
            var controller = new MoviesController(service);

            return new TestFixture(dbContext, controller);
        }

        public ValueTask DisposeAsync()
        {
            return DbContext.DisposeAsync();
        }
    }
}
