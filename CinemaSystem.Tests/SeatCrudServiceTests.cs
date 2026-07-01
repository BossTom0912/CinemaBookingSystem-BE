using CinemaSystem.Contracts.Seats;
using CinemaSystem.Infrastructure.Persistence;

using CinemaSystem.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Tests;

/// <summary>
/// Unit test cho SeatService CRUD: CreateSeat, UpdateSeat, DeleteSeat, GetSeatsByRoom.
/// Nguồn: CinemaSystem.Infrastructure/Services/SeatService.cs
/// </summary>
public sealed class SeatCrudServiceTests
{
  private const string UserId = "USR_MANAGER_01";
  private const string RoomId = "ROOM_TEST";
  private const string SeatTypeId = "SEAT_TYPE_STANDARD";

  [Fact]
  public async Task CreateSeatAsync_HappyPath_CreatesSeat()
  {
    // Luồng: phòng + seat type hợp lệ → tạo ghế mới → 201.
    var fixture = await Fixture.CreateAsync();

    var result = await fixture.Service.CreateSeatAsync(
      new CreateSeatRequest
      {
        RoomId = RoomId,
        RowLabel = "A",
        SeatNumber = 5,
        SeatTypeId = SeatTypeId
      },
      UserId,
      CancellationToken.None);

    Assert.True(result.Success);
    Assert.Equal(201, result.StatusCode);
    Assert.True(await fixture.DbContext.Seats.AnyAsync(s => s.SeatCode == "A5"));
  }

  [Fact]
  public async Task CreateSeatAsync_DuplicateSeatCode_ReturnsConflict()
  {
    // Luồng: ghế A1 đã tồn tại → tạo trùng mã → 409 SEAT_ALREADY_EXISTS.
    var fixture = await Fixture.CreateAsync();

    var result = await fixture.Service.CreateSeatAsync(
      new CreateSeatRequest { RoomId = RoomId, RowLabel = "A", SeatNumber = 1, SeatTypeId = SeatTypeId },
      UserId,
      CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(409, result.StatusCode);
    Assert.Equal("SEAT_ALREADY_EXISTS", result.ErrorCode);
  }

  [Fact]
  public async Task CreateSeatAsync_RoomNotFound_ReturnsNotFound()
  {
    // Luồng: RoomId không tồn tại → 404 ROOM_NOT_FOUND.
    var fixture = await Fixture.CreateAsync();

    var result = await fixture.Service.CreateSeatAsync(
      new CreateSeatRequest { RoomId = "ROOM_GHOST", RowLabel = "A", SeatNumber = 9, SeatTypeId = SeatTypeId },
      UserId,
      CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(404, result.StatusCode);
    Assert.Equal("ROOM_NOT_FOUND", result.ErrorCode);
  }

  [Fact]
  public async Task UpdateSeatAsync_HappyPath_UpdatesSeat()
  {
    // Luồng: cập nhật ghế SEAT_1 từ A1 → B2.
    var fixture = await Fixture.CreateAsync();

    var result = await fixture.Service.UpdateSeatAsync(
      new UpdateSeatRequest
      {
        SeatId = "SEAT_1",
        RowLabel = "B",
        SeatNumber = 2,
        SeatTypeId = SeatTypeId
      },
      UserId,
      CancellationToken.None);

    Assert.True(result.Success);
    var seat = await fixture.DbContext.Seats.SingleAsync(s => s.SeatId == "SEAT_1");
    Assert.Equal("B2", seat.SeatCode);
  }

  [Fact]
  public async Task UpdateSeatAsync_DuplicateSeatCode_ReturnsConflict()
  {
    // Luồng: đổi SEAT_2 thành A1 (đã có SEAT_1) → 409 DUPLICATE_SEAT.
    var fixture = await Fixture.CreateAsync();

    var result = await fixture.Service.UpdateSeatAsync(
      new UpdateSeatRequest { SeatId = "SEAT_2", RowLabel = "A", SeatNumber = 1, SeatTypeId = SeatTypeId },
      UserId,
      CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(409, result.StatusCode);
    Assert.Equal("DUPLICATE_SEAT", result.ErrorCode);
  }

  [Fact]
  public async Task DeleteSeatAsync_HappyPath_DeactivatesSeat()
  {
    // Luồng: xóa ghế không gắn showtime tương lai → IsActive = false.
    var fixture = await Fixture.CreateAsync();

    var result = await fixture.Service.DeleteSeatAsync("SEAT_2", UserId, CancellationToken.None);

    Assert.True(result.Success);
    var seat = await fixture.DbContext.Seats.SingleAsync(s => s.SeatId == "SEAT_2");
    Assert.False(seat.IsActive);
  }

  [Fact]
  public async Task DeleteSeatAsync_AvailableFutureShowtimeSeat_DeactivatesSeat()
  {
    // Luồng: ghế đang trong suất chiếu OPEN tương lai → 409 SEAT_IN_ACTIVE_SHOWTIME.
    var fixture = await Fixture.CreateAsync();
    fixture.DbContext.Showtimes.Add(new Showtime
    {
      ShowtimeId = "SHW_FUTURE",
      MovieId = "MOV_TEST",
      RoomId = RoomId,
      StartTime = DateTime.UtcNow.AddDays(1),
      EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
      BasePrice = 90000,
      Status = "OPEN",
      CreatedAt = DateTime.UtcNow
    });
    fixture.DbContext.ShowtimeSeats.Add(new ShowtimeSeat
    {
      ShowtimeSeatId = "STS_1",
      ShowtimeId = "SHW_FUTURE",
      SeatId = "SEAT_1",
      SeatStatus = "AVAILABLE",
      RowVersion = new byte[8]
    });
    await fixture.DbContext.SaveChangesAsync();

    var result = await fixture.Service.DeleteSeatAsync("SEAT_1", UserId, CancellationToken.None);

    Assert.True(result.Success);
    var seat = await fixture.DbContext.Seats.SingleAsync(item => item.SeatId == "SEAT_1");
    var showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync(item => item.ShowtimeSeatId == "STS_1");
    Assert.False(seat.IsActive);
    Assert.Equal("MAINTENANCE", showtimeSeat.SeatStatus);
  }

  [Fact]
  public async Task GetSeatsByRoomAsync_ReturnsOrderedSeats()
  {
    // Luồng: lấy ghế theo phòng → sắp xếp RowLabel, SeatNumber.
    var fixture = await Fixture.CreateAsync();

    var result = await fixture.Service.GetSeatsByRoomAsync(RoomId, CancellationToken.None);

    Assert.True(result.Success);
    var seats = result.Data!.ToList();
    Assert.Equal(2, seats.Count);
    Assert.Equal("A1", seats[0].SeatCode);
    Assert.Equal("A2", seats[1].SeatCode);
  }

  [Fact]
  public async Task GetSeatsByRoomAsync_EmptyRoomId_ReturnsBadRequest()
  {
    // Luồng: roomId rỗng → 400 INVALID_ROOM_ID.
    var fixture = await Fixture.CreateAsync();

    var result = await fixture.Service.GetSeatsByRoomAsync("  ", CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(400, result.StatusCode);
    Assert.Equal("INVALID_ROOM_ID", result.ErrorCode);
  }

  private sealed class Fixture
  {
    public CinemaDbContext DbContext { get; }
    public SeatService Service { get; }

    private Fixture(CinemaDbContext dbContext, SeatService service)
    {
      DbContext = dbContext;
      Service = service;
    }

    public static async Task<Fixture> CreateAsync()
    {
      var options = new DbContextOptionsBuilder<CinemaDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options;
      var dbContext = new CinemaDbContext(options);

      dbContext.Cinemas.Add(new Cinema
      {
        CinemaId = "CIN_TEST",
        CinemaName = "Test",
        Address = "1 St",
        City = "HCM",
        CinemaStatus = "ACTIVE"
      });
      dbContext.Rooms.Add(new Room
      {
        RoomId = RoomId,
        CinemaId = "CIN_TEST",
        RoomName = "Room 1",
        Capacity = 10,
        RoomStatus = "ACTIVE"
      });
      dbContext.SeatTypes.Add(new SeatType
      {
        SeatTypeId = SeatTypeId,
        TypeName = "STANDARD",
        ExtraFee = 0
      });
      dbContext.Seats.AddRange(
        new Seat
        {
          SeatId = "SEAT_1",
          RoomId = RoomId,
          SeatTypeId = SeatTypeId,
          RowLabel = "A",
          SeatNumber = 1,
          SeatCode = "A1",
          IsActive = true
        },
        new Seat
        {
          SeatId = "SEAT_2",
          RoomId = RoomId,
          SeatTypeId = SeatTypeId,
          RowLabel = "A",
          SeatNumber = 2,
          SeatCode = "A2",
          IsActive = true
        });
      await dbContext.SaveChangesAsync();

      return new Fixture(
        dbContext,
        new SeatService(
          dbContext,
          new Moq.Mock<Hangfire.IBackgroundJobClient>().Object,
          new Moq.Mock<CinemaSystem.Application.Interfaces.IAdminRefundService>().Object,
          Microsoft.Extensions.Options.Options.Create(new CinemaSystem.Application.Settings.SecuritySettings()),
          Microsoft.Extensions.Options.Options.Create(new CinemaSystem.Application.Settings.EmailTemplatesSettings()),
          Microsoft.Extensions.Options.Options.Create(
            new CinemaSystem.Infrastructure.Configuration.BookingSettings())));
    }
  }
}
