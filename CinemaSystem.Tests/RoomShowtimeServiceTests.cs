using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Persistence.Models;
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
        Assert.Equal(200, result.StatusCode);
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
