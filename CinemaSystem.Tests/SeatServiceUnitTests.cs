using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Infrastructure.Persistence;

using CinemaSystem.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace CinemaSystem.Tests;

/// <summary>
/// Mức 1 — Unit test cho <see cref="SeatService.LockSeatAsync"/> và <see cref="SeatService.GetSeatMapAsync"/>.
/// Logic nguồn:
/// - CinemaSystem.Infrastructure/Services/SeatService.cs (LockSeatAsync, GetSeatMapAsync, ReleaseExpiredLocksAsync)
/// - CinemaSystem.Application/Interfaces/ISeatLockStore.cs
/// - CinemaSystem.Contracts/Seats/LockSeatRequest.cs
/// </summary>
public sealed class SeatServiceUnitTests
{
  private const string UserId = "USR_CUSTOMER_01";
  private const string ShowtimeId = "SHW_TEST_001";
  private const string SeatId = "SEAT_A1";
  private const string ShowtimeSeatId = "STS_TEST_001";

  [Fact]
  public async Task LockSeatAsync_HappyPath_LocksSeatSuccessfully()
  {
    // Coverage: nhánh thành công — ghế AVAILABLE, Redis TryLock = true, SaveChanges OK.
    var fixture = await UnitTestFixture.CreateAsync();
    var lockStoreMock = fixture.LockStoreMock;

    lockStoreMock
      .Setup(store => store.TryLockAsync(
        It.Is<string>(key => key == $"seat-lock:{ShowtimeId}:{SeatId}"),
        UserId,
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    var request = new LockSeatRequest
    {
      ShowtimeId = ShowtimeId,
      SeatId = SeatId
    };

    var result = await fixture.Service.LockSeatAsync(request, UserId, CancellationToken.None);

    Assert.True(result.Success);
    Assert.Equal(200, result.StatusCode);
    Assert.Equal("LOCKED", result.Data!.SeatStatus);
    Assert.Equal(ShowtimeSeatId, result.Data.ShowtimeSeatId);
    Assert.True(result.Data.LockedUntil > DateTime.UtcNow);

    var persisted = await fixture.DbContext.ShowtimeSeats.SingleAsync();
    Assert.Equal("LOCKED", persisted.SeatStatus);
    Assert.Equal(UserId, persisted.LockedByUserId);
    Assert.NotNull(persisted.LockedUntil);

    lockStoreMock.Verify(
      store => store.TryLockAsync(
        $"seat-lock:{ShowtimeId}:{SeatId}",
        UserId,
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()),
      Times.Once);
    lockStoreMock.Verify(
      store => store.ReleaseAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task LockSeatAsync_InvalidShowtimeId_ReturnsNotFound()
  {
    // Coverage: nhánh showtimeSeat == null khi ShowtimeId không khớp bản ghi SHOWTIME_SEAT.
    var fixture = await UnitTestFixture.CreateAsync();

    var request = new LockSeatRequest
    {
      ShowtimeId = "SHW_DOES_NOT_EXIST",
      SeatId = SeatId
    };

    var result = await fixture.Service.LockSeatAsync(request, UserId, CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(404, result.StatusCode);
    Assert.Equal("SHOWTIME_SEAT_NOT_FOUND", result.ErrorCode);

    fixture.LockStoreMock.Verify(
      store => store.TryLockAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task LockSeatAsync_SeatAlreadyLockedInDatabase_ReturnsConflict()
  {
    // Coverage: nhánh SeatStatus == LOCKED && LockedUntil > now (ghế đã bị khóa trong DB).
    // Production trả về ServiceResult 409 thay vì throw Exception.
    var fixture = await UnitTestFixture.CreateAsync();
    var showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
    showtimeSeat.SeatStatus = "LOCKED";
    showtimeSeat.LockedUntil = DateTime.UtcNow.AddMinutes(5);
    showtimeSeat.LockedByUserId = "USR_OTHER";
    await fixture.DbContext.SaveChangesAsync();

    var request = new LockSeatRequest
    {
      ShowtimeId = ShowtimeId,
      SeatId = SeatId
    };

    var result = await fixture.Service.LockSeatAsync(request, UserId, CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(409, result.StatusCode);
    Assert.Equal("SEAT_LOCKED", result.ErrorCode);

    fixture.LockStoreMock.Verify(
      store => store.TryLockAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()),
      Times.Never);
  }

  [Fact]
  public async Task LockSeatAsync_RedisLockRejected_ReturnsConflict()
  {
    // Coverage: nhánh TryLockAsync = false — ghế đang bị giữ ở tầng Redis (race condition / lock còn TTL).
    var fixture = await UnitTestFixture.CreateAsync();

    fixture.LockStoreMock
      .Setup(store => store.TryLockAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(false);

    var request = new LockSeatRequest
    {
      ShowtimeId = ShowtimeId,
      SeatId = SeatId
    };

    var result = await fixture.Service.LockSeatAsync(request, UserId, CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(409, result.StatusCode);
    Assert.Equal("SEAT_LOCKED", result.ErrorCode);

    var persisted = await fixture.DbContext.ShowtimeSeats.SingleAsync();
    Assert.Equal("AVAILABLE", persisted.SeatStatus);
    Assert.Null(persisted.LockedUntil);
  }

  [Fact]
  public async Task LockSeatAsync_MissingUserId_ReturnsUnauthorized()
  {
    // Coverage: nhánh validation userId rỗng.
    var fixture = await UnitTestFixture.CreateAsync();

    var result = await fixture.Service.LockSeatAsync(
      new LockSeatRequest { ShowtimeId = ShowtimeId, SeatId = SeatId },
      string.Empty,
      CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(401, result.StatusCode);
    Assert.Equal("USER_REQUIRED", result.ErrorCode);
  }

  [Fact]
  public async Task GetSeatMapAsync_ShowtimeNotFound_ReturnsNotFound()
  {
    // Coverage: nhánh showtime không tồn tại trong GetSeatMapAsync.
    var fixture = await UnitTestFixture.CreateAsync();

    var result = await fixture.Service.GetSeatMapAsync("SHW_MISSING", CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(404, result.StatusCode);
    Assert.Equal("SHOWTIME_NOT_FOUND", result.ErrorCode);
  }

  [Fact]
  public async Task GetSeatMapAsync_ExpiredLock_ReleasesAndReturnsAvailable()
  {
    // Coverage: ReleaseExpiredLocksAsync + phân loại ghế AVAILABLE sau khi LockedUntil hết hạn.
    var fixture = await UnitTestFixture.CreateAsync();
    var showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
    showtimeSeat.SeatStatus = "LOCKED";
    showtimeSeat.LockedUntil = DateTime.UtcNow.AddSeconds(-1);
    showtimeSeat.LockedByUserId = UserId;
    await fixture.DbContext.SaveChangesAsync();

    fixture.LockStoreMock
      .Setup(store => store.ReleaseAsync(
        $"seat-lock:{ShowtimeId}:{SeatId}",
        It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    var result = await fixture.Service.GetSeatMapAsync(ShowtimeId, CancellationToken.None);

    Assert.True(result.Success);
    Assert.Single(result.Data!.AvailableSeats);
    Assert.Empty(result.Data.LockedSeats);
    Assert.Equal(SeatId, result.Data.AvailableSeats[0].SeatId);
    Assert.Equal("AVAILABLE", result.Data.AvailableSeats[0].SeatStatus);
    Assert.Equal(90000m, result.Data.AvailableSeats[0].Price);

    fixture.LockStoreMock.Verify(
      store => store.ReleaseAsync(
        $"seat-lock:{ShowtimeId}:{SeatId}",
        It.IsAny<CancellationToken>()),
      Times.Once);
  }

  [Fact]
  public async Task LockSeatAsync_SeatStatusBooked_ReturnsSoldConflict()
  {
    // [Điểm mù - Nghiệp vụ] Ghế đã BOOKED không được khóa lại — nhánh SEAT_SOLD (SeatStatus == BOOKED).
    var fixture = await UnitTestFixture.CreateAsync();
    var showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
    showtimeSeat.SeatStatus = "BOOKED";
    await fixture.DbContext.SaveChangesAsync();

    var result = await fixture.Service.LockSeatAsync(
      new LockSeatRequest { ShowtimeId = ShowtimeId, SeatId = SeatId },
      UserId,
      CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(409, result.StatusCode);
    Assert.Equal("SEAT_SOLD", result.ErrorCode);
  }

  [Fact]
  public async Task LockSeatAsync_BookingSeatExists_ReturnsSoldConflict()
  {
    // [Điểm mù - Nghiệp vụ] Ghế đã có bản ghi BookingSeat (đã bán) — nhánh SEAT_SOLD (BookingSeat != null).
    var fixture = await UnitTestFixture.CreateAsync();
    fixture.DbContext.Bookings.Add(new Booking
    {
      BookingId = "BKG_TEST_001",
      ShowtimeId = ShowtimeId,
      BookingStatus = "PAID",
      TotalAmount = 90000,
      CreatedAt = DateTime.UtcNow,
      BookingChannel = "ONLINE"
    });
    fixture.DbContext.BookingSeats.Add(new BookingSeat
    {
      BookingSeatId = "BKS_TEST_001",
      BookingId = "BKG_TEST_001",
      ShowtimeSeatId = ShowtimeSeatId,
      SeatPrice = 90000
    });
    await fixture.DbContext.SaveChangesAsync();

    var result = await fixture.Service.LockSeatAsync(
      new LockSeatRequest { ShowtimeId = ShowtimeId, SeatId = SeatId },
      UserId,
      CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(409, result.StatusCode);
    Assert.Equal("SEAT_SOLD", result.ErrorCode);
  }

  [Fact]
  public async Task LockSeatAsync_InvalidSeatId_ReturnsNotFound()
  {
    // [Điểm mù - Validation] ShowtimeId hợp lệ nhưng SeatId không thuộc suất chiếu — 404 SHOWTIME_SEAT_NOT_FOUND.
    var fixture = await UnitTestFixture.CreateAsync();

    var result = await fixture.Service.LockSeatAsync(
      new LockSeatRequest { ShowtimeId = ShowtimeId, SeatId = "SEAT_DOES_NOT_EXIST" },
      UserId,
      CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(404, result.StatusCode);
    Assert.Equal("SHOWTIME_SEAT_NOT_FOUND", result.ErrorCode);
  }

  [Fact]
  public async Task LockSeatAsync_ExpiredDbLock_AllowsReLock()
  {
    // [Điểm mù - Time-based] Ghế LOCKED nhưng LockedUntil đã hết hạn — cho phép khóa lại (boundary: LockedUntil <= now).
    var fixture = await UnitTestFixture.CreateAsync();
    var showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
    showtimeSeat.SeatStatus = "LOCKED";
    showtimeSeat.LockedUntil = DateTime.UtcNow.AddSeconds(-30);
    showtimeSeat.LockedByUserId = "USR_PREVIOUS";
    await fixture.DbContext.SaveChangesAsync();

    fixture.LockStoreMock
      .Setup(store => store.TryLockAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    var result = await fixture.Service.LockSeatAsync(
      new LockSeatRequest { ShowtimeId = ShowtimeId, SeatId = SeatId },
      UserId,
      CancellationToken.None);

    Assert.True(result.Success);
    Assert.Equal("LOCKED", result.Data!.SeatStatus);

    var persisted = await fixture.DbContext.ShowtimeSeats.SingleAsync();
    Assert.Equal(UserId, persisted.LockedByUserId);
  }

  [Fact]
  public async Task LockSeatAsync_LockedUntilExactlyNow_AllowsReLock()
  {
    // [Điểm mù - Boundary Value] LockedUntil == UtcNow (không lớn hơn now) — không bị chặn bởi nhánh SEAT_LOCKED.
    var fixture = await UnitTestFixture.CreateAsync();
    var showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
    showtimeSeat.SeatStatus = "LOCKED";
    showtimeSeat.LockedUntil = DateTime.UtcNow;
    showtimeSeat.LockedByUserId = "USR_PREVIOUS";
    await fixture.DbContext.SaveChangesAsync();

    fixture.LockStoreMock
      .Setup(store => store.TryLockAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);

    var result = await fixture.Service.LockSeatAsync(
      new LockSeatRequest { ShowtimeId = ShowtimeId, SeatId = SeatId },
      UserId,
      CancellationToken.None);

    Assert.True(result.Success);
  }

  [Fact]
  public async Task LockSeatAsync_AnotherUserActiveLock_ReturnsConflict()
  {
    // [Điểm mù - Concurrency/Nghiệp vụ] User B cố khóa ghế user A đang giữ (LOCKED + LockedUntil còn hiệu lực).
    var fixture = await UnitTestFixture.CreateAsync();
    var showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
    showtimeSeat.SeatStatus = "LOCKED";
    showtimeSeat.LockedUntil = DateTime.UtcNow.AddMinutes(8);
    showtimeSeat.LockedByUserId = "USR_USER_A";
    await fixture.DbContext.SaveChangesAsync();

    var result = await fixture.Service.LockSeatAsync(
      new LockSeatRequest { ShowtimeId = ShowtimeId, SeatId = SeatId },
      "USR_USER_B",
      CancellationToken.None);

    Assert.False(result.Success);
    Assert.Equal(409, result.StatusCode);
    Assert.Equal("SEAT_LOCKED", result.ErrorCode);
    Assert.Equal("USR_USER_A", (await fixture.DbContext.ShowtimeSeats.SingleAsync()).LockedByUserId);
  }

  [Fact]
  public async Task LockSeatAsync_DatabaseSaveFails_ReleasesRedisLockAndRethrows()
  {
    // [Điểm mù - Partial Failure] Redis lock thành công nhưng DB SaveChanges lỗi — phải ReleaseAsync Redis rồi rethrow.
    var options = new DbContextOptionsBuilder<CinemaDbContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
      .Options;
    var dbContext = new SaveChangesThrowingDbContext(options);
    await UnitTestFixture.SeedShowtimeSeatAsync(dbContext);

    var lockStoreMock = new Mock<ISeatLockStore>(MockBehavior.Strict);
    lockStoreMock
      .Setup(store => store.TryLockAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()))
      .ReturnsAsync(true);
    lockStoreMock
      .Setup(store => store.ReleaseAsync(
        $"seat-lock:{ShowtimeId}:{SeatId}",
        It.IsAny<CancellationToken>()))
      .Returns(Task.CompletedTask);

    dbContext.ThrowOnNextSave = true;
    var service = new SeatService(
      dbContext,
      new Mock<Hangfire.IBackgroundJobClient>().Object,
      new Mock<IAdminRefundService>().Object,
      Microsoft.Extensions.Options.Options.Create(new CinemaSystem.Application.Settings.SecuritySettings()),
      Microsoft.Extensions.Options.Options.Create(new CinemaSystem.Application.Settings.EmailTemplatesSettings()),
      lockStoreMock.Object);

    await Assert.ThrowsAsync<DbUpdateException>(() =>
      service.LockSeatAsync(
        new LockSeatRequest { ShowtimeId = ShowtimeId, SeatId = SeatId },
        UserId,
        CancellationToken.None));

    lockStoreMock.Verify(
      store => store.ReleaseAsync(
        $"seat-lock:{ShowtimeId}:{SeatId}",
        It.IsAny<CancellationToken>()),
      Times.Once);

    // Clear change tracker để đọc lại từ store — SaveChanges thất bại nên DB không được commit.
    dbContext.ChangeTracker.Clear();
    var persisted = await dbContext.ShowtimeSeats.AsNoTracking().SingleAsync();
    Assert.Equal("AVAILABLE", persisted.SeatStatus);
  }

  [Fact]
  public async Task LockSeatAsync_RedisThrowsException_PropagatesFault()
  {
    // [Điểm mù - Infrastructure] Redis timeout/connection lỗi — hiện tại exception lan ra (chưa có retry/fallback).
    var fixture = await UnitTestFixture.CreateAsync();

    fixture.LockStoreMock
      .Setup(store => store.TryLockAsync(
        It.IsAny<string>(),
        It.IsAny<string>(),
        It.IsAny<TimeSpan>(),
        It.IsAny<CancellationToken>()))
      .ThrowsAsync(new TimeoutException("Redis connection timed out."));

    await Assert.ThrowsAsync<TimeoutException>(() =>
      fixture.Service.LockSeatAsync(
        new LockSeatRequest { ShowtimeId = ShowtimeId, SeatId = SeatId },
        UserId,
        CancellationToken.None));
  }

  [Fact]
  public async Task LockSeatAsync_ParallelRequests_OnlyOneSucceeds()
  {
    // [Điểm mù - Race Condition] Nhiều user gọi lock cùng ghế đồng thời — chỉ 1 thành công, còn lại 409.
    var options = new DbContextOptionsBuilder<CinemaDbContext>()
      .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
      .Options;
    var dbContext = new CinemaDbContext(options);
    await UnitTestFixture.SeedShowtimeSeatAsync(dbContext);

    var lockStore = new InMemorySeatLockStore();
    var service = new SeatService(
      dbContext,
      new Mock<Hangfire.IBackgroundJobClient>().Object,
      new Mock<IAdminRefundService>().Object,
      Microsoft.Extensions.Options.Options.Create(new CinemaSystem.Application.Settings.SecuritySettings()),
      Microsoft.Extensions.Options.Options.Create(new CinemaSystem.Application.Settings.EmailTemplatesSettings()),
      lockStore);
    var request = new LockSeatRequest { ShowtimeId = ShowtimeId, SeatId = SeatId };

    var tasks = Enumerable.Range(0, 8)
      .Select(index => service.LockSeatAsync(request, $"USR_PARALLEL_{index}", CancellationToken.None));
    var results = await Task.WhenAll(tasks);

    Assert.Equal(1, results.Count(item => item.Success));
    Assert.Equal(7, results.Count(item => !item.Success && item.ErrorCode == "SEAT_LOCKED"));

    var persisted = await dbContext.ShowtimeSeats.SingleAsync();
    Assert.Equal("LOCKED", persisted.SeatStatus);
    Assert.NotNull(persisted.LockedByUserId);
  }

  [Fact]
  public async Task GetSeatMapAsync_SoldSeat_AppearsInSoldList()
  {
    // [Điểm mù - Nghiệp vụ] Sơ đồ ghế phân loại đúng ghế đã bán vào SoldSeats.
    var fixture = await UnitTestFixture.CreateAsync();
    var showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
    showtimeSeat.SeatStatus = "BOOKED";
    await fixture.DbContext.SaveChangesAsync();

    var result = await fixture.Service.GetSeatMapAsync(ShowtimeId, CancellationToken.None);

    Assert.True(result.Success);
    Assert.Empty(result.Data!.AvailableSeats);
    Assert.Empty(result.Data.LockedSeats);
    var soldSeat = Assert.Single(result.Data.SoldSeats);
    Assert.Equal("BOOKED", soldSeat.SeatStatus);
  }

  [Fact]
  public async Task GetSeatMapAsync_ActiveLock_AppearsInLockedList()
  {
    // [Điểm mù - Consistency] Ghế đang LOCKED (TTL còn hiệu lực) phải nằm trong LockedSeats, không phải AvailableSeats.
    var fixture = await UnitTestFixture.CreateAsync();
    var showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
    showtimeSeat.SeatStatus = "LOCKED";
    showtimeSeat.LockedUntil = DateTime.UtcNow.AddMinutes(5);
    showtimeSeat.LockedByUserId = UserId;
    await fixture.DbContext.SaveChangesAsync();

    var result = await fixture.Service.GetSeatMapAsync(ShowtimeId, CancellationToken.None);

    Assert.True(result.Success);
    Assert.Empty(result.Data!.AvailableSeats);
    var lockedSeat = Assert.Single(result.Data.LockedSeats);
    Assert.Equal("LOCKED", lockedSeat.SeatStatus);
    Assert.Equal(90000m, lockedSeat.Price);
    Assert.NotNull(lockedSeat.LockedUntil);
  }

  private sealed class SaveChangesThrowingDbContext : CinemaDbContext
  {
    public SaveChangesThrowingDbContext(DbContextOptions<CinemaDbContext> options)
      : base(options)
    {
    }

    public bool ThrowOnNextSave { get; set; }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
      if (ThrowOnNextSave)
      {
        ThrowOnNextSave = false;
        throw new DbUpdateException("Simulated database failure during seat lock persist.");
      }

      return base.SaveChangesAsync(cancellationToken);
    }
  }

  private sealed class UnitTestFixture
  {
    private UnitTestFixture(
      CinemaDbContext dbContext,
      Mock<ISeatLockStore> lockStoreMock,
      SeatService service)
    {
      DbContext = dbContext;
      LockStoreMock = lockStoreMock;
      Service = service;
    }

    public CinemaDbContext DbContext { get; }

    public Mock<ISeatLockStore> LockStoreMock { get; }

    public SeatService Service { get; }

    public static async Task<UnitTestFixture> CreateAsync()
    {
      var options = new DbContextOptionsBuilder<CinemaDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
        .Options;
      var dbContext = new CinemaDbContext(options);
      await SeedShowtimeSeatAsync(dbContext);

      var lockStoreMock = new Mock<ISeatLockStore>(MockBehavior.Strict);
      var service = new SeatService(
        dbContext,
        new Mock<Hangfire.IBackgroundJobClient>().Object,
        new Mock<IAdminRefundService>().Object,
        Microsoft.Extensions.Options.Options.Create(new CinemaSystem.Application.Settings.SecuritySettings()),
        Microsoft.Extensions.Options.Options.Create(new CinemaSystem.Application.Settings.EmailTemplatesSettings()),
        lockStoreMock.Object);

      return new UnitTestFixture(dbContext, lockStoreMock, service);
    }

    internal static async Task SeedShowtimeSeatAsync(CinemaDbContext dbContext)
    {
      dbContext.Cinemas.Add(new Cinema
      {
        CinemaId = "CIN_TEST",
        CinemaName = "Test Cinema",
        Address = "1 Test Street",
        City = "HCM",
        CinemaStatus = "ACTIVE"
      });

      dbContext.Movies.Add(new Movie
      {
        MovieId = "MOV_TEST",
        Title = "Test Movie",
        DurationMinutes = 120,
        AgeRating = "T13",
        MovieStatus = "NOW_SHOWING"
      });

      dbContext.SeatTypes.Add(new SeatType
      {
        SeatTypeId = "SEAT_TYPE_STANDARD",
        TypeName = "STANDARD",
        ExtraFee = 0
      });

      dbContext.Rooms.Add(new Room
      {
        RoomId = "ROOM_TEST",
        CinemaId = "CIN_TEST",
        RoomName = "Room 1",
        Capacity = 1,
        RoomStatus = "ACTIVE"
      });

      dbContext.Seats.Add(new Seat
      {
        SeatId = SeatId,
        RoomId = "ROOM_TEST",
        SeatTypeId = "SEAT_TYPE_STANDARD",
        SeatCode = "A1",
        RowLabel = "A",
        SeatNumber = 1,
        IsActive = true
      });

      dbContext.Showtimes.Add(new Showtime
      {
        ShowtimeId = ShowtimeId,
        MovieId = "MOV_TEST",
        RoomId = "ROOM_TEST",
        StartTime = DateTime.UtcNow.AddHours(2),
        EndTime = DateTime.UtcNow.AddHours(4),
        BasePrice = 90000,
        Status = "OPEN",
        CreatedAt = DateTime.UtcNow
      });

      dbContext.ShowtimeSeats.Add(new ShowtimeSeat
      {
        ShowtimeSeatId = ShowtimeSeatId,
        ShowtimeId = ShowtimeId,
        SeatId = SeatId,
        SeatStatus = "AVAILABLE",
        RowVersion = new byte[8]
      });

      await dbContext.SaveChangesAsync();
    }
  }
}
