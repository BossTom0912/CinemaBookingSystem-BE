using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Infrastructure.Persistence;

using CinemaSystem.Infrastructure.Rooms;
using CinemaSystem.Infrastructure.Showtimes;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Tests;

public sealed class RoomShowtimeServiceTests
{
    [Fact]
    public async Task CreateRoom_ThenGenerateSeats_CreatesSeatMap()
    {
        var fixture = Fixture.Create();
        await fixture.SeedCinemaAsync();

        var result = await fixture.RoomService.CreateRoomAsync(
            "CIN_TEST",
            new CreateRoomRequest
            {
                RoomName = "Room 1",
                Capacity = 12
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(0, result.Data!.SeatCount);

        fixture.DbContext.SeatTypes.Add(new SeatType
        {
            SeatTypeId = "SEAT_TYPE_STANDARD",
            TypeName = "STANDARD",
            ExtraFee = 0
        });
        await fixture.DbContext.SaveChangesAsync();

        var generated = await fixture.RoomService.GenerateSeatsAsync(
            result.Data.RoomId,
            new GenerateSeatsRequest
            {
                Rows = 3,
                Columns = 4,
                SeatTypeId = "SEAT_TYPE_STANDARD"
            },
            CancellationToken.None);

        Assert.True(generated.Success);
        Assert.Equal(200, generated.StatusCode);

        var seats = await fixture.DbContext.Seats
            .OrderBy(item => item.RowLabel)
            .ThenBy(item => item.SeatNumber)
            .ToListAsync();

        Assert.Equal(12, seats.Count);
        Assert.Equal("A1", seats[0].SeatCode);
        Assert.Equal("C4", seats[^1].SeatCode);
        Assert.Equal(1, await fixture.DbContext.SeatTypes.CountAsync());
    }

    [Fact]
    public async Task CreateShowtime_OverlappingSameRoom_ReturnsBadRequest()
    {
        var fixture = Fixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        var first = await fixture.ShowtimeService.CreateShowtimeAsync(
            new CreateShowtimeRequest
            {
                MovieId = "MOV_TEST",
                RoomId = "ROOM_TEST",
                StartTime = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
                BasePrice = 90000
            },
            CancellationToken.None);

        var overlapping = await fixture.ShowtimeService.CreateShowtimeAsync(
            new CreateShowtimeRequest
            {
                MovieId = "MOV_TEST",
                RoomId = "ROOM_TEST",
                StartTime = new DateTime(2026, 6, 1, 11, 0, 0, DateTimeKind.Utc),
                BasePrice = 90000
            },
            CancellationToken.None);

        Assert.True(first.Success);
        Assert.False(overlapping.Success);
        Assert.Equal(400, overlapping.StatusCode);
        Assert.Equal("SHOWTIME_OVERLAP", overlapping.ErrorCode);
        Assert.Single(await fixture.DbContext.Showtimes.ToListAsync());
        Assert.Equal(10, await fixture.DbContext.ShowtimeSeats.CountAsync());
    }

    [Fact]
    public async Task DeleteShowtime_RemovesShowtimeAndGeneratedSeats()
    {
        var fixture = Fixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        var created = await fixture.ShowtimeService.CreateShowtimeAsync(
            new CreateShowtimeRequest
            {
                MovieId = "MOV_TEST",
                RoomId = "ROOM_TEST",
                StartTime = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
                BasePrice = 90000
            },
            CancellationToken.None);

        Assert.True(created.Success);
        Assert.Equal(1, await fixture.DbContext.Showtimes.CountAsync());
        Assert.Equal(10, await fixture.DbContext.ShowtimeSeats.CountAsync());

        var deleted = await fixture.ShowtimeService.DeleteShowtimeAsync(
            created.Data!.ShowtimeId,
            CancellationToken.None);

        Assert.True(deleted.Success);
        Assert.Equal(0, await fixture.DbContext.Showtimes.CountAsync());
        Assert.Equal(0, await fixture.DbContext.ShowtimeSeats.CountAsync());
        Assert.Equal(10, await fixture.DbContext.Seats.CountAsync());
    }

    [Fact]
    public async Task DeleteRoom_DeactivatesRoomAndKeepsExistingData()
    {
        var fixture = Fixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        var created = await fixture.ShowtimeService.CreateShowtimeAsync(
            new CreateShowtimeRequest
            {
                MovieId = "MOV_TEST",
                RoomId = "ROOM_TEST",
                StartTime = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc),
                BasePrice = 90000
            },
            CancellationToken.None);

        Assert.True(created.Success);

        var deleted = await fixture.RoomService.DeleteRoomAsync("ROOM_TEST", CancellationToken.None);

        Assert.True(deleted.Success);
        Assert.Equal(1, await fixture.DbContext.Rooms.CountAsync());
        Assert.Equal("INACTIVE", await fixture.DbContext.Rooms
            .Where(item => item.RoomId == "ROOM_TEST")
            .Select(item => item.RoomStatus)
            .SingleAsync());
        Assert.Equal(10, await fixture.DbContext.Seats.CountAsync());
        Assert.Equal(1, await fixture.DbContext.Showtimes.CountAsync());
        Assert.Equal(10, await fixture.DbContext.ShowtimeSeats.CountAsync());
    }

    [Fact]
    public async Task GetRoomsAsync_ExcludesInactiveRooms()
    {
        // Luồng: seed phòng ACTIVE + INACTIVE → GetRooms chỉ trả ACTIVE.
        var fixture = Fixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();
        fixture.DbContext.Rooms.Add(new Room
        {
            RoomId = "ROOM_INACTIVE",
            CinemaId = "CIN_TEST",
            RoomName = "Closed Room",
            Capacity = 5,
            RoomStatus = "INACTIVE"
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.RoomService.GetRoomsAsync(null, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
        Assert.Equal("ROOM_TEST", result.Data[0].RoomId);
    }

    [Fact]
    public async Task GetRoomByIdAsync_ReturnsRoomDetail()
    {
        // Luồng: lấy chi tiết phòng ACTIVE → 200 kèm SeatCount.
        var fixture = Fixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        var result = await fixture.RoomService.GetRoomByIdAsync("ROOM_TEST", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Room Test", result.Data!.RoomName);
        Assert.Equal(10, result.Data.SeatCount);
    }

    [Fact]
    public async Task GetRoomByIdAsync_InactiveRoom_ReturnsNotFound()
    {
        // Luồng: phòng INACTIVE được coi như không tồn tại → 404 ROOM_NOT_FOUND.
        var fixture = Fixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();
        var room = await fixture.DbContext.Rooms.SingleAsync(r => r.RoomId == "ROOM_TEST");
        room.RoomStatus = "INACTIVE";
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.RoomService.GetRoomByIdAsync("ROOM_TEST", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("ROOM_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateRoomAsync_HappyPath_UpdatesRoom()
    {
        // Luồng: cập nhật tên + capacity phòng hợp lệ → 200.
        var fixture = Fixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        var result = await fixture.RoomService.UpdateRoomAsync(
            "ROOM_TEST",
            new UpdateRoomRequest
            {
                RoomName = "Room Updated",
                Capacity = 20,
                RoomStatus = "ACTIVE"
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Room Updated", result.Data!.RoomName);
        Assert.Equal(20, result.Data.Capacity);
    }

    [Fact]
    public async Task GenerateSeatsAsync_RoomAlreadyHasSeats_ReturnsConflict()
    {
        // Luồng: phòng đã có ghế → generate lại → 409 ROOM_HAS_SEATS.
        var fixture = Fixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        var result = await fixture.RoomService.GenerateSeatsAsync(
            "ROOM_TEST",
            new GenerateSeatsRequest { Rows = 2, Columns = 2, SeatTypeId = "SEAT_TYPE_STANDARD" },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("ROOM_HAS_SEATS", result.ErrorCode);
    }

    [Fact]
    public async Task CreateShowtimeAsync_HappyPath_CreatesShowtimeSeats()
    {
        // Luồng: tạo suất chiếu hợp lệ → sinh ShowtimeSeat cho mọi ghế active.
        var fixture = Fixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        var result = await fixture.ShowtimeService.CreateShowtimeAsync(
            new CreateShowtimeRequest
            {
                MovieId = "MOV_TEST",
                RoomId = "ROOM_TEST",
                StartTime = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc),
                BasePrice = 85000
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.Equal(10, result.Data!.ShowtimeSeatCount);
        Assert.Equal(10, await fixture.DbContext.ShowtimeSeats.CountAsync());
    }

    [Fact]
    public async Task GetShowtimesAsync_ReturnsAllShowtimes()
    {
        // Luồng: có 1 showtime → GetShowtimes trả danh sách.
        var fixture = Fixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();
        await fixture.ShowtimeService.CreateShowtimeAsync(
            new CreateShowtimeRequest
            {
                MovieId = "MOV_TEST",
                RoomId = "ROOM_TEST",
                StartTime = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc),
                BasePrice = 85000
            },
            CancellationToken.None);

        var result = await fixture.ShowtimeService.GetShowtimesAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task GetShowtimeByIdAsync_NotFound_Returns404()
    {
        // Luồng: showtime không tồn tại → 404 SHOWTIME_NOT_FOUND.
        var fixture = Fixture.Create();

        var result = await fixture.ShowtimeService.GetShowtimeByIdAsync("SHW_MISSING", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("SHOWTIME_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateShowtimeAsync_HappyPath_UpdatesPrice()
    {
        // Luồng: cập nhật base price showtime không có booking → 200.
        var fixture = Fixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();
        var created = await fixture.ShowtimeService.CreateShowtimeAsync(
            new CreateShowtimeRequest
            {
                MovieId = "MOV_TEST",
                RoomId = "ROOM_TEST",
                StartTime = new DateTime(2026, 6, 1, 14, 0, 0, DateTimeKind.Utc),
                BasePrice = 85000
            },
            CancellationToken.None);

        var result = await fixture.ShowtimeService.UpdateShowtimeAsync(
            created.Data!.ShowtimeId,
            new UpdateShowtimeRequest
            {
                MovieId = "MOV_TEST",
                RoomId = "ROOM_TEST",
                StartTime = new DateTime(2026, 6, 1, 16, 0, 0, DateTimeKind.Utc),
                BasePrice = 95000,
                Status = "OPEN"
            },
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(95000, result.Data!.BasePrice);
    }

    private sealed class Fixture
    {
        private Fixture(CinemaDbContext dbContext, RoomService roomService, ShowtimeService showtimeService)
        {
            DbContext = dbContext;
            RoomService = roomService;
            ShowtimeService = showtimeService;
        }

        public CinemaDbContext DbContext { get; }

        public RoomService RoomService { get; }

        public ShowtimeService ShowtimeService { get; }

        public static Fixture Create()
        {
            var options = new DbContextOptionsBuilder<CinemaDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var dbContext = new CinemaDbContext(options);
            var clock = new FakeClock(new DateTime(2026, 6, 1, 1, 0, 0, DateTimeKind.Utc));
            return new Fixture(
                dbContext,
                new RoomService(dbContext),
                new ShowtimeService(dbContext, clock));
        }

        public async Task SeedCinemaAsync()
        {
            DbContext.Cinemas.Add(new Cinema
            {
                CinemaId = "CIN_TEST",
                CinemaName = "Test Cinema",
                Address = "1 Test Street",
                City = "Ho Chi Minh",
                CinemaStatus = "ACTIVE"
            });

            await DbContext.SaveChangesAsync();
        }

        public async Task SeedCinemaMovieAndRoomWithSeatsAsync()
        {
            await SeedCinemaAsync();

            DbContext.Movies.Add(new Movie
            {
                MovieId = "MOV_TEST",
                Title = "Test Movie",
                DurationMinutes = 120,
                AgeRating = "T13",
                MovieStatus = "NOW_SHOWING"
            });

            DbContext.SeatTypes.Add(new SeatType
            {
                SeatTypeId = "SEAT_TYPE_STANDARD",
                TypeName = "STANDARD",
                ExtraFee = 0
            });

            DbContext.Rooms.Add(new Room
            {
                RoomId = "ROOM_TEST",
                CinemaId = "CIN_TEST",
                RoomName = "Room Test",
                Capacity = 10,
                RoomStatus = "ACTIVE"
            });

            DbContext.Seats.AddRange(Enumerable.Range(1, 10).Select(index => new Seat
            {
                SeatId = $"SEAT_{index}",
                RoomId = "ROOM_TEST",
                SeatTypeId = "SEAT_TYPE_STANDARD",
                SeatCode = $"A{index}",
                RowLabel = "A",
                SeatNumber = index,
                IsActive = true
            }));

            await DbContext.SaveChangesAsync();
        }
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }
}
