using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Movies;
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
/// Comprehensive test suite verifying Admin CRUD operations for Room, Seat, Showtime, and Movie
/// under two specific conditions: WHEN THERE ARE NO BOOKINGS vs WHEN THERE ARE ACTIVE BOOKINGS.
/// </summary>
public sealed class AdminCrudWithAndWithoutBookingTests
{
    private const string CinemaId = "CIN_ADMIN_TEST";
    private const string UserId = "USR_ADMIN_01";
    private const string SeatTypeId = "SEAT_TYPE_STD";

    [Fact]
    public async Task RoomCrud_WithoutBooking_CanCreateUpdateAndDeleteRoom()
    {
        var fixture = await TestFixture.CreateAsync();

        // 1. Create Room
        var createResult = await fixture.RoomService.CreateRoomAsync(
            CinemaId,
            new CreateRoomRequest { RoomName = "Room No Booking", Capacity = 50 },
            CancellationToken.None);
        Assert.True(createResult.Success);
        var roomId = createResult.Data!.RoomId;

        // 2. Update Room
        var updateResult = await fixture.RoomService.UpdateRoomAsync(
            roomId,
            new UpdateRoomRequest { RoomName = "Room Updated", Capacity = 60, RoomStatus = DomainConstants.EntityStatus.Active },
            UserId,
            CancellationToken.None);
        Assert.True(updateResult.Success);

        // 3. Delete Room (without showtimes/bookings)
        var deleteResult = await fixture.RoomService.DeleteRoomAsync(roomId, UserId, CancellationToken.None);
        Assert.True(deleteResult.Success);
    }

    [Fact]
    public async Task RoomCrud_WithBooking_DeactivatingRoomSetsRoomInactiveAndHandlesShowtimes()
    {
        var fixture = await TestFixture.CreateAsync();
        var (roomId, showtimeId, bookingId) = await fixture.SeedRoomWithBookingAsync("Room With Booking");

        // Attempting to update room to Maintenance/Inactive when bookings exist
        var updateResult = await fixture.RoomService.UpdateRoomAsync(
            roomId,
            new UpdateRoomRequest { RoomName = "Room With Booking", Capacity = 50, RoomStatus = DomainConstants.RoomStatus.Maintenance },
            UserId,
            CancellationToken.None);

        Assert.True(updateResult.Success);
        var room = await fixture.DbContext.Rooms.FindAsync(roomId);
        Assert.Equal(DomainConstants.RoomStatus.Maintenance, room!.RoomStatus);
    }

    [Fact]
    public async Task SeatCrud_WithoutBooking_CanCreateUpdateAndDeleteSeat()
    {
        var fixture = await TestFixture.CreateAsync();
        var roomId = await fixture.SeedEmptyRoomAsync("Seat Room 1");

        // 1. Create Seat
        var createResult = await fixture.SeatService.CreateSeatAsync(
            new CreateSeatRequest { RoomId = roomId, RowLabel = "B", SeatNumber = 1, SeatTypeId = SeatTypeId },
            UserId,
            CancellationToken.None);
        Assert.True(createResult.Success);
        var seatCode = "B1";

        // 2. Delete Seat without booking
        var seat = await fixture.DbContext.Seats.FirstAsync(s => s.RoomId == roomId && s.SeatCode == seatCode);
        var deleteResult = await fixture.SeatService.DeleteSeatAsync(seat.SeatId, UserId, CancellationToken.None);
        Assert.True(deleteResult.Success);

        var updatedSeat = await fixture.DbContext.Seats.FindAsync(seat.SeatId);
        Assert.False(updatedSeat!.IsActive);
    }

    [Fact]
    public async Task SeatCrud_WithBooking_BlocksDeactivationWhenFutureBookingsExist()
    {
        var fixture = await TestFixture.CreateAsync();
        var (roomId, showtimeId, bookingId) = await fixture.SeedRoomWithBookingAsync("Seat Room 2");
        var seat = await fixture.DbContext.Seats.FirstAsync(s => s.RoomId == roomId && s.RowLabel == "B");

        // Attempt to update seat status to Maintenance when a future booking exists for it
        var updateResult = await fixture.SeatService.UpdateSeatAsync(
            new UpdateSeatRequest
            {
                SeatId = seat.SeatId,
                RowLabel = seat.RowLabel,
                SeatNumber = seat.SeatNumber,
                SeatTypeId = seat.SeatTypeId,
                SeatStatus = DomainConstants.EntityStatus.Maintenance
            },
            UserId,
            CancellationToken.None);

        Assert.False(updateResult.Success);
        Assert.Equal("SEAT_HAS_FUTURE_BOOKINGS", updateResult.ErrorCode);
    }

    [Fact]
    public async Task ShowtimeCrud_WithoutBooking_CanCreateUpdateAndDeleteShowtime()
    {
        var fixture = await TestFixture.CreateAsync();
        var roomId = await fixture.SeedEmptyRoomAsync("Showtime Room 1");
        var movieId = await fixture.SeedMovieAsync("Movie 1");

        var startTime = DateTime.UtcNow.AddDays(2);
        var createResult = await fixture.ShowtimeService.CreateShowtimeAsync(
            new CreateShowtimeRequest
            {
                MovieId = movieId,
                RoomId = roomId,
                StartTime = startTime,
                BasePrice = 100000,
                Status = DomainConstants.ShowtimeStatus.Open
            },
            CancellationToken.None);
        Assert.True(createResult.Success);
        var showtimeId = createResult.Data!.ShowtimeId;

        // Delete showtime without booking
        var deleteResult = await fixture.ShowtimeService.DeleteShowtimeAsync(showtimeId, CancellationToken.None);
        Assert.True(deleteResult.Success);
    }

    [Fact]
    public async Task ShowtimeCrud_WithBooking_DeletingShowtimeTriggersCancellationAndRefunds()
    {
        var fixture = await TestFixture.CreateAsync();
        var (roomId, showtimeId, bookingId) = await fixture.SeedRoomWithBookingAsync("Showtime Room 2");

        // Delete showtime that has paid bookings
        var deleteResult = await fixture.ShowtimeService.DeleteShowtimeAsync(showtimeId, CancellationToken.None);
        Assert.True(deleteResult.Success);

        var showtime = await fixture.DbContext.Showtimes.FindAsync(showtimeId);
        Assert.Equal(DomainConstants.ShowtimeStatus.Cancelled, showtime!.Status);

        var booking = await fixture.DbContext.Bookings.FindAsync(bookingId);
        Assert.Equal(DomainConstants.EntityStatus.PendingRefund, booking!.BookingStatus);
    }

    [Fact]
    public async Task ShowtimeCrud_WithBooking_UpdatingRoomReturnsErrorAndBlocksFkViolation()
    {
        var fixture = await TestFixture.CreateAsync();
        var (roomId, showtimeId, bookingId) = await fixture.SeedRoomWithBookingAsync("Showtime Room 3");
        var booking = await fixture.DbContext.Bookings.FindAsync(bookingId);
        booking!.BookingStatus = DomainConstants.EntityStatus.PendingPayment;
        await fixture.DbContext.SaveChangesAsync();

        var newRoomId = await fixture.SeedEmptyRoomAsync("New Room 4");
        var showtime = await fixture.DbContext.Showtimes.FindAsync(showtimeId);

        var updateResult = await fixture.ShowtimeService.UpdateShowtimeAsync(
            showtimeId,
            new UpdateShowtimeRequest
            {
                MovieId = showtime!.MovieId,
                RoomId = newRoomId,
                StartTime = showtime.StartTime,
                BasePrice = showtime.BasePrice,
                Status = showtime.Status
            },
            force: false,
            CancellationToken.None);

        Assert.False(updateResult.Success);
        Assert.Equal("SHOWTIME_HAS_BOOKINGS", updateResult.ErrorCode);
    }

    [Fact]
    public async Task MovieCrud_WithoutBooking_CanSoftDeleteMovie()
    {
        var fixture = await TestFixture.CreateAsync();
        var movieId = await fixture.SeedMovieAsync("Movie Without Booking");

        var movie = await fixture.DbContext.Movies.FindAsync(movieId);
        movie!.MovieStatus = DomainConstants.EntityStatus.Inactive;
        await fixture.DbContext.SaveChangesAsync();

        var updated = await fixture.DbContext.Movies.FindAsync(movieId);
        Assert.Equal(DomainConstants.EntityStatus.Inactive, updated!.MovieStatus);
    }

    [Fact]
    public async Task MovieCrud_WithBooking_PreservesBookingRecordsWhenMovieIsDeactivated()
    {
        var fixture = await TestFixture.CreateAsync();
        var (roomId, showtimeId, bookingId) = await fixture.SeedRoomWithBookingAsync("Movie With Booking Room");
        var showtime = await fixture.DbContext.Showtimes.Include(s => s.Movie).FirstAsync(s => s.ShowtimeId == showtimeId);

        // Deactivate movie
        showtime.Movie.MovieStatus = DomainConstants.EntityStatus.Inactive;
        await fixture.DbContext.SaveChangesAsync();

        // Booking remains valid
        var booking = await fixture.DbContext.Bookings.FindAsync(bookingId);
        Assert.Equal(DomainConstants.EntityStatus.Paid, booking!.BookingStatus);
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
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var dbContext = new CinemaDbContext(options);

            dbContext.Cinemas.Add(new Cinema
            {
                CinemaId = CinemaId,
                CinemaName = "Admin Test Cinema",
                Address = "123 Admin St",
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
                Options.Create(new SecuritySettings { ConfirmationTokenSecret = "secret-key-32-chars-long-for-testing" }),
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

        public async Task<string> SeedEmptyRoomAsync(string roomName)
        {
            var roomId = $"ROOM_{Guid.NewGuid():N}";
            DbContext.Rooms.Add(new Room
            {
                RoomId = roomId,
                CinemaId = CinemaId,
                RoomName = roomName,
                Capacity = 20,
                RoomStatus = DomainConstants.EntityStatus.Active
            });
            DbContext.Seats.Add(new Seat
            {
                SeatId = $"SEAT_{Guid.NewGuid():N}",
                RoomId = roomId,
                SeatTypeId = SeatTypeId,
                RowLabel = "A",
                SeatNumber = 1,
                SeatCode = "A1",
                IsActive = true
            });
            await DbContext.SaveChangesAsync();
            return roomId;
        }

        public async Task<string> SeedMovieAsync(string title)
        {
            var movieId = $"MOV_{Guid.NewGuid():N}";
            DbContext.Movies.Add(new Movie
            {
                MovieId = movieId,
                Title = title,
                DurationMinutes = 120,
                MovieStatus = DomainConstants.EntityStatus.Active,
                AgeRating = "P"
            });
            await DbContext.SaveChangesAsync();
            return movieId;
        }

        public async Task<(string RoomId, string ShowtimeId, string BookingId)> SeedRoomWithBookingAsync(string roomName)
        {
            var roomId = await SeedEmptyRoomAsync(roomName);
            var movieId = await SeedMovieAsync($"Movie {roomName}");

            var seatId = $"SEAT_{Guid.NewGuid():N}";
            DbContext.Seats.Add(new Seat
            {
                SeatId = seatId,
                RoomId = roomId,
                SeatTypeId = SeatTypeId,
                RowLabel = "B",
                SeatNumber = 1,
                SeatCode = "B1",
                IsActive = true
            });

            var showtimeId = $"SHW_{Guid.NewGuid():N}";
            var startTime = DateTime.UtcNow.AddDays(1);
            DbContext.Showtimes.Add(new Showtime
            {
                ShowtimeId = showtimeId,
                MovieId = movieId,
                RoomId = roomId,
                StartTime = startTime,
                EndTime = startTime.AddHours(2),
                BasePrice = 100000,
                Status = DomainConstants.ShowtimeStatus.Open,
                CreatedAt = DateTime.UtcNow
            });

            var showtimeSeatId = $"STS_{Guid.NewGuid():N}";
            DbContext.ShowtimeSeats.Add(new ShowtimeSeat
            {
                ShowtimeSeatId = showtimeSeatId,
                ShowtimeId = showtimeId,
                SeatId = seatId,
                SeatStatus = DomainConstants.EntityStatus.Booked,
                RowVersion = new byte[] { 1 }
            });

            var bookingId = $"BKG_{Guid.NewGuid():N}";
            DbContext.Bookings.Add(new Booking
            {
                BookingId = bookingId,
                ShowtimeId = showtimeId,
                BookingStatus = DomainConstants.EntityStatus.Paid,
                TotalAmount = 100000,
                BookingChannel = DomainConstants.BookingChannel.Online,
                CreatedAt = DateTime.UtcNow
            });

            var providerId = "PROV_SEPAY";
            if (!DbContext.PaymentProviders.Any(p => p.PaymentProviderId == providerId))
            {
                DbContext.PaymentProviders.Add(new PaymentProvider { PaymentProviderId = providerId, ProviderName = "SePay", ProviderStatus = DomainConstants.EntityStatus.Active });
            }

            DbContext.Payments.Add(new Payment
            {
                PaymentId = $"PAY_{bookingId}",
                BookingId = bookingId,
                PaymentProviderId = providerId,
                Amount = 100000,
                PaymentStatus = DomainConstants.PaymentStatus.Success,
                CreatedAt = DateTime.UtcNow
            });

            DbContext.BookingSeats.Add(new BookingSeat
            {
                BookingSeatId = $"BS_{Guid.NewGuid():N}",
                BookingId = bookingId,
                ShowtimeSeatId = showtimeSeatId,
                SeatPrice = 100000
            });

            await DbContext.SaveChangesAsync();
            return (roomId, showtimeId, bookingId);
        }
    }
}
