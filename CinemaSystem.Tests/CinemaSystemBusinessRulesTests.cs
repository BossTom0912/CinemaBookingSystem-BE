using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Contracts.Movies;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Rooms;
using CinemaSystem.Infrastructure.Services;
using CinemaSystem.Infrastructure.Showtimes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CinemaSystem.Tests;

public sealed class CinemaSystemBusinessRulesTests
{
    // =========================================================================
    // SECTION 1: ROOM MANAGEMENT (RM)
    // =========================================================================

    [Fact]
    public async Task CreateRoom_CinemaNotFound_Returns404()
    {
        // TC-RM-001: Tạo phòng gắn với rạp không tồn tại -> 404 CINEMA_NOT_FOUND.
        var fixture = TestFixture.Create();
        var request = new CreateRoomRequest
        {
            RoomName = "Room A",
            Capacity = 100,
            RoomStatus = "ACTIVE"
        };

        var result = await fixture.RoomService.CreateRoomAsync("CIN_INVALID", request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("CINEMA_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateRoom_DuplicateNameSameCinema_Returns409()
    {
        // TC-RM-002: Trùng tên phòng trong cùng một rạp -> 409 DUPLICATE_ROOM_NAME.
        var fixture = TestFixture.Create();
        await fixture.SeedCinemaAsync("CIN_01", "Beta Cinema");

        // Tạo 2 phòng
        var room1Result = await fixture.RoomService.CreateRoomAsync("CIN_01", new CreateRoomRequest { RoomName = "Room 1", Capacity = 10, RoomStatus = "ACTIVE" }, CancellationToken.None);
        var room2Result = await fixture.RoomService.CreateRoomAsync("CIN_01", new CreateRoomRequest { RoomName = "Room 2", Capacity = 10, RoomStatus = "ACTIVE" }, CancellationToken.None);

        // Update phòng 2 thành tên "Room 1" (có khoảng trắng và chữ hoa thường khác)
        var updateRequest = new UpdateRoomRequest
        {
            RoomName = "  ROOM 1  ",
            Capacity = 20,
            RoomStatus = "ACTIVE"
        };

        var result = await fixture.RoomService.UpdateRoomAsync(room2Result.Data!.RoomId, updateRequest, "ADMIN", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("DUPLICATE_ROOM_NAME", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateRoom_CapacityLessThanExistingSeats_Returns400()
    {
        // TC-RM-003: Hạ capacity nhỏ hơn số lượng ghế thực tế -> 400 INVALID_CAPACITY.
        var fixture = TestFixture.Create();
        await fixture.SeedCinemaAsync("CIN_01", "Beta Cinema");
        var roomResult = await fixture.RoomService.CreateRoomAsync("CIN_01", new CreateRoomRequest { RoomName = "Room 1", Capacity = 10, RoomStatus = "ACTIVE" }, CancellationToken.None);
        var roomId = roomResult.Data!.RoomId;

        // Sinh 5 ghế
        fixture.DbContext.SeatTypes.Add(new SeatType { SeatTypeId = "SEAT_TYPE_STANDARD", TypeName = "STANDARD", ExtraFee = 0 });
        await fixture.DbContext.SaveChangesAsync();
        await fixture.RoomService.GenerateSeatsAsync(roomId, new GenerateSeatsRequest { Rows = 1, Columns = 5, SeatTypeId = "SEAT_TYPE_STANDARD" }, CancellationToken.None);

        // Cập nhật capacity nhỏ hơn 5
        var updateRequest = new UpdateRoomRequest { RoomName = "Room 1", Capacity = 3, RoomStatus = "ACTIVE" };
        var result = await fixture.RoomService.UpdateRoomAsync(roomId, updateRequest, "ADMIN", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("INVALID_CAPACITY", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateRoom_StatusToMaintenance_SuspendsOpenShowtimes()
    {
        // TC-RM-004: Đình chỉ suất chiếu liên đới (Cascading Suspension) khi đóng phòng -> các showtime chuyển sang SUSPENDED.
        var fixture = TestFixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        // Tạo showtime OPEN
        var showtime = new Showtime
        {
            ShowtimeId = "SHW_01",
            MovieId = "MOV_TEST",
            RoomId = "ROOM_TEST",
            StartTime = DateTime.UtcNow.AddHours(5),
            EndTime = DateTime.UtcNow.AddHours(7),
            BasePrice = 90000,
            Status = DomainConstants.EntityStatus.Open
        };
        fixture.DbContext.Showtimes.Add(showtime);
        await fixture.DbContext.SaveChangesAsync();

        // Đổi phòng thành MAINTENANCE
        var updateRequest = new UpdateRoomRequest { RoomName = "Room Test", Capacity = 10, RoomStatus = "MAINTENANCE" };
        var result = await fixture.RoomService.UpdateRoomAsync("ROOM_TEST", updateRequest, "ADMIN", CancellationToken.None);

        Assert.True(result.Success);
        var updatedShowtime = await fixture.DbContext.Showtimes.FindAsync("SHW_01");
        Assert.Equal("SUSPENDED", updatedShowtime!.Status);
    }

    [Fact]
    public async Task DeleteRoom_DeactivatesRoomAndSuspendsShowtimes()
    {
        // TC-RM-005: Xóa mềm phòng đang hoạt động -> chuyển sang INACTIVE và đình chỉ các showtime OPEN.
        var fixture = TestFixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        var showtime = new Showtime
        {
            ShowtimeId = "SHW_01",
            MovieId = "MOV_TEST",
            RoomId = "ROOM_TEST",
            StartTime = DateTime.UtcNow.AddHours(5),
            EndTime = DateTime.UtcNow.AddHours(7),
            BasePrice = 90000,
            Status = DomainConstants.EntityStatus.Open
        };
        fixture.DbContext.Showtimes.Add(showtime);
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.RoomService.DeleteRoomAsync("ROOM_TEST", "ADMIN", CancellationToken.None);

        Assert.True(result.Success);
        var room = await fixture.DbContext.Rooms.FindAsync("ROOM_TEST");
        Assert.Equal("INACTIVE", room!.RoomStatus);

        var updatedShowtime = await fixture.DbContext.Showtimes.FindAsync("SHW_01");
        Assert.Equal("SUSPENDED", updatedShowtime!.Status);
    }

    [Fact]
    public async Task GenerateSeats_AlreadyHasSeats_ReturnsConflict()
    {
        // TC-RM-006: Sinh ghế tự động khi đã có ghế -> 409 ROOM_HAS_SEATS.
        var fixture = TestFixture.Create();
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
    public async Task GenerateSeats_ValidExecution_UpdatesCapacity()
    {
        // TC-RM-007: Sinh ghế tự động hợp lệ -> cập nhật capacity và sinh đúng mã hàng cột.
        var fixture = TestFixture.Create();
        await fixture.SeedCinemaAsync("CIN_01", "Beta Cinema");
        var roomResult = await fixture.RoomService.CreateRoomAsync("CIN_01", new CreateRoomRequest { RoomName = "Room Empty", Capacity = 100, RoomStatus = "ACTIVE" }, CancellationToken.None);
        var roomId = roomResult.Data!.RoomId;

        fixture.DbContext.SeatTypes.Add(new SeatType { SeatTypeId = "SEAT_TYPE_STANDARD", TypeName = "STANDARD", ExtraFee = 0 });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.RoomService.GenerateSeatsAsync(
            roomId,
            new GenerateSeatsRequest { Rows = 2, Columns = 12, SeatTypeId = "SEAT_TYPE_STANDARD" },
            CancellationToken.None);

        Assert.True(result.Success);
        var room = await fixture.DbContext.Rooms.Include(r => r.Seats).FirstOrDefaultAsync(r => r.RoomId == roomId);
        Assert.Equal(24, room!.Capacity);
        Assert.Equal(24, room.Seats.Count);
        Assert.Contains(room.Seats, s => s.SeatCode == "A12");
        Assert.Contains(room.Seats, s => s.SeatCode == "B12");
    }

    // =========================================================================
    // SECTION 2: SEAT MANAGEMENT (SM)
    // =========================================================================

    [Fact]
    public async Task UpdateSeat_DeactivateSeatWithFutureBookings_Returns400()
    {
        // TC-SM-003: Đưa ghế vào bảo trì (IsActive = false) khi đang có khách đặt trong tương lai -> SEAT_HAS_FUTURE_BOOKINGS.
        var fixture = TestFixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        // Tạo showtime tương lai
        var showtime = new Showtime
        {
            ShowtimeId = "SHW_FUTURE",
            MovieId = "MOV_TEST",
            RoomId = "ROOM_TEST",
            StartTime = DateTime.UtcNow.AddDays(5),
            EndTime = DateTime.UtcNow.AddDays(5).AddHours(2),
            BasePrice = 90000,
            Status = DomainConstants.EntityStatus.Open
        };
        fixture.DbContext.Showtimes.Add(showtime);

        // Tạo ShowtimeSeat
        var showtimeSeat = new ShowtimeSeat
        {
            ShowtimeSeatId = "STS_01",
            ShowtimeId = "SHW_FUTURE",
            SeatId = "SEAT_1", // Mã ghế A1
            SeatStatus = "AVAILABLE"
        };
        fixture.DbContext.ShowtimeSeats.Add(showtimeSeat);

        // Tạo BookingSeat liên kết
        var booking = new Booking
        {
            BookingId = "BKG_01",
            BookingStatus = DomainConstants.EntityStatus.Paid,
            ShowtimeId = "SHW_FUTURE"
        };
        fixture.DbContext.Bookings.Add(booking);

        var bookingSeat = new BookingSeat
        {
            BookingSeatId = "BS_01",
            BookingId = "BKG_01",
            ShowtimeSeatId = "STS_01"
        };
        fixture.DbContext.BookingSeats.Add(bookingSeat);

        await fixture.DbContext.SaveChangesAsync();

        // Admin cố đưa ghế SEAT_1 về bảo trì
        var request = new UpdateSeatRequest
        {
            SeatId = "SEAT_1",
            RowLabel = "A",
            SeatNumber = 1,
            SeatTypeId = "SEAT_TYPE_STANDARD",
            IsActive = false
        };

        var result = await fixture.SeatService.UpdateSeatAsync(request, "ADMIN", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("SEAT_HAS_FUTURE_BOOKINGS", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateSeat_DeactivateSeatNoFutureBookings_Succeeds()
    {
        // TC-SM-005: Đóng ghế bảo trì thành công khi không có lịch đặt tương lai.
        var fixture = TestFixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        var request = new UpdateSeatRequest
        {
            SeatId = "SEAT_1",
            RowLabel = "A",
            SeatNumber = 1,
            SeatTypeId = "SEAT_TYPE_STANDARD",
            IsActive = false
        };

        var result = await fixture.SeatService.UpdateSeatAsync(request, "ADMIN", CancellationToken.None);

        Assert.True(result.Success);
        var seat = await fixture.DbContext.Seats.FindAsync("SEAT_1");
        Assert.False(seat!.IsActive);
    }

    // =========================================================================
    // SECTION 3: SHOWTIME MANAGEMENT (ST)
    // =========================================================================

    [Fact]
    public async Task CreateShowtime_CloserThanBlockingMinutes_Returns400()
    {
        // TC-ST-001: Tạo suất chiếu sát giờ -> 400 PRE_SHOWTIME_BLOCK.
        var fixture = TestFixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        var request = new CreateShowtimeRequest
        {
            MovieId = "MOV_TEST",
            RoomId = "ROOM_TEST",
            StartTime = DateTime.UtcNow.AddMinutes(5), // Block = 60 phút
            BasePrice = 90000
        };

        var result = await fixture.ShowtimeService.CreateShowtimeAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("PRE_SHOWTIME_BLOCK", result.ErrorCode);
    }

    [Fact]
    public async Task CreateShowtime_SeatCloning_ClonesActiveSeats()
    {
        // TC-ST-003: Tự động nhân bản ghế khi tạo suất thành công.
        var fixture = TestFixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        var request = new CreateShowtimeRequest
        {
            MovieId = "MOV_TEST",
            RoomId = "ROOM_TEST",
            StartTime = DateTime.UtcNow.AddHours(5),
            BasePrice = 90000
        };

        var result = await fixture.ShowtimeService.CreateShowtimeAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(10, result.Data!.ShowtimeSeatCount);

        var clonedSeats = await fixture.DbContext.ShowtimeSeats
            .Where(sts => sts.ShowtimeId == result.Data.ShowtimeId)
            .ToListAsync();
        Assert.Equal(10, clonedSeats.Count);
        Assert.All(clonedSeats, sts => Assert.Equal("AVAILABLE", sts.SeatStatus));
    }

    // =========================================================================
    // SECTION 4: MOVIE MANAGEMENT (MV)
    // =========================================================================

    [Fact]
    public async Task GetMovies_PublicVisibilityFilter_FiltersInactiveAndAgeRatingC()
    {
        // TC-MV-001: Lọc hiển thị công khai -> ẩn Inactive và tuổi cấm C.
        var fixture = TestFixture.Create();
        
        fixture.DbContext.Movies.AddRange(
            new Movie { MovieId = "MOV_ACTIVE", Title = "Active Movie", ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), MovieStatus = "NOW_SHOWING", AgeRating = "T16" },
            new Movie { MovieId = "MOV_INACTIVE", Title = "Inactive Movie", ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), MovieStatus = "INACTIVE", AgeRating = "T16" },
            new Movie { MovieId = "MOV_C", Title = "Restricted Movie", ReleaseDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1)), MovieStatus = "NOW_SHOWING", AgeRating = "C" }
        );
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.MovieService.GetMoviesAsync(
            status: null,
            pageIndex: 1,
            pageSize: 10,
            genre: null,
            includeDeleted: false,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data!.Items);
        Assert.Equal("MOV_ACTIVE", result.Data.Items[0].Id);
    }

    [Fact]
    public async Task UpdateMovie_DurationChangeWithBookings_Returns400()
    {
        // TC-MV-003: Chặn sửa thời lượng phim khi có showtime chứa booking hoạt động.
        var fixture = TestFixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        // Tạo showtime
        var showtime = new Showtime
        {
            ShowtimeId = "SHW_1",
            MovieId = "MOV_TEST",
            RoomId = "ROOM_TEST",
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
            BasePrice = 90000,
            Status = DomainConstants.EntityStatus.Open
        };
        fixture.DbContext.Showtimes.Add(showtime);

        // Tạo booking hoạt động
        var booking = new Booking
        {
            BookingId = "BKG_1",
            ShowtimeId = "SHW_1",
            BookingStatus = DomainConstants.EntityStatus.Paid
        };
        fixture.DbContext.Bookings.Add(booking);
        await fixture.DbContext.SaveChangesAsync();

        var updateRequest = new UpdateMovieRequest
        {
            Title = "Test Movie",
            DurationMinutes = 150, // Cố tình đổi từ 120 -> 150
            MovieStatus = "NOW_SHOWING"
        };

        var result = await fixture.MovieService.UpdateMovieAsync("MOV_TEST", updateRequest, null, null, "ADMIN", CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal("DURATION_CANNOT_BE_CHANGED_HAS_SHOWTIMES", result.ErrorCode);
    }

    [Fact]
    public async Task UpdateMovie_DurationChangeNoBookings_Succeeds()
    {
        // Kiểm tra logic mới: Có showtime nhưng KHÔNG có booking -> vẫn cho sửa bình thường.
        var fixture = TestFixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        // Tạo showtime nhưng không có booking
        var showtime = new Showtime
        {
            ShowtimeId = "SHW_1",
            MovieId = "MOV_TEST",
            RoomId = "ROOM_TEST",
            StartTime = DateTime.UtcNow.AddDays(1),
            EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
            BasePrice = 90000,
            Status = DomainConstants.EntityStatus.Open
        };
        fixture.DbContext.Showtimes.Add(showtime);
        await fixture.DbContext.SaveChangesAsync();

        var updateRequest = new UpdateMovieRequest
        {
            Title = "Test Movie Updated",
            DurationMinutes = 150, // Đổi từ 120 -> 150
            MovieStatus = "NOW_SHOWING"
        };

        var result = await fixture.MovieService.UpdateMovieAsync("MOV_TEST", updateRequest, null, null, "ADMIN", CancellationToken.None);

        Assert.True(result.Success);
        var movie = await fixture.DbContext.Movies.FindAsync("MOV_TEST");
        Assert.Equal(150, movie!.DurationMinutes);
    }

    // =========================================================================
    // HELPERS & FIXTURE CONFIGURATION
    // =========================================================================

    private sealed class TestFixture
    {
        private TestFixture(
            CinemaDbContext dbContext,
            RoomService roomService,
            SeatService seatService,
            ShowtimeService showtimeService,
            MovieService movieService)
        {
            DbContext = dbContext;
            RoomService = roomService;
            SeatService = seatService;
            ShowtimeService = showtimeService;
            MovieService = movieService;
        }

        public CinemaDbContext DbContext { get; }
        public RoomService RoomService { get; }
        public SeatService SeatService { get; }
        public ShowtimeService ShowtimeService { get; }
        public MovieService MovieService { get; }

        public static TestFixture Create()
        {
            var options = new DbContextOptionsBuilder<CinemaDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var dbContext = new CinemaDbContext(options);

            var mockClock = new Mock<IClock>();
            mockClock.Setup(c => c.UtcNow).Returns(DateTime.UtcNow);

            var settings = Options.Create(new CinemaProcessingSettings
            {
                MaxRoomCapacity = 100,
                PreShowtimeBlockingMinutes = 60,
                ScreeningRoomCleaningMinutes = 15
            });

            var mockRefundService = new Mock<IAdminRefundService>();
            var mockJobClient = new Mock<Hangfire.IBackgroundJobClient>();
            var mockHttpContextAccessor = new Mock<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
            var mockFileStorage = new Mock<IFileStorageService>();

            var roomService = new RoomService(dbContext, mockRefundService.Object, settings);
            
            var seatService = new SeatService(
                dbContext, 
                new Mock<ISeatLockStore>().Object,
                mockClock.Object,
                settings,
                mockJobClient.Object,
                new Mock<IEmailService>().Object,
                Options.Create(new SecuritySettings { ConfirmationTokenSecret = "test-secret" })
            );

            var showtimeService = new ShowtimeService(
                dbContext,
                mockClock.Object,
                settings,
                mockJobClient.Object,
                mockHttpContextAccessor.Object,
                new Mock<IAiEmailService>().Object,
                Options.Create(new SecuritySettings { ConfirmationTokenSecret = "test-secret" })
            );

            var movieService = new MovieService(
                dbContext,
                mockRefundService.Object,
                mockFileStorage.Object,
                settings
            );

            return new TestFixture(dbContext, roomService, seatService, showtimeService, movieService);
        }

        public async Task SeedCinemaAsync(string cinemaId, string name)
        {
            DbContext.Cinemas.Add(new Cinema
            {
                CinemaId = cinemaId,
                CinemaName = name,
                Address = "123 Test Street",
                City = "HCM",
                CinemaStatus = "ACTIVE"
            });
            await DbContext.SaveChangesAsync();
        }

        public async Task SeedCinemaMovieAndRoomWithSeatsAsync()
        {
            await SeedCinemaAsync("CIN_TEST", "Test Cinema");

            DbContext.Movies.Add(new Movie
            {
                MovieId = "MOV_TEST",
                Title = "Test Movie",
                DurationMinutes = 120,
                MovieStatus = "NOW_SHOWING",
                AgeRating = "T16"
            });

            DbContext.Rooms.Add(new Room
            {
                RoomId = "ROOM_TEST",
                CinemaId = "CIN_TEST",
                RoomName = "Room Test",
                Capacity = 10,
                RoomStatus = "ACTIVE"
            });

            DbContext.SeatTypes.Add(new SeatType
            {
                SeatTypeId = "SEAT_TYPE_STANDARD",
                TypeName = "STANDARD",
                ExtraFee = 0
            });

            for (int i = 1; i <= 10; i++)
            {
                DbContext.Seats.Add(new Seat
                {
                    SeatId = $"SEAT_{i}",
                    RoomId = "ROOM_TEST",
                    SeatCode = $"A{i}",
                    RowLabel = "A",
                    SeatNumber = i,
                    SeatTypeId = "SEAT_TYPE_STANDARD",
                    IsActive = true
                });
            }

            await DbContext.SaveChangesAsync();
        }
    }
}
