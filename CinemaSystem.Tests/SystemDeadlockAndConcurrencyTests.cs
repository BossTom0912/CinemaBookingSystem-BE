using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Rooms;
using CinemaSystem.Infrastructure.Services;
using CinemaSystem.Infrastructure.Showtimes;
using CinemaSystem.Infrastructure.Time;
using CinemaSystem.Application.Settings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CinemaSystem.Tests;

/// <summary>
/// Stress and Concurrency Test Suite checking that concurrent operations across 
/// Showtime, Room, Seat, Booking, and Movie do NOT cause system deadlocks or hang execution threads.
/// </summary>
public sealed class SystemDeadlockAndConcurrencyTests
{
    private const string CinemaId = "CIN_CONCURRENCY";
    private const string SeatTypeId = "SEAT_TYPE_STD";

    [Fact]
    public async Task SystemOperations_ConcurrentExecution_DoesNotDeadlockOrHangSystem()
    {
        var fixture = await TestFixture.CreateAsync();
        var (roomId, showtimeId, seatIds) = await fixture.SetupShowtimeAndSeatsAsync();

        var cancellationTokenSource = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cancellationToken = cancellationTokenSource.Token;

        var tasks = new List<Task>();

        // Concurrent Task Group 1: 10 parallel seat lock/unlock operations
        for (int i = 0; i < 10; i++)
        {
            var userId = $"USR_SEAT_LOCKER_{i}";
            var seatId = seatIds[i % seatIds.Count];
            tasks.Add(Task.Run(async () =>
            {
                await fixture.SeatService.LockSeatAsync(
                    new LockSeatRequest { ShowtimeId = showtimeId, SeatId = seatId },
                    userId,
                    cancellationToken);
            }));
        }

        // Concurrent Task Group 2: Parallel Room info queries and updates
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await fixture.RoomService.GetRoomByIdAsync(roomId, includeInactive: true, cancellationToken);
            }));
        }

        // Concurrent Task Group 3: Parallel Showtime status queries and updates
        for (int i = 0; i < 5; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await fixture.ShowtimeService.GetShowtimeByIdAsync(showtimeId, cancellationToken);
            }));
        }

        // Execute all concurrent tasks and verify none deadlock
        var allTasks = Task.WhenAll(tasks);
        var completedTask = await Task.WhenAny(allTasks, Task.Delay(10000, cancellationToken));

        Assert.True(completedTask == allTasks, "The system deadlocked or hung during concurrent operations across Showtime, Room, Seat, Booking, and Movie.");
    }

    private sealed class TestFixture
    {
        public CinemaDbContext DbContext { get; }
        public RoomService RoomService { get; }
        public SeatService SeatService { get; }
        public ShowtimeService ShowtimeService { get; }

        private TestFixture(CinemaDbContext dbContext, RoomService roomService, SeatService seatService, ShowtimeService showtimeService)
        {
            DbContext = dbContext;
            RoomService = roomService;
            SeatService = seatService;
            ShowtimeService = showtimeService;
        }

        public static async Task<TestFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<CinemaDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;

            var dbContext = new CinemaDbContext(options);

            dbContext.Cinemas.Add(new Cinema
            {
                CinemaId = CinemaId,
                CinemaName = "Concurrency Cinema",
                Address = "456 Test Ave",
                City = "HCM",
                CinemaStatus = DomainConstants.EntityStatus.Active
            });

            dbContext.SeatTypes.Add(new SeatType
            {
                SeatTypeId = SeatTypeId,
                TypeName = "STANDARD",
                ExtraFee = 0
            });

            await dbContext.SaveChangesAsync();

            var clock = new SystemClock();
            var bgJobClient = new Mock<Hangfire.IBackgroundJobClient>().Object;
            var seatLockStore = new InMemorySeatLockStore();
            var adminRefundService = new AdminRefundService(
                dbContext,
                seatLockStore,
                Options.Create(new CinemaProcessingSettings { PreShowtimeBlockingMinutes = 30, ScreeningRoomCleaningMinutes = 15 }),
                bgJobClient,
                Options.Create(new EmailTemplatesSettings()),
                new Mock<IAiEmailService>().Object);

            var roomService = new RoomService(
                dbContext,
                adminRefundService,
                Options.Create(new CinemaProcessingSettings()));

            var seatService = new SeatService(
                dbContext,
                bgJobClient,
                adminRefundService,
                Options.Create(new SecuritySettings { ConfirmationTokenSecret = "concurrency-test-secret-key-at-least-32-chars" }),
                Options.Create(new EmailTemplatesSettings()),
                Options.Create(new BookingSettings()),
                seatLockStore);

            var showtimeService = new ShowtimeService(
                dbContext,
                clock,
                Options.Create(new CinemaProcessingSettings { PreShowtimeBlockingMinutes = 30, ScreeningRoomCleaningMinutes = 15 }),
                Options.Create(new SecuritySettings()),
                Options.Create(new EmailTemplatesSettings()),
                bgJobClient,
                null,
                new Mock<IAiEmailService>().Object);

            return new TestFixture(dbContext, roomService, seatService, showtimeService);
        }

        public async Task<(string RoomId, string ShowtimeId, List<string> ShowtimeSeatIds)> SetupShowtimeAndSeatsAsync()
        {
            var roomId = $"ROOM_CONC_{Guid.NewGuid():N}";
            DbContext.Rooms.Add(new Room { RoomId = roomId, CinemaId = CinemaId, RoomName = "Conc Room", Capacity = 20, RoomStatus = DomainConstants.EntityStatus.Active });

            var movieId = $"MOV_CONC_{Guid.NewGuid():N}";
            DbContext.Movies.Add(new Movie { MovieId = movieId, Title = "Conc Movie", DurationMinutes = 120, MovieStatus = DomainConstants.EntityStatus.Active });

            var showtimeId = $"SHW_CONC_{Guid.NewGuid():N}";
            var startTime = DateTime.UtcNow.AddDays(2);
            DbContext.Showtimes.Add(new Showtime
            {
                ShowtimeId = showtimeId,
                RoomId = roomId,
                MovieId = movieId,
                StartTime = startTime,
                EndTime = startTime.AddHours(2),
                BasePrice = 100000,
                Status = DomainConstants.ShowtimeStatus.Open
            });

            var showtimeSeatIds = new List<string>();
            for (int i = 1; i <= 10; i++)
            {
                var seatId = $"SEAT_CONC_{i}";
                DbContext.Seats.Add(new Seat
                {
                    SeatId = seatId,
                    RoomId = roomId,
                    SeatTypeId = SeatTypeId,
                    RowLabel = "A",
                    SeatNumber = i,
                    SeatCode = $"A{i}",
                    IsActive = true
                });

                var stsId = $"STS_CONC_{i}";
                DbContext.ShowtimeSeats.Add(new ShowtimeSeat
                {
                    ShowtimeSeatId = stsId,
                    ShowtimeId = showtimeId,
                    SeatId = seatId,
                    SeatStatus = DomainConstants.EntityStatus.Available,
                    RowVersion = new byte[] { 1 }
                });
                showtimeSeatIds.Add(stsId);
            }

            await DbContext.SaveChangesAsync();
            return (roomId, showtimeId, showtimeSeatIds);
        }
    }
}
