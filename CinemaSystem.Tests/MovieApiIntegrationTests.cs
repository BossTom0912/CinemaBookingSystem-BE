using System.Net;
using System.Text.Json;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Movies;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CinemaSystem.Tests;

public sealed class MovieApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetMovies_ReturnsSeededMovies()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedMovieAsync(factory);

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/movies?status=now_showing");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonSerializer.Deserialize<ApiResponse<PagedList<MovieResponse>>>(
            await response.Content.ReadAsStringAsync(),
            JsonOptions);
            
        Assert.True(body!.Success);
        Assert.Single(body.Data!.Items);
        Assert.Equal("Test Movie", body.Data.Items[0].MovieNameVn);
        Assert.Contains("Action", body.Data.Items[0].Genres!);
        Assert.Equal("HOT", body.Data.Items[0].Highlight);
    }

    [Fact]
    public async Task GetMovieById_ReturnsMovieDetailWithoutAuthentication()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedMovieAsync(factory);

        using var client = factory.CreateClient();
        var response = await client.GetAsync("/api/movies/MOV_01");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = JsonSerializer.Deserialize<ApiResponse<MovieDetailResponse>>(
            await response.Content.ReadAsStringAsync(),
            JsonOptions);

        Assert.True(body!.Success);
        Assert.Equal("MOV_01", body.Data!.MovieId);
        Assert.Equal("Test Movie", body.Data.Title);
        Assert.Equal("NOW_SHOWING", body.Data.MovieStatus);
    }

    private static async Task SeedMovieAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var genre = new Genre
        {
            GenreId = 1,
            Name = "Action"
        };
        var movie = new Movie
        {
            MovieId = "MOV_01",
            Title = "Test Movie",
            DurationMinutes = 120,
            MovieStatus = "NOW_SHOWING",
            Highlight = "HOT",
            ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };
        movie.MovieGenres.Add(new MovieGenre
        {
            MovieId = movie.MovieId,
            GenreId = genre.GenreId,
            Genre = genre
        });
        db.Movies.Add(movie);
        await db.SaveChangesAsync();
    }
}
