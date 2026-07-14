using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

public sealed class RoomSeatUpdateApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task UpdateRoomSeat_AdminAndManagerScope_ReturnExpectedResponses()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedRoomSeatScopeDataAsync(factory);

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

        var adminRoomResponse = await adminClient.PutAsJsonAsync(
            "/api/rooms/rooms/ROOM_RS_B",
            new UpdateRoomRequest
            {
                RoomName = "Room B Admin Updated",
                Capacity = 12,
                RoomStatus = "ACTIVE"
            });

        Assert.Equal(HttpStatusCode.OK, adminRoomResponse.StatusCode);
        var adminRoomBody = await DeserializeAsync<ApiResponse<RoomResponse>>(adminRoomResponse);
        Assert.True(adminRoomBody!.Success);
        Assert.Equal("Room B Admin Updated", adminRoomBody.Data!.RoomName);

        var adminSeatResponse = await adminClient.PutAsJsonAsync(
            "/api/seats/SEAT_RS_B_1",
            new UpdateSeatRequest
            {
                SeatId = "SEAT_RS_B_1",
                RowLabel = "B",
                SeatNumber = 1,
                SeatTypeId = "SEAT_TYPE_RS",
                SeatStatus = "ACTIVE"
            });

        Assert.Equal(HttpStatusCode.OK, adminSeatResponse.StatusCode);
        var adminSeatBody = await DeserializeAsync<ApiResponse<bool>>(adminSeatResponse);
        Assert.True(adminSeatBody!.Success);
        Assert.True(adminSeatBody.Data);

        using var managerClient = factory.CreateClient();
        managerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var managerOwnRoomResponse = await managerClient.PutAsJsonAsync(
            "/api/rooms/rooms/ROOM_RS_A",
            new UpdateRoomRequest
            {
                RoomName = "Room A Manager Updated",
                Capacity = 12,
                RoomStatus = "ACTIVE"
            });

        Assert.Equal(HttpStatusCode.OK, managerOwnRoomResponse.StatusCode);

        var managerOwnSeatResponse = await managerClient.PutAsJsonAsync(
            "/api/seats/SEAT_RS_A_1",
            new UpdateSeatRequest
            {
                SeatId = "SEAT_RS_A_1",
                RowLabel = "C",
                SeatNumber = 1,
                SeatTypeId = "SEAT_TYPE_RS",
                SeatStatus = "ACTIVE"
            });

        Assert.Equal(HttpStatusCode.OK, managerOwnSeatResponse.StatusCode);

        var managerOtherRoomResponse = await managerClient.PutAsJsonAsync(
            "/api/rooms/rooms/ROOM_RS_B",
            new UpdateRoomRequest
            {
                RoomName = "Room B Manager Blocked",
                Capacity = 12,
                RoomStatus = "ACTIVE"
            });

        Assert.Equal(HttpStatusCode.Forbidden, managerOtherRoomResponse.StatusCode);
        var managerOtherRoomBody = await DeserializeAsync<ApiResponse<object>>(managerOtherRoomResponse);
        Assert.Equal("CINEMA_SCOPE_FORBIDDEN", managerOtherRoomBody!.ErrorCode);

        var managerOtherSeatResponse = await managerClient.PutAsJsonAsync(
            "/api/seats/SEAT_RS_B_2",
            new UpdateSeatRequest
            {
                SeatId = "SEAT_RS_B_2",
                RowLabel = "D",
                SeatNumber = 2,
                SeatTypeId = "SEAT_TYPE_RS",
                SeatStatus = "ACTIVE"
            });

        Assert.Equal(HttpStatusCode.Forbidden, managerOtherSeatResponse.StatusCode);
        var managerOtherSeatBody = await DeserializeAsync<ApiResponse<object>>(managerOtherSeatResponse);
        Assert.Equal("CINEMA_SCOPE_FORBIDDEN", managerOtherSeatBody!.ErrorCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var roomA = await db.Rooms.SingleAsync(item => item.RoomId == "ROOM_RS_A");
        var roomB = await db.Rooms.SingleAsync(item => item.RoomId == "ROOM_RS_B");
        var seatA = await db.Seats.SingleAsync(item => item.SeatId == "SEAT_RS_A_1");
        var seatB1 = await db.Seats.SingleAsync(item => item.SeatId == "SEAT_RS_B_1");
        var seatB2 = await db.Seats.SingleAsync(item => item.SeatId == "SEAT_RS_B_2");

        Assert.Equal("Room A Manager Updated", roomA.RoomName);
        Assert.Equal("Room B Admin Updated", roomB.RoomName);
        Assert.Equal("C1", seatA.SeatCode);
        Assert.Equal("B1", seatB1.SeatCode);
        Assert.Equal("A2", seatB2.SeatCode);
    }

    private static async Task SeedRoomSeatScopeDataAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        db.Roles.AddRange(
            new Role
            {
                RoleId = AuthConstants.RoleIds.Admin,
                RoleName = AuthConstants.Roles.Admin,
                Description = "Admin test role"
            },
            new Role
            {
                RoleId = AuthConstants.RoleIds.Manager,
                RoleName = AuthConstants.Roles.Manager,
                Description = "Manager test role"
            });
        db.Users.Add(new User
        {
            UserId = "USR_TEST_ADMIN",
            RoleId = AuthConstants.RoleIds.Admin,
            Email = "admin@test.com",
            PasswordHash = "TEST_HASH",
            FullName = "Test Admin",
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = DateTime.UtcNow
        });
        db.Cinemas.AddRange(
            new Cinema { CinemaId = "CIN_RS_A", CinemaName = "RoomSeat A", Address = "A", City = "HCM", CinemaStatus = "ACTIVE" },
            new Cinema { CinemaId = "CIN_RS_B", CinemaName = "RoomSeat B", Address = "B", City = "HCM", CinemaStatus = "ACTIVE" });
        db.SeatTypes.Add(new SeatType { SeatTypeId = "SEAT_TYPE_RS", TypeName = "STANDARD", ExtraFee = 0 });
        db.Rooms.AddRange(
            new Room { RoomId = "ROOM_RS_A", CinemaId = "CIN_RS_A", RoomName = "Room A", Capacity = 10, RoomStatus = "ACTIVE" },
            new Room { RoomId = "ROOM_RS_B", CinemaId = "CIN_RS_B", RoomName = "Room B", Capacity = 10, RoomStatus = "ACTIVE" });
        db.Seats.AddRange(
            new Seat { SeatId = "SEAT_RS_A_1", RoomId = "ROOM_RS_A", SeatTypeId = "SEAT_TYPE_RS", RowLabel = "A", SeatNumber = 1, SeatCode = "A1", IsActive = true },
            new Seat { SeatId = "SEAT_RS_A_2", RoomId = "ROOM_RS_A", SeatTypeId = "SEAT_TYPE_RS", RowLabel = "A", SeatNumber = 2, SeatCode = "A2", IsActive = true },
            new Seat { SeatId = "SEAT_RS_B_1", RoomId = "ROOM_RS_B", SeatTypeId = "SEAT_TYPE_RS", RowLabel = "A", SeatNumber = 1, SeatCode = "A1", IsActive = true },
            new Seat { SeatId = "SEAT_RS_B_2", RoomId = "ROOM_RS_B", SeatTypeId = "SEAT_TYPE_RS", RowLabel = "A", SeatNumber = 2, SeatCode = "A2", IsActive = true });

        await db.SaveChangesAsync();
        await CinemaScopeTestData.SeedManagerScopeAsync(factory, "CIN_RS_A");
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
    }
}
