using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Infrastructure.Persistence;

using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

/// <summary>
/// Integration test HTTP cho api/rooms và api/showtimes.
/// </summary>
public sealed class RoomShowtimeApiIntegrationTests
{
  private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

  [Fact]
  public async Task RoomCrud_ManagerE2E_CreateGenerateGetUpdateDelete()
  {
    // Luồng E2E Room: Manager tạo phòng → generate ghế → GET → UPDATE → DELETE (soft).
    await using var factory = new CinemaWebApplicationFactory();
    await SeedBaseDataAsync(factory);

    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

    // Tạo phòng mới.
    var createResponse = await client.PostAsJsonAsync(
      "/api/rooms/cinemas/CIN_E2E/rooms",
      new CreateRoomRequest { RoomName = "API Room", Capacity = 8 });
    Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
    var created = await DeserializeAsync<ApiResponse<RoomResponse>>(createResponse);
    var roomId = created!.Data!.RoomId;

    // Generate ghế 2x3.
    var generateResponse = await client.PostAsJsonAsync(
      $"/api/rooms/{roomId}/generate-seats",
      new GenerateSeatsRequest { Rows = 2, Columns = 3, SeatTypeId = "SEAT_TYPE_STANDARD" });
    Assert.Equal(HttpStatusCode.OK, generateResponse.StatusCode);

    // GET danh sách phòng.
    var listResponse = await client.GetAsync("/api/rooms/rooms");
    Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
    var listBody = await DeserializeAsync<ApiResponse<List<RoomResponse>>>(listResponse);
    Assert.Contains(listBody!.Data!, r => r.RoomId == roomId && r.SeatCount == 6);

    // GET chi tiết phòng.
    var detailResponse = await client.GetAsync($"/api/rooms/rooms/{roomId}");
    Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

    // Cập nhật phòng.
    var updateResponse = await client.PutAsJsonAsync(
      $"/api/rooms/rooms/{roomId}",
      new UpdateRoomRequest { RoomName = "API Room Updated", Capacity = 10, RoomStatus = "ACTIVE" });
    Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

    // Xóa (deactivate) phòng.
    var deleteResponse = await client.DeleteAsync($"/api/rooms/rooms/{roomId}");
    Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
  }

  [Fact]
  public async Task GetRooms_CustomerRole_ReturnsForbidden()
  {
    // Luồng: Customer không có quyền xem danh sách phòng → 403.
    await using var factory = new CinemaWebApplicationFactory();
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

    var response = await client.GetAsync("/api/rooms/rooms");
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task ShowtimeCrud_ManagerE2E_CreateGetUpdateDelete()
  {
    // Luồng E2E Showtime: Manager tạo → GET list/detail → UPDATE giá → DELETE.
    await using var factory = new CinemaWebApplicationFactory();
    await SeedBaseDataWithSeatsAsync(factory);

    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

    var createResponse = await client.PostAsJsonAsync("/api/showtimes", new CreateShowtimeRequest
    {
      MovieId = "MOV_E2E",
      RoomId = "ROOM_E2E",
      StartTime = new DateTime(2026, 12, 1, 10, 0, 0, DateTimeKind.Utc),
      BasePrice = 90000
    });
    Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
    var created = await DeserializeAsync<ApiResponse<ShowtimeResponse>>(createResponse);
    var showtimeId = created!.Data!.ShowtimeId;

    var listResponse = await client.GetAsync("/api/showtimes");
    Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

    var detailResponse = await client.GetAsync($"/api/showtimes/{showtimeId}");
    Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);

    var updateResponse = await client.PutAsJsonAsync($"/api/showtimes/{showtimeId}", new UpdateShowtimeRequest
    {
      MovieId = "MOV_E2E",
      RoomId = "ROOM_E2E",
      StartTime = new DateTime(2026, 12, 1, 14, 0, 0, DateTimeKind.Utc),
      BasePrice = 95000,
      Status = "OPEN"
    });
    Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

    var deleteResponse = await client.DeleteAsync($"/api/showtimes/{showtimeId}");
    Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
  }

  [Fact]
  public async Task GetShowtimes_WithoutToken_CanAccess()
  {
    await using var factory = new CinemaWebApplicationFactory();
    await SeedBaseDataWithSeatsAsync(factory);
    using var client = factory.CreateClient();

    var response = await client.GetAsync("/api/showtimes");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task GetShowtimes_CustomerRole_CanAccess()
  {
    // Luồng: Customer được xem danh sách suất chiếu.
    await using var factory = new CinemaWebApplicationFactory();
    await SeedBaseDataWithSeatsAsync(factory);
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

    var response = await client.GetAsync("/api/showtimes");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  [Fact]
  public async Task CreateShowtime_WithoutToken_ReturnsUnauthorized()
  {
    // Luồng: tạo showtime không JWT → 401.
    await using var factory = new CinemaWebApplicationFactory();
    using var client = factory.CreateClient();

    var response = await client.PostAsJsonAsync("/api/showtimes", new CreateShowtimeRequest
    {
      MovieId = "MOV_E2E",
      RoomId = "ROOM_E2E",
      StartTime = DateTime.UtcNow.AddDays(1),
      BasePrice = 90000
    });
    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
  {
    return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
  }

  private static async Task SeedBaseDataAsync(CinemaWebApplicationFactory factory)
  {
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
    db.Cinemas.Add(new Cinema
    {
      CinemaId = "CIN_E2E",
      CinemaName = "E2E Cinema",
      Address = "1 St",
      City = "HCM",
      CinemaStatus = "ACTIVE"
    });
    db.SeatTypes.Add(new SeatType { SeatTypeId = "SEAT_TYPE_STANDARD", TypeName = "STANDARD", ExtraFee = 0 });
    await db.SaveChangesAsync();
  }

  private static async Task SeedBaseDataWithSeatsAsync(CinemaWebApplicationFactory factory)
  {
    await SeedBaseDataAsync(factory);
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
    db.Movies.Add(new Movie
    {
      MovieId = "MOV_E2E",
      Title = "E2E Movie",
      DurationMinutes = 120,
      AgeRating = "T13",
      MovieStatus = "NOW_SHOWING"
    });
    db.Rooms.Add(new Room
    {
      RoomId = "ROOM_E2E",
      CinemaId = "CIN_E2E",
      RoomName = "Room E2E",
      Capacity = 5,
      RoomStatus = "ACTIVE"
    });
    db.Seats.AddRange(Enumerable.Range(1, 5).Select(i => new Seat
    {
      SeatId = $"SEAT_{i}",
      RoomId = "ROOM_E2E",
      SeatTypeId = "SEAT_TYPE_STANDARD",
      RowLabel = "A",
      SeatNumber = i,
      SeatCode = $"A{i}",
      IsActive = true
    }));
    await db.SaveChangesAsync();
  }
}
