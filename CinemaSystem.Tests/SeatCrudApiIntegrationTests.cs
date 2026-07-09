using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Infrastructure.Persistence;

using CinemaSystem.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

/// <summary>
/// Integration test HTTP cho Seat CRUD: POST/PUT/DELETE /api/seats, GET /api/seats/room/{roomId}.
/// </summary>
public sealed class SeatCrudApiIntegrationTests
{
  private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

  [Fact]
  public async Task SeatCrud_ManagerE2E_CreateUpdateDeleteAndGetByRoom()
  {
    // Luồng E2E: Manager tạo ghế → GET by room → update → delete (soft).
    await using var factory = new CinemaWebApplicationFactory();
    await SeedRoomAsync(factory);

    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

    // Tạo ghế B1.
    var createResponse = await client.PostAsJsonAsync("/api/seats", new CreateSeatRequest
    {
      RoomId = "ROOM_E2E",
      RowLabel = "B",
      SeatNumber = 1,
      SeatTypeId = "SEAT_TYPE_STANDARD"
    });
    Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

    // GET ghế theo phòng (Staff/Manager được phép).
    var listResponse = await client.GetAsync("/api/seats/room/ROOM_E2E");
    Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
    var listBody = await DeserializeAsync<ApiResponse<List<SeatResponse>>>(listResponse);
    var createdSeat = Assert.Single(listBody!.Data!, s => s.SeatCode == "B1");

    // Cập nhật ghế B1 → C1.
    var updateResponse = await client.PutAsJsonAsync(
      $"/api/seats/{createdSeat.SeatId}",
      new UpdateSeatRequest
      {
        SeatId = createdSeat.SeatId,
        RowLabel = "C",
        SeatNumber = 1,
        SeatTypeId = "SEAT_TYPE_STANDARD",
        SeatStatus = "ACTIVE"
      });
    Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

    // Xóa ghế.
    var deleteResponse = await client.DeleteAsync($"/api/seats/{createdSeat.SeatId}");
    Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
  }

  [Fact]
  public async Task CreateSeat_CustomerRole_ReturnsForbidden()
  {
    // Luồng: Customer không có quyền CRUD ghế → 403.
    await using var factory = new CinemaWebApplicationFactory();
    await SeedRoomAsync(factory);
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

    var response = await client.PostAsJsonAsync("/api/seats", new CreateSeatRequest
    {
      RoomId = "ROOM_E2E",
      RowLabel = "A",
      SeatNumber = 9,
      SeatTypeId = "SEAT_TYPE_STANDARD"
    });
    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task GetSeatsByRoom_StaffRole_CanAccess()
  {
    // Luồng: Staff được xem layout ghế theo phòng.
    await using var factory = new CinemaWebApplicationFactory();
    await SeedRoomAsync(factory);
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", TestAuthTokens.Staff());

    var response = await client.GetAsync("/api/seats/room/ROOM_E2E");
    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
  }

  private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
  {
    return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
  }

  private static async Task SeedRoomAsync(CinemaWebApplicationFactory factory)
  {
    await using var scope = factory.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
    db.Cinemas.Add(new Cinema
    {
      CinemaId = "CIN_E2E",
      CinemaName = "Cinema",
      Address = "1",
      City = "HCM",
      CinemaStatus = "ACTIVE"
    });
    db.Rooms.Add(new Room
    {
      RoomId = "ROOM_E2E",
      CinemaId = "CIN_E2E",
      RoomName = "Room",
      Capacity = 10,
      RoomStatus = "ACTIVE"
    });
    db.SeatTypes.Add(new SeatType { SeatTypeId = "SEAT_TYPE_STANDARD", TypeName = "STANDARD", ExtraFee = 0 });
    await db.SaveChangesAsync();
    await CinemaScopeTestData.SeedManagerScopeAsync(factory, "CIN_E2E");
    await CinemaScopeTestData.SeedStaffScopeAsync(factory, "CIN_E2E");
  }
}
