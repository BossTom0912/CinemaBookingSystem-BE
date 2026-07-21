using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Tickets;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

public sealed class TicketScanApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Scan_StaffAssignedCinema_ChecksInAndWritesActorLog()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Staff());
        var response = await ScanAsync(client, TicketA.QrCode, RoomA.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ScanTicketResponse>>(response);
        Assert.Equal(TicketA.Id, body!.Data!.TicketId);
        Assert.Equal(BookingConstants.TicketStatus.CheckedIn, body.Data.TicketStatus);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var ticket = await db.Tickets.SingleAsync(item => item.TicketId == TicketA.Id);
        var log = await db.CheckinLogs.SingleAsync();

        Assert.Equal(BookingConstants.TicketStatus.CheckedIn, ticket.TicketStatus);
        Assert.Equal(BookingConstants.CheckInResult.Success, log.Result);
        Assert.Equal(TestUsers.Staff, log.ScannedByUserId);
        Assert.NotNull(log.StaffProfileId);
    }

    [Fact]
    public async Task Scan_ManagerAssignedCinema_ChecksInTicket()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Manager());
        var response = await ScanAsync(client, TicketA.QrCode, RoomA.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Scan_AdminOtherCinema_BypassesScopeAndDoesNotRequireStaffProfile()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Admin());
        var response = await ScanAsync(client, TicketB.QrCode, RoomB.Id);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var log = await db.CheckinLogs.SingleAsync();
        Assert.Equal(TestUsers.Admin, log.ScannedByUserId);
        Assert.Null(log.StaffProfileId);
    }

    [Fact]
    public async Task Scan_ManagerOtherCinema_ReturnsForbiddenAndWritesFailedLog()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Manager());
        var response = await ScanAsync(client, TicketB.QrCode, RoomB.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal(
            BookingConstants.TicketScanErrorCodes.TicketWrongCinema,
            body!.ErrorCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var log = await db.CheckinLogs.SingleAsync();
        Assert.Equal(BookingConstants.CheckInResult.Failed, log.Result);
        Assert.Equal(FailureReasons.WrongCinema, log.FailureReason);
    }

    [Fact]
    public async Task Scan_WrongRoom_ReturnsConflictAndWritesFailedLog()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Manager());
        var response = await ScanAsync(client, TicketA.QrCode, RoomB.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal(
            BookingConstants.TicketScanErrorCodes.TicketWrongRoom,
            body!.ErrorCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var log = await db.CheckinLogs.SingleAsync();
        Assert.Equal(BookingConstants.CheckInResult.Failed, log.Result);
        Assert.Equal(FailureReasons.WrongRoom, log.FailureReason);
    }

    [Fact]
    public async Task Scan_SameTicketTwice_SecondScanIsRejectedAndBothAttemptsAreLogged()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Manager());
        var first = await ScanAsync(client, TicketA.QrCode, RoomA.Id);
        var second = await ScanAsync(client, TicketA.QrCode, RoomA.Id);

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(second);
        Assert.Equal(
            BookingConstants.TicketScanErrorCodes.TicketAlreadyCheckedIn,
            body!.ErrorCode);
        Assert.Equal("Ticket has already been used.", body.Message);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        Assert.Equal(2, await db.CheckinLogs.CountAsync());
        Assert.Equal(
            1,
            await db.CheckinLogs.CountAsync(
                log => log.Result == BookingConstants.CheckInResult.Success));
        Assert.Equal(
            1,
            await db.CheckinLogs.CountAsync(
                log => log.Result == BookingConstants.CheckInResult.Failed
                    && log.FailureReason == FailureReasons.TicketAlreadyUsed));
    }

    [Fact]
    public async Task Scan_CancelledTicket_ReturnsConflictAndWritesFailedLog()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);
        await SetTicketStatusAsync(
            factory,
            TicketA.Id,
            BookingConstants.TicketStatus.Cancelled);

        using var client = CreateClient(factory, TestAuthTokens.Manager());
        var response = await ScanAsync(client, TicketA.QrCode, RoomA.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal(
            BookingConstants.TicketScanErrorCodes.TicketCancelled,
            body!.ErrorCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var log = await db.CheckinLogs.SingleAsync();
        Assert.Equal(BookingConstants.CheckInResult.Failed, log.Result);
        Assert.Equal(FailureReasons.TicketCancelled, log.FailureReason);
    }

    [Fact]
    public async Task Scan_RefundedTicket_ReturnsConflictAndWritesFailedLog()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);
        await SetTicketStatusAsync(
            factory,
            TicketA.Id,
            BookingConstants.TicketStatus.Refunded);

        using var client = CreateClient(factory, TestAuthTokens.Manager());
        var response = await ScanAsync(client, TicketA.QrCode, RoomA.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal(
            BookingConstants.TicketScanErrorCodes.TicketRefunded,
            body!.ErrorCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var log = await db.CheckinLogs.SingleAsync();
        Assert.Equal(BookingConstants.CheckInResult.Failed, log.Result);
        Assert.Equal(FailureReasons.TicketRefunded, log.FailureReason);
    }

    [Fact]
    public async Task Scan_CancelledShowtime_ReturnsConflict()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);
        await SetShowtimeStatusAsync(
            factory,
            TicketA.ShowtimeId,
            BookingConstants.ShowtimeStatus.Cancelled);

        using var client = CreateClient(factory, TestAuthTokens.Manager());
        var response = await ScanAsync(client, TicketA.QrCode, RoomA.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal(
            BookingConstants.TicketScanErrorCodes.ShowtimeCancelled,
            body!.ErrorCode);
    }

    [Fact]
    public async Task Scan_UnknownQr_ReturnsNotFoundAndWritesFailedLog()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Manager());
        var response = await ScanAsync(client, UnknownQrCode, RoomA.Id);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var log = await db.CheckinLogs.SingleAsync();
        Assert.Null(log.TicketId);
        Assert.Equal(FailureReasons.TicketNotFound, log.FailureReason);
    }

    [Fact]
    public async Task Scan_BeforeConfiguredWindow_ReturnsConflictAndWritesFailedLog()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Manager());
        var response = await ScanAsync(client, TicketEarly.QrCode, RoomA.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal(
            BookingConstants.TicketScanErrorCodes.CheckInTooEarly,
            body!.ErrorCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var log = await db.CheckinLogs.SingleAsync();
        Assert.Equal(BookingConstants.CheckInResult.Failed, log.Result);
        Assert.Equal(FailureReasons.InvalidTime, log.FailureReason);
    }

    [Fact]
    public async Task Scan_AfterShowtimeEnds_ReturnsConflictAndWritesFailedLog()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Manager());
        var response = await ScanAsync(client, TicketLate.QrCode, RoomA.Id);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal(
            BookingConstants.TicketScanErrorCodes.CheckInWindowClosed,
            body!.ErrorCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var log = await db.CheckinLogs.SingleAsync();
        Assert.Equal(BookingConstants.CheckInResult.Failed, log.Result);
        Assert.Equal(FailureReasons.InvalidTime, log.FailureReason);
    }

    [Fact]
    public async Task Scan_Customer_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Customer());
        var response = await ScanAsync(client, TicketA.QrCode, RoomA.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Scan_TokenRoleDoesNotMatchDatabaseRole_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAsync(factory);
        await SetUserRoleAsync(
            factory,
            TestUsers.Manager,
            AuthConstants.RoleIds.Admin);

        using var client = CreateClient(factory, TestAuthTokens.Manager());
        var response = await ScanAsync(client, TicketA.QrCode, RoomA.Id);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal(
            BookingConstants.TicketScanErrorCodes.ScanActorRoleForbidden,
            body!.ErrorCode);
    }

    private static HttpClient CreateClient(
        CinemaWebApplicationFactory factory,
        string accessToken)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    private static Task<HttpResponseMessage> ScanAsync(
        HttpClient client,
        string qrCode,
        string roomId)
    {
        return client.PostAsJsonAsync(
            "/api/tickets/scan",
            new ScanTicketRequest
            {
                QrCode = qrCode,
                RoomId = roomId
            });
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }

    private static async Task SetTicketStatusAsync(
        CinemaWebApplicationFactory factory,
        string ticketId,
        string status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var ticket = await db.Tickets.SingleAsync(item => item.TicketId == ticketId);
        ticket.TicketStatus = status;
        await db.SaveChangesAsync();
    }

    private static async Task SetShowtimeStatusAsync(
        CinemaWebApplicationFactory factory,
        string showtimeId,
        string status)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var showtime = await db.Showtimes.SingleAsync(
            item => item.ShowtimeId == showtimeId);
        showtime.Status = status;
        await db.SaveChangesAsync();
    }

    private static async Task SetUserRoleAsync(
        CinemaWebApplicationFactory factory,
        string userId,
        string roleId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var user = await db.Users.SingleAsync(item => item.UserId == userId);
        user.RoleId = roleId;
        await db.SaveChangesAsync();
    }

    private static async Task SeedAsync(CinemaWebApplicationFactory factory)
    {
        var now = DateTime.UtcNow;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

            db.Cinemas.AddRange(
                NewCinema(CinemaA.Id, CinemaA.Name),
                NewCinema(CinemaB.Id, CinemaB.Name));
            db.Rooms.AddRange(
                NewRoom(RoomA.Id, CinemaA.Id, RoomA.Name),
                NewRoom(RoomB.Id, CinemaB.Id, RoomB.Name));
            db.Movies.Add(new Movie
            {
                MovieId = MovieId,
                Title = MovieTitle,
                DurationMinutes = MovieDurationMinutes,
                MovieStatus = DomainConstants.MovieStatus.NowShowing
            });
            db.SeatTypes.Add(new SeatType
            {
                SeatTypeId = SeatTypeId,
                TypeName = DomainConstants.SeatType.StandardName,
                ExtraFee = decimal.Zero
            });
            db.Seats.AddRange(
                NewSeat(SeatA.Id, RoomA.Id, SeatA.Code),
                NewSeat(SeatB.Id, RoomB.Id, SeatB.Code),
                NewSeat(SeatEarly.Id, RoomA.Id, SeatEarly.Code),
                NewSeat(SeatLate.Id, RoomA.Id, SeatLate.Code));

            AddTicketGraph(
                db,
                TicketA,
                RoomA.Id,
                SeatA.Id,
                now.AddMinutes(ScanWindow.CurrentShowtimeStartOffsetMinutes),
                now.AddMinutes(ScanWindow.CurrentShowtimeEndOffsetMinutes));
            AddTicketGraph(
                db,
                TicketB,
                RoomB.Id,
                SeatB.Id,
                now.AddMinutes(ScanWindow.CurrentShowtimeStartOffsetMinutes),
                now.AddMinutes(ScanWindow.CurrentShowtimeEndOffsetMinutes));
            AddTicketGraph(
                db,
                TicketEarly,
                RoomA.Id,
                SeatEarly.Id,
                now.AddMinutes(ScanWindow.EarlyShowtimeStartOffsetMinutes),
                now.AddMinutes(ScanWindow.EarlyShowtimeEndOffsetMinutes));
            AddTicketGraph(
                db,
                TicketLate,
                RoomA.Id,
                SeatLate.Id,
                now.AddMinutes(ScanWindow.LateShowtimeStartOffsetMinutes),
                now.AddMinutes(ScanWindow.LateShowtimeEndOffsetMinutes));

            db.Roles.Add(new Role
            {
                RoleId = AuthConstants.RoleIds.Admin,
                RoleName = AuthConstants.Roles.Admin
            });
            db.Users.Add(new User
            {
                UserId = TestUsers.Admin,
                RoleId = AuthConstants.RoleIds.Admin,
                Email = "ticket-admin@test.com",
                PasswordHash = "TEST_HASH",
                FullName = "Ticket Admin",
                Status = AuthConstants.UserStatus.Active,
                EmailVerified = true,
                CreatedAt = now
            });

            await db.SaveChangesAsync();
        }

        await CinemaScopeTestData.SeedManagerScopeAsync(
            factory,
            CinemaA.Id,
            TestUsers.Manager);
        await CinemaScopeTestData.SeedStaffScopeAsync(
            factory,
            CinemaA.Id,
            TestUsers.Staff);
    }

    private static void AddTicketGraph(
        CinemaDbContext db,
        TicketFixture ticket,
        string roomId,
        string seatId,
        DateTime startTime,
        DateTime endTime)
    {
        db.Showtimes.Add(new Showtime
        {
            ShowtimeId = ticket.ShowtimeId,
            MovieId = MovieId,
            RoomId = roomId,
            StartTime = startTime,
            EndTime = endTime,
            BasePrice = TicketPrice,
            Status = BookingConstants.ShowtimeStatus.Open,
            CreatedAt = startTime.AddDays(-1)
        });
        db.ShowtimeSeats.Add(new ShowtimeSeat
        {
            ShowtimeSeatId = ticket.ShowtimeSeatId,
            ShowtimeId = ticket.ShowtimeId,
            SeatId = seatId,
            SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked,
            RowVersion = []
        });
        db.Bookings.Add(new Booking
        {
            BookingId = ticket.BookingId,
            ShowtimeId = ticket.ShowtimeId,
            BookingStatus = BookingConstants.BookingStatus.Paid,
            BookingChannel = BookingConstants.BookingChannel.Online,
            TotalAmount = TicketPrice,
            CreatedAt = startTime.AddDays(-1)
        });
        db.BookingSeats.Add(new BookingSeat
        {
            BookingSeatId = ticket.BookingSeatId,
            BookingId = ticket.BookingId,
            ShowtimeSeatId = ticket.ShowtimeSeatId,
            SeatPrice = TicketPrice
        });
        db.Tickets.Add(new Ticket
        {
            TicketId = ticket.Id,
            BookingSeatId = ticket.BookingSeatId,
            QrCode = ticket.QrCode,
            TicketStatus = BookingConstants.TicketStatus.Unused,
            GeneratedAt = startTime.AddDays(-1)
        });
    }

    private static Cinema NewCinema(string cinemaId, string name)
    {
        return new Cinema
        {
            CinemaId = cinemaId,
            CinemaName = name,
            Address = name,
            City = TestCity,
            CinemaStatus = DomainConstants.CinemaStatus.Active
        };
    }

    private static Room NewRoom(string roomId, string cinemaId, string name)
    {
        return new Room
        {
            RoomId = roomId,
            CinemaId = cinemaId,
            RoomName = name,
            Capacity = RoomCapacity,
            RoomStatus = DomainConstants.RoomStatus.Active
        };
    }

    private static Seat NewSeat(string seatId, string roomId, string seatCode)
    {
        return new Seat
        {
            SeatId = seatId,
            RoomId = roomId,
            SeatTypeId = SeatTypeId,
            SeatCode = seatCode,
            RowLabel = SeatRow,
            SeatNumber = 1,
            IsActive = true
        };
    }

    private static class TestUsers
    {
        public const string Staff = "USR_TEST_STAFF";
        public const string Manager = "USR_TEST_MANAGER";
        public const string Admin = "USR_TEST_ADMIN";
    }

    private static class ScanWindow
    {
        public const int CurrentShowtimeStartOffsetMinutes = 10;
        public const int CurrentShowtimeEndOffsetMinutes = 120;
        public const int EarlyShowtimeStartOffsetMinutes = 120;
        public const int EarlyShowtimeEndOffsetMinutes = 240;
        public const int LateShowtimeStartOffsetMinutes = -180;
        public const int LateShowtimeEndOffsetMinutes = -60;
    }

    private static class FailureReasons
    {
        public const string TicketNotFound = "Ticket Not Found";
        public const string WrongCinema = "Wrong Cinema";
        public const string WrongRoom = "Wrong Room";
        public const string TicketAlreadyUsed = "Ticket Already Used";
        public const string TicketCancelled = "Ticket Cancelled";
        public const string TicketRefunded = "Ticket Refunded";
        public const string InvalidTime = "Invalid Time";
    }

    private static readonly TicketFixture TicketA =
        new("TCK_SCAN_A", "QR_SCAN_A", "BOK_SCAN_A", "BKS_SCAN_A", "STS_SCAN_A", "SHW_SCAN_A");
    private static readonly TicketFixture TicketB =
        new("TCK_SCAN_B", "QR_SCAN_B", "BOK_SCAN_B", "BKS_SCAN_B", "STS_SCAN_B", "SHW_SCAN_B");
    private static readonly TicketFixture TicketEarly =
        new(
            "TCK_SCAN_EARLY",
            "QR_SCAN_EARLY",
            "BOK_SCAN_EARLY",
            "BKS_SCAN_EARLY",
            "STS_SCAN_EARLY",
            "SHW_SCAN_EARLY");
    private static readonly TicketFixture TicketLate =
        new(
            "TCK_SCAN_LATE",
            "QR_SCAN_LATE",
            "BOK_SCAN_LATE",
            "BKS_SCAN_LATE",
            "STS_SCAN_LATE",
            "SHW_SCAN_LATE");

    private static readonly (string Id, string Name) CinemaA = ("CIN_SCAN_A", "Scan Cinema A");
    private static readonly (string Id, string Name) CinemaB = ("CIN_SCAN_B", "Scan Cinema B");
    private static readonly (string Id, string Name) RoomA = ("ROOM_SCAN_A", "Scan Room A");
    private static readonly (string Id, string Name) RoomB = ("ROOM_SCAN_B", "Scan Room B");
    private static readonly (string Id, string Code) SeatA = ("SEAT_SCAN_A", "A1");
    private static readonly (string Id, string Code) SeatB = ("SEAT_SCAN_B", "B1");
    private static readonly (string Id, string Code) SeatEarly = ("SEAT_SCAN_EARLY", "A2");
    private static readonly (string Id, string Code) SeatLate = ("SEAT_SCAN_LATE", "A3");

    private const string MovieId = "MOV_SCAN";
    private const string MovieTitle = "Ticket Scan Movie";
    private const int MovieDurationMinutes = 110;
    private const string SeatTypeId = "SEAT_TYPE_SCAN";
    private const string SeatRow = "A";
    private const string TestCity = "HCM";
    private const string UnknownQrCode = "QR_SCAN_UNKNOWN";
    private const int RoomCapacity = 20;
    private const decimal TicketPrice = 100000m;

    private sealed record TicketFixture(
        string Id,
        string QrCode,
        string BookingId,
        string BookingSeatId,
        string ShowtimeSeatId,
        string ShowtimeId);
}
