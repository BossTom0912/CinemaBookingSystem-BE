using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Identity;
using CinemaSystem.Infrastructure.Persistence;

using CinemaSystem.Infrastructure.Time;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Tests;

/// <summary>
/// Mức 2 — Integration / API test E2E cho Lock Seat và Get Seat Map.
/// Logic nguồn:
/// - CinemaSystem/Controllers/SeatsController.cs (POST api/seats/lock, GET api/seats/showtimes/{showtimeId}/map)
/// - CinemaSystem.Infrastructure/Services/SeatService.cs
/// - CinemaSystem/Program.cs (JWT auth, policies CanBookTicket / CanSelectSeat)
/// </summary>
public sealed class SeatApiIntegrationTests
{
  private static readonly JsonSerializerOptions JsonOptions = new()
  {
    PropertyNameCaseInsensitive = true
  };

  [Fact]
  public async Task LockSeat_E2E_GetMapThenLockThenConflictThenTtlRelease_ReturnsAvailableAgain()
  {
    // Coverage E2E: luồng nghiệp vụ đầy đủ qua HTTP pipeline (auth → controller → service → DB/Redis).
    await using var factory = new SeatTestWebApplicationFactory();
    await SeedShowtimeWithAvailableSeatAsync(factory);

    var customerToken = GenerateCustomerAccessToken("USR_E2E_CUSTOMER");
    using var client = factory.CreateClient();

    // Bước 1: Lấy sơ đồ ghế — tìm ghế trống.
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", customerToken);
    var mapResponse = await client.GetAsync("/api/seats/showtimes/SHW_E2E_001/map");
    Assert.Equal(HttpStatusCode.OK, mapResponse.StatusCode);

    var mapBody = await DeserializeAsync<ApiResponse<SeatMapResponse>>(mapResponse);
    Assert.True(mapBody!.Success);
    var availableSeat = Assert.Single(mapBody.Data!.AvailableSeats);
    Assert.Equal("AVAILABLE", availableSeat.SeatStatus);

    var lockPayload = new LockSeatRequest
    {
      ShowtimeId = "SHW_E2E_001",
      SeatId = availableSeat.SeatId
    };

    // Bước 2: Khóa ghế trống — HTTP 200.
    var firstLockResponse = await client.PostAsJsonAsync("/api/seats/lock", lockPayload);
    Assert.Equal(HttpStatusCode.OK, firstLockResponse.StatusCode);

    var firstLockBody = await DeserializeAsync<ApiResponse<LockSeatResponse>>(firstLockResponse);
    Assert.True(firstLockBody!.Success);
    Assert.Equal("LOCKED", firstLockBody.Data!.SeatStatus);

    // Bước 3: Khóa lại cùng ghế — conflict HTTP 409.
    var secondLockResponse = await client.PostAsJsonAsync("/api/seats/lock", lockPayload);
    Assert.Equal(HttpStatusCode.Conflict, secondLockResponse.StatusCode);

    var secondLockBody = await DeserializeAsync<ApiResponse<LockSeatResponse>>(secondLockResponse);
    Assert.False(secondLockBody!.Success);
    Assert.Equal("SEAT_LOCKED", secondLockBody.ErrorCode);

    // Bước 4: Task.Delay mô phỏng chờ Redis TTL hết hạn (ShortTtlSeatLockStore = 300ms trong test host).
    // SeatService production dùng SeatLockTtl = 10 phút cho LockedUntil trong DB,
    // nên test fast-forward LockedUntil để mô phỏng hết TTL DB song song với Redis.
    await Task.Delay(400);
    await ExpireSeatLockInDatabaseAsync(factory, lockPayload.ShowtimeId, lockPayload.SeatId);

    // Bước 5: Gọi lại sơ đồ ghế — ghế phải về trạng thái Trống (AVAILABLE).
    var mapAfterTtlResponse = await client.GetAsync("/api/seats/showtimes/SHW_E2E_001/map");
    Assert.Equal(HttpStatusCode.OK, mapAfterTtlResponse.StatusCode);

    var mapAfterTtlBody = await DeserializeAsync<ApiResponse<SeatMapResponse>>(mapAfterTtlResponse);
    Assert.True(mapAfterTtlBody!.Success);
    Assert.Empty(mapAfterTtlBody.Data!.LockedSeats);

    var releasedSeat = Assert.Single(mapAfterTtlBody.Data.AvailableSeats);
    Assert.Equal(lockPayload.SeatId, releasedSeat.SeatId);
    Assert.Equal("AVAILABLE", releasedSeat.SeatStatus);
  }

  [Fact]
  public async Task GetSeatMap_InvalidShowtimeId_ReturnsNotFound()
  {
    // Coverage E2E: API trả 404 khi showtime không tồn tại.
    await using var factory = new SeatTestWebApplicationFactory();
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", GenerateCustomerAccessToken("USR_E2E_CUSTOMER"));

    var response = await client.GetAsync("/api/seats/showtimes/SHW_MISSING/map");

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    var body = await DeserializeAsync<ApiResponse<SeatMapResponse>>(response);
    Assert.False(body!.Success);
    Assert.Equal("SHOWTIME_NOT_FOUND", body.ErrorCode);
  }

  [Fact]
  public async Task LockSeat_WithoutToken_ReturnsUnauthorized()
  {
    // Coverage E2E: policy CanBookTicket yêu cầu JWT hợp lệ.
    await using var factory = new SeatTestWebApplicationFactory();
    await SeedShowtimeWithAvailableSeatAsync(factory);
    using var client = factory.CreateClient();

    var response = await client.PostAsJsonAsync(
      "/api/seats/lock",
      new LockSeatRequest { ShowtimeId = "SHW_E2E_001", SeatId = "SEAT_E2E_01" });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task LockSeat_ValidationMissingSeatId_ReturnsBadRequest()
  {
    // [Điểm mù - API Contract] Body thiếu SeatId — model validation trả 400 VALIDATION_ERROR.
    await using var factory = new SeatTestWebApplicationFactory();
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", GenerateCustomerAccessToken("USR_E2E_CUSTOMER"));

    var response = await client.PostAsJsonAsync(
      "/api/seats/lock",
      new { ShowtimeId = "SHW_E2E_001" });

    Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    var body = await DeserializeAsync<ApiResponse<object>>(response);
    Assert.False(body!.Success);
    Assert.Equal("VALIDATION_ERROR", body.ErrorCode);
  }

  [Fact]
  public async Task LockSeat_ExpiredJwt_ReturnsUnauthorized()
  {
    // [Điểm mù - Security] JWT đã hết hạn — middleware từ chối trước khi vào service.
    await using var factory = new SeatTestWebApplicationFactory();
    await SeedShowtimeWithAvailableSeatAsync(factory);
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", GenerateExpiredAccessToken("USR_E2E_CUSTOMER"));

    var response = await client.PostAsJsonAsync(
      "/api/seats/lock",
      new LockSeatRequest { ShowtimeId = "SHW_E2E_001", SeatId = "SEAT_E2E_01" });

    Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
  }

  [Fact]
  public async Task LockSeat_StaffRole_ReturnsForbidden()
  {
    // [Điểm mù - Security/Authorization] Staff có CanSelectSeat (xem map) nhưng KHÔNG có CanBookTicket (lock).
    await using var factory = new SeatTestWebApplicationFactory();
    await SeedShowtimeWithAvailableSeatAsync(factory);
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", GenerateAccessTokenForRole("USR_STAFF", AuthConstants.Roles.Staff));

    var response = await client.PostAsJsonAsync(
      "/api/seats/lock",
      new LockSeatRequest { ShowtimeId = "SHW_E2E_001", SeatId = "SEAT_E2E_01" });

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
  }

  [Fact]
  public async Task GetSeatMap_StaffRole_CanViewMap()
  {
    // [Điểm mù - Security] Staff được phép xem sơ đồ ghế qua policy CanSelectSeat.
    await using var factory = new SeatTestWebApplicationFactory();
    await SeedShowtimeWithAvailableSeatAsync(factory);
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", GenerateAccessTokenForRole("USR_STAFF", AuthConstants.Roles.Staff));

    var response = await client.GetAsync("/api/seats/showtimes/SHW_E2E_001/map");

    Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    var body = await DeserializeAsync<ApiResponse<SeatMapResponse>>(response);
    Assert.True(body!.Success);
    Assert.NotEmpty(body.Data!.AvailableSeats);
  }

  [Fact]
  public async Task LockSeat_SoldSeat_ReturnsConflict()
  {
    // [Điểm mù - Nghiệp vụ] Khóa ghế đã BOOKED qua API — HTTP 409 SEAT_SOLD.
    await using var factory = new SeatTestWebApplicationFactory();
    await SeedSoldSeatAsync(factory);
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", GenerateCustomerAccessToken("USR_E2E_CUSTOMER"));

    var response = await client.PostAsJsonAsync(
      "/api/seats/lock",
      new LockSeatRequest { ShowtimeId = "SHW_E2E_001", SeatId = "SEAT_E2E_01" });

    Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    var body = await DeserializeAsync<ApiResponse<LockSeatResponse>>(response);
    Assert.False(body!.Success);
    Assert.Equal("SEAT_SOLD", body.ErrorCode);
  }

  [Fact]
  public async Task LockSeat_InvalidSeatId_ReturnsNotFound()
  {
    // [Điểm mù - Validation] SeatId không thuộc showtime — HTTP 404 SHOWTIME_SEAT_NOT_FOUND.
    await using var factory = new SeatTestWebApplicationFactory();
    await SeedShowtimeWithAvailableSeatAsync(factory);
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", GenerateCustomerAccessToken("USR_E2E_CUSTOMER"));

    var response = await client.PostAsJsonAsync(
      "/api/seats/lock",
      new LockSeatRequest { ShowtimeId = "SHW_E2E_001", SeatId = "SEAT_GHOST" });

    Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    var body = await DeserializeAsync<ApiResponse<LockSeatResponse>>(response);
    Assert.Equal("SHOWTIME_SEAT_NOT_FOUND", body!.ErrorCode);
  }

  [Fact]
  public async Task LockSeat_ParallelClients_OnlyOneSucceeds()
  {
    // [Điểm mù - Race Condition] Hai client HTTP lock cùng ghế song song — chỉ 1 HTTP 200, còn lại 409.
    await using var factory = new SeatTestWebApplicationFactory();
    await SeedShowtimeWithAvailableSeatAsync(factory);

    var payload = new LockSeatRequest { ShowtimeId = "SHW_E2E_001", SeatId = "SEAT_E2E_01" };
    var lockTasks = Enumerable.Range(0, 5).Select(async index =>
    {
      var client = factory.CreateClient();
      client.DefaultRequestHeaders.Authorization =
        new AuthenticationHeaderValue("Bearer", GenerateCustomerAccessToken($"USR_PARALLEL_{index}"));
      return await client.PostAsJsonAsync("/api/seats/lock", payload);
    });

    var responses = await Task.WhenAll(lockTasks);

    Assert.Equal(1, responses.Count(item => item.StatusCode == HttpStatusCode.OK));
    Assert.Equal(4, responses.Count(item => item.StatusCode == HttpStatusCode.Conflict));
  }

  [Fact]
  public async Task LockSeat_RapidSequentialAttempts_OnlyFirstSucceeds()
  {
    // [Điểm mù - Abuse/Script] Mô phỏng script gọi lock liên tiếp nhanh — chỉ lần đầu 200, các lần sau 409.
    await using var factory = new SeatTestWebApplicationFactory();
    await SeedShowtimeWithAvailableSeatAsync(factory);
    using var client = factory.CreateClient();
    client.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", GenerateCustomerAccessToken("USR_SCRIPT_BOT"));

    var payload = new LockSeatRequest { ShowtimeId = "SHW_E2E_001", SeatId = "SEAT_E2E_01" };
    var statusCodes = new List<HttpStatusCode>();

    for (var attempt = 0; attempt < 5; attempt++)
    {
      var response = await client.PostAsJsonAsync("/api/seats/lock", payload);
      statusCodes.Add(response.StatusCode);
    }

    Assert.Equal(HttpStatusCode.OK, statusCodes[0]);
    Assert.All(statusCodes.Skip(1), code => Assert.Equal(HttpStatusCode.Conflict, code));
  }

  [Fact]
  public async Task GetSeatMap_AfterLockByAnotherUser_ShowsSeatInLockedList()
  {
    // [Điểm mù - Consistency] Sau khi user A lock, user B xem map — ghế phải nằm trong LockedSeats.
    await using var factory = new SeatTestWebApplicationFactory();
    await SeedShowtimeWithAvailableSeatAsync(factory);

    using var clientA = factory.CreateClient();
    clientA.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", GenerateCustomerAccessToken("USR_A"));

    var lockResponse = await clientA.PostAsJsonAsync(
      "/api/seats/lock",
      new LockSeatRequest { ShowtimeId = "SHW_E2E_001", SeatId = "SEAT_E2E_01" });
    Assert.Equal(HttpStatusCode.OK, lockResponse.StatusCode);

    using var clientB = factory.CreateClient();
    clientB.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", GenerateCustomerAccessToken("USR_B"));

    var mapResponse = await clientB.GetAsync("/api/seats/showtimes/SHW_E2E_001/map");
    Assert.Equal(HttpStatusCode.OK, mapResponse.StatusCode);

    var mapBody = await DeserializeAsync<ApiResponse<SeatMapResponse>>(mapResponse);
    Assert.True(mapBody!.Success);
    Assert.Empty(mapBody.Data!.AvailableSeats);
    var lockedSeat = Assert.Single(mapBody.Data.LockedSeats);
    Assert.Equal("SEAT_E2E_01", lockedSeat.SeatId);
    Assert.Equal("LOCKED", lockedSeat.SeatStatus);
  }

  [Fact]
  public async Task GetSeatMap_ConcurrentWithLock_ReflectsConsistentState()
  {
    // [Điểm mù - Concurrency/Consistency] Lock và GetMap chạy song song — không có ghế vừa AVAILABLE vừa LOCKED.
    await using var factory = new SeatTestWebApplicationFactory();
    await SeedShowtimeWithAvailableSeatAsync(factory);

    using var lockClient = factory.CreateClient();
    lockClient.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", GenerateCustomerAccessToken("USR_LOCK"));

    var payload = new LockSeatRequest { ShowtimeId = "SHW_E2E_001", SeatId = "SEAT_E2E_01" };

    var lockTask = lockClient.PostAsJsonAsync("/api/seats/lock", payload);
    using var mapClient = factory.CreateClient();
    mapClient.DefaultRequestHeaders.Authorization =
      new AuthenticationHeaderValue("Bearer", GenerateCustomerAccessToken("USR_MAP"));

    var mapTask = mapClient.GetAsync("/api/seats/showtimes/SHW_E2E_001/map");
    await Task.WhenAll(lockTask, mapTask);

    var mapBody = await DeserializeAsync<ApiResponse<SeatMapResponse>>(await mapTask);
    Assert.True(mapBody!.Success);

    var seatInAvailable = mapBody.Data!.AvailableSeats.Any(item => item.SeatId == "SEAT_E2E_01");
    var seatInLocked = mapBody.Data.LockedSeats.Any(item => item.SeatId == "SEAT_E2E_01");
    Assert.False(seatInAvailable && seatInLocked);
    Assert.True(seatInAvailable || seatInLocked);
  }

  private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
  {
    var json = await response.Content.ReadAsStringAsync();
    return JsonSerializer.Deserialize<T>(json, JsonOptions);
  }

  private static string GenerateCustomerAccessToken(string userId)
  {
    return GenerateAccessTokenForRole(userId, AuthConstants.Roles.Customer);
  }

  private static string GenerateAccessTokenForRole(string userId, string role)
  {
    var jwtOptions = Options.Create(new JwtSettings
    {
      Issuer = CinemaWebApplicationFactory.TestJwtIssuer,
      Audience = CinemaWebApplicationFactory.TestJwtAudience,
      Secret = CinemaWebApplicationFactory.TestJwtSecret,
      AccessTokenMinutes = 15,
      RefreshTokenDays = 7
    });
    var tokenService = new JwtTokenService(jwtOptions, new SystemClock());
    return tokenService.GenerateAccessToken(userId, "customer-e2e@example.com", role).AccessToken;
  }

  private static string GenerateExpiredAccessToken(string userId)
  {
    var jwtOptions = Options.Create(new JwtSettings
    {
      Issuer = CinemaWebApplicationFactory.TestJwtIssuer,
      Audience = CinemaWebApplicationFactory.TestJwtAudience,
      Secret = CinemaWebApplicationFactory.TestJwtSecret,
      AccessTokenMinutes = 15,
      RefreshTokenDays = 7
    });
    var expiredClock = new FakeClock(DateTime.UtcNow.AddHours(-2));
    var tokenService = new JwtTokenService(jwtOptions, expiredClock);
    return tokenService.GenerateAccessToken(userId, "customer-e2e@example.com", AuthConstants.Roles.Customer)
      .AccessToken;
  }

  private static async Task SeedShowtimeWithAvailableSeatAsync(SeatTestWebApplicationFactory factory)
  {
    await using var scope = factory.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

    dbContext.Cinemas.Add(new Cinema
    {
      CinemaId = "CIN_E2E",
      CinemaName = "E2E Cinema",
      Address = "1 E2E Street",
      City = "HCM",
      CinemaStatus = "ACTIVE"
    });

    dbContext.Movies.Add(new Movie
    {
      MovieId = "MOV_E2E",
      Title = "E2E Movie",
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
      RoomId = "ROOM_E2E",
      CinemaId = "CIN_E2E",
      RoomName = "E2E Room",
      Capacity = 1,
      RoomStatus = "ACTIVE"
    });

    dbContext.Seats.Add(new Seat
    {
      SeatId = "SEAT_E2E_01",
      RoomId = "ROOM_E2E",
      SeatTypeId = "SEAT_TYPE_STANDARD",
      SeatCode = "A1",
      RowLabel = "A",
      SeatNumber = 1,
      IsActive = true
    });

    dbContext.Showtimes.Add(new Showtime
    {
      ShowtimeId = "SHW_E2E_001",
      MovieId = "MOV_E2E",
      RoomId = "ROOM_E2E",
      StartTime = DateTime.UtcNow.AddHours(2),
      EndTime = DateTime.UtcNow.AddHours(4),
      BasePrice = 90000,
      Status = "OPEN",
      CreatedAt = DateTime.UtcNow
    });

    dbContext.ShowtimeSeats.Add(new ShowtimeSeat
    {
      ShowtimeSeatId = "STS_E2E_001",
      ShowtimeId = "SHW_E2E_001",
      SeatId = "SEAT_E2E_01",
      SeatStatus = "AVAILABLE",
      RowVersion = new byte[8]
    });

    await dbContext.SaveChangesAsync();
  }

  private static async Task SeedSoldSeatAsync(SeatTestWebApplicationFactory factory)
  {
    await SeedShowtimeWithAvailableSeatAsync(factory);

    await using var scope = factory.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
    var showtimeSeat = await dbContext.ShowtimeSeats.SingleAsync(item => item.SeatId == "SEAT_E2E_01");
    showtimeSeat.SeatStatus = "BOOKED";
    await dbContext.SaveChangesAsync();
  }

  private static async Task ExpireSeatLockInDatabaseAsync(
    SeatTestWebApplicationFactory factory,
    string showtimeId,
    string seatId)
  {
    await using var scope = factory.Services.CreateAsyncScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
    var showtimeSeat = await dbContext.ShowtimeSeats
      .SingleAsync(item => item.ShowtimeId == showtimeId && item.SeatId == seatId);

    showtimeSeat.LockedUntil = DateTime.UtcNow.AddSeconds(-1);
    await dbContext.SaveChangesAsync();
  }

  /// <summary>
  /// Test host thay InMemory DB và ShortTtl Redis lock store để E2E không phụ thuộc SQL Server/Redis thật.
  /// </summary>
  private sealed class SeatTestWebApplicationFactory : WebApplicationFactory<Program>
  {
    private readonly string _databaseName = Guid.NewGuid().ToString("N");
    private readonly ShortTtlSeatLockStore _seatLockStore = new(TimeSpan.FromMilliseconds(300));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
      CinemaWebApplicationFactory.ConfigureRequiredSettings(builder);

      builder.ConfigureTestServices(services =>
      {
        services.AddDataProtection()
          .UseEphemeralDataProtectionProvider();

        services.RemoveAll<DbContextOptions<CinemaDbContext>>();
        services.RemoveAll<CinemaDbContext>();

        services.AddDbContext<CinemaDbContext>(options =>
          options.UseInMemoryDatabase(_databaseName));

        services.RemoveAll<ISeatLockStore>();
        services.AddSingleton<ISeatLockStore>(_seatLockStore);
      });
    }
  }

  /// <summary>
  /// Giới hạn TTL lock ở mức thấp để Task.Delay trong test mô phỏng hết hạn Redis nhanh.
  /// Logic gốc: CinemaSystem.Infrastructure/Services/InMemorySeatLockStore.cs
  /// </summary>
  private sealed class ShortTtlSeatLockStore : ISeatLockStore
  {
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, LockEntry> _locks = new();
    private readonly TimeSpan _testTtlCap;

    public ShortTtlSeatLockStore(TimeSpan testTtlCap)
    {
      _testTtlCap = testTtlCap;
    }

    public Task<bool> TryLockAsync(
      string lockKey,
      string userId,
      TimeSpan ttl,
      CancellationToken cancellationToken)
    {
      var effectiveTtl = ttl < _testTtlCap ? ttl : _testTtlCap;
      var now = DateTime.UtcNow;
      var entry = new LockEntry(userId, now.Add(effectiveTtl));

      while (true)
      {
        if (!_locks.TryGetValue(lockKey, out var existing))
        {
          if (_locks.TryAdd(lockKey, entry))
          {
            return Task.FromResult(true);
          }

          continue;
        }

        if (existing.ExpiresAt > now)
        {
          return Task.FromResult(false);
        }

        if (_locks.TryUpdate(lockKey, entry, existing))
        {
          return Task.FromResult(true);
        }
      }
    }

    public Task ReleaseAsync(string lockKey, CancellationToken cancellationToken)
    {
      _locks.TryRemove(lockKey, out _);
      return Task.CompletedTask;
    }

    private sealed record LockEntry(string UserId, DateTime ExpiresAt);
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
