using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

public sealed class ManagerCinemaScopeApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetRooms_ManagerScope_ReturnsOnlyAssignedCinemaRooms()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedTwoCinemaDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.GetAsync("/api/rooms/rooms");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<List<RoomResponse>>>(response);
        Assert.NotNull(body?.Data);
        Assert.Contains(body.Data, room => room.RoomId == "ROOM_SCOPE_A");
        Assert.DoesNotContain(body.Data, room => room.RoomId == "ROOM_SCOPE_B");
    }

    [Fact]
    public async Task CreateRoom_ManagerOtherCinema_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedTwoCinemaDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.PostAsJsonAsync(
            "/api/rooms/cinemas/CIN_SCOPE_B/rooms",
            new CreateRoomRequest { RoomName = "Blocked Room", Capacity = 10 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("CINEMA_SCOPE_FORBIDDEN", body!.ErrorCode);
    }

    [Fact]
    public async Task CreateRoom_AdminOtherCinema_BypassesCinemaScope()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedTwoCinemaDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

        var response = await client.PostAsJsonAsync(
            "/api/rooms/cinemas/CIN_SCOPE_B/rooms",
            new CreateRoomRequest { RoomName = "Admin Room", Capacity = 10 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task CreateShowtime_ManagerOtherCinemaRoom_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedTwoCinemaDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.PostAsJsonAsync("/api/showtimes", new CreateShowtimeRequest
        {
            MovieId = "MOV_SCOPE",
            RoomId = "ROOM_SCOPE_B",
            StartTime = new DateTime(2027, 1, 1, 10, 0, 0, DateTimeKind.Utc),
            BasePrice = 90000
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("CINEMA_SCOPE_FORBIDDEN", body!.ErrorCode);
    }

    [Fact]
    public async Task UpdateShowtime_ManagerMovesToOtherCinemaRoom_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedTwoCinemaDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.PutAsJsonAsync("/api/showtimes/SHW_SCOPE_A", new UpdateShowtimeRequest
        {
            MovieId = "MOV_SCOPE",
            RoomId = "ROOM_SCOPE_B",
            StartTime = new DateTime(2027, 1, 1, 14, 0, 0, DateTimeKind.Utc),
            BasePrice = 95000,
            Status = "OPEN"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("CINEMA_SCOPE_FORBIDDEN", body!.ErrorCode);
    }

    [Fact]
    public async Task DeleteShowtime_ManagerOtherCinema_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedTwoCinemaDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.DeleteAsync("/api/showtimes/SHW_SCOPE_B");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("CINEMA_SCOPE_FORBIDDEN", body!.ErrorCode);
    }

    private static async Task SeedTwoCinemaDataAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        db.Cinemas.AddRange(
            new Cinema { CinemaId = "CIN_SCOPE_A", CinemaName = "Scope A", Address = "A", City = "HCM", CinemaStatus = "ACTIVE" },
            new Cinema { CinemaId = "CIN_SCOPE_B", CinemaName = "Scope B", Address = "B", City = "HCM", CinemaStatus = "ACTIVE" });
        db.Movies.Add(new Movie
        {
            MovieId = "MOV_SCOPE",
            Title = "Scope Movie",
            DurationMinutes = 120,
            AgeRating = "T13",
            MovieStatus = "NOW_SHOWING"
        });
        db.SeatTypes.Add(new SeatType { SeatTypeId = "SEAT_TYPE_SCOPE", TypeName = "STANDARD", ExtraFee = 0 });
        db.Rooms.AddRange(
            new Room { RoomId = "ROOM_SCOPE_A", CinemaId = "CIN_SCOPE_A", RoomName = "Room A", Capacity = 5, RoomStatus = "ACTIVE" },
            new Room { RoomId = "ROOM_SCOPE_B", CinemaId = "CIN_SCOPE_B", RoomName = "Room B", Capacity = 5, RoomStatus = "ACTIVE" });
        db.Seats.AddRange(Enumerable.Range(1, 5).SelectMany(index => new[]
        {
            new Seat
            {
                SeatId = $"SEAT_SCOPE_A_{index}",
                RoomId = "ROOM_SCOPE_A",
                SeatTypeId = "SEAT_TYPE_SCOPE",
                RowLabel = "A",
                SeatNumber = index,
                SeatCode = $"A{index}",
                IsActive = true
            },
            new Seat
            {
                SeatId = $"SEAT_SCOPE_B_{index}",
                RoomId = "ROOM_SCOPE_B",
                SeatTypeId = "SEAT_TYPE_SCOPE",
                RowLabel = "A",
                SeatNumber = index,
                SeatCode = $"A{index}",
                IsActive = true
            }
        }));
        db.Showtimes.AddRange(
            new Showtime
            {
                ShowtimeId = "SHW_SCOPE_A",
                MovieId = "MOV_SCOPE",
                RoomId = "ROOM_SCOPE_A",
                StartTime = new DateTime(2027, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2027, 1, 1, 12, 15, 0, DateTimeKind.Utc),
                BasePrice = 90000,
                Status = "OPEN",
                CreatedAt = DateTime.UtcNow
            },
            new Showtime
            {
                ShowtimeId = "SHW_SCOPE_B",
                MovieId = "MOV_SCOPE",
                RoomId = "ROOM_SCOPE_B",
                StartTime = new DateTime(2027, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2027, 1, 1, 12, 15, 0, DateTimeKind.Utc),
                BasePrice = 90000,
                Status = "OPEN",
                CreatedAt = DateTime.UtcNow
            });

        await db.SaveChangesAsync();
        await CinemaScopeTestData.SeedManagerScopeAsync(factory, "CIN_SCOPE_A");
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
    }
}
