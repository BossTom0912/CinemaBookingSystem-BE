using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Tickets;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

public sealed class TicketScanApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task ScanTicket_StaffAssignedCinema_ChecksInTicketAndWritesSuccessLog()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedTicketDataAsync(factory, "CIN_SCAN_A", "QR_SCAN_OK");
        await CinemaScopeTestData.SeedStaffScopeAsync(factory, "CIN_SCAN_A");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Staff());

        var response = await client.PostAsJsonAsync(
            "/api/staff/tickets/scan",
            new ScanTicketRequest { QrCode = "QR_SCAN_OK" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ScanTicketResponse>>(response);
        Assert.True(body!.Success);
        Assert.Equal("TCK_SCAN", body.Data!.TicketId);
        Assert.Equal(BookingConstants.TicketStatus.CheckedIn, body.Data.TicketStatus);
        Assert.Equal("Cinema Scan A", body.Data.CinemaName);
        Assert.Equal("A1", body.Data.SeatCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var ticket = await db.Tickets.SingleAsync(item => item.TicketId == "TCK_SCAN");
        Assert.Equal(BookingConstants.TicketStatus.CheckedIn, ticket.TicketStatus);

        var log = await db.CheckinLogs.SingleAsync();
        Assert.Equal("SUCCESS", log.Result);
        Assert.Equal("TCK_SCAN", log.TicketId);
        Assert.Equal("QR_SCAN_OK", log.RawQrCode);
        Assert.Null(log.FailureReason);
    }

    [Fact]
    public async Task ScanTicket_SecondScan_ReturnsConflictAndWritesFailedLog()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedTicketDataAsync(factory, "CIN_SCAN_A", "QR_SCAN_DUP");
        await CinemaScopeTestData.SeedStaffScopeAsync(factory, "CIN_SCAN_A");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Staff());

        var firstResponse = await client.PostAsJsonAsync(
            "/api/staff/tickets/scan",
            new ScanTicketRequest { QrCode = "QR_SCAN_DUP" });
        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);

        var secondResponse = await client.PostAsJsonAsync(
            "/api/staff/tickets/scan",
            new ScanTicketRequest { QrCode = "QR_SCAN_DUP" });

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(secondResponse);
        Assert.False(body!.Success);
        Assert.Equal(BookingConstants.ErrorCodes.TicketAlreadyCheckedIn, body.ErrorCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        Assert.Equal(BookingConstants.TicketStatus.CheckedIn,
            (await db.Tickets.SingleAsync(item => item.TicketId == "TCK_SCAN")).TicketStatus);
        Assert.Equal(1, await db.CheckinLogs.CountAsync(item => item.Result == "SUCCESS"));
        Assert.Equal(1, await db.CheckinLogs.CountAsync(item => item.Result == "FAILED"));
    }

    [Fact]
    public async Task ScanTicket_StaffOtherCinema_ReturnsForbiddenAndWritesFailedLog()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedTicketDataAsync(factory, "CIN_SCAN_B", "QR_SCAN_OTHER");
        await CinemaScopeTestData.SeedStaffScopeAsync(factory, "CIN_SCAN_A");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Staff());

        var response = await client.PostAsJsonAsync(
            "/api/staff/tickets/scan",
            new ScanTicketRequest { QrCode = "QR_SCAN_OTHER" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.False(body!.Success);
        Assert.Equal("CINEMA_SCOPE_FORBIDDEN", body.ErrorCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        Assert.Equal(BookingConstants.TicketStatus.Unused,
            (await db.Tickets.SingleAsync(item => item.TicketId == "TCK_SCAN")).TicketStatus);

        var log = await db.CheckinLogs.SingleAsync();
        Assert.Equal("FAILED", log.Result);
        Assert.Equal("TCK_SCAN", log.TicketId);
        Assert.Equal("QR_SCAN_OTHER", log.RawQrCode);
    }

    [Fact]
    public async Task ScanTicket_UnknownQr_ReturnsNotFoundAndWritesFailedLog()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedTicketDataAsync(factory, "CIN_SCAN_A", "QR_SCAN_REAL");
        await CinemaScopeTestData.SeedStaffScopeAsync(factory, "CIN_SCAN_A");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Staff());

        var response = await client.PostAsJsonAsync(
            "/api/staff/tickets/scan",
            new ScanTicketRequest { QrCode = "QR_SCAN_UNKNOWN" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.False(body!.Success);
        Assert.Equal(BookingConstants.ErrorCodes.TicketNotFound, body.ErrorCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var log = await db.CheckinLogs.SingleAsync();
        Assert.Equal("FAILED", log.Result);
        Assert.Null(log.TicketId);
        Assert.Equal("QR_SCAN_UNKNOWN", log.RawQrCode);
    }

    [Fact]
    public async Task ScanTicket_TooEarly_ReturnsConflictAndWritesFailedLog()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedTicketDataAsync(
            factory,
            "CIN_SCAN_A",
            "QR_SCAN_EARLY",
            startTime: DateTime.UtcNow.AddHours(2),
            endTime: DateTime.UtcNow.AddHours(4));
        await CinemaScopeTestData.SeedStaffScopeAsync(factory, "CIN_SCAN_A");

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Staff());

        var response = await client.PostAsJsonAsync(
            "/api/staff/tickets/scan",
            new ScanTicketRequest { QrCode = "QR_SCAN_EARLY" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.False(body!.Success);
        Assert.Equal(BookingConstants.ErrorCodes.CheckInTimeNotAllowed, body.ErrorCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        Assert.Equal(BookingConstants.TicketStatus.Unused,
            (await db.Tickets.SingleAsync(item => item.TicketId == "TCK_SCAN")).TicketStatus);
        Assert.Equal("FAILED", (await db.CheckinLogs.SingleAsync()).Result);
    }

    private static async Task SeedTicketDataAsync(
        CinemaWebApplicationFactory factory,
        string cinemaId,
        string qrCode,
        string ticketStatus = BookingConstants.TicketStatus.Unused,
        string bookingStatus = BookingConstants.BookingStatus.Paid,
        string showtimeStatus = BookingConstants.ShowtimeStatus.Open,
        DateTime? startTime = null,
        DateTime? endTime = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var now = DateTime.UtcNow;

        db.Roles.Add(new Role
        {
            RoleId = AuthConstants.RoleIds.Customer,
            RoleName = AuthConstants.Roles.Customer,
            Description = "Customer"
        });

        db.Users.Add(new User
        {
            UserId = "USR_SCAN_CUSTOMER",
            RoleId = AuthConstants.RoleIds.Customer,
            Email = "scan-customer@test.com",
            PasswordHash = "HASH",
            FullName = "Scan Customer",
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = now
        });

        db.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerProfileId = "CUS_SCAN",
            UserId = "USR_SCAN_CUSTOMER",
            MemberLevel = "BRONZE",
            RewardPoints = 0
        });

        db.Cinemas.Add(new Cinema
        {
            CinemaId = cinemaId,
            CinemaName = cinemaId.EndsWith("_B", StringComparison.OrdinalIgnoreCase)
                ? "Cinema Scan B"
                : "Cinema Scan A",
            Address = "Scan Address",
            City = "Scan City",
            CinemaStatus = BookingConstants.ResourceStatus.Active
        });

        db.Rooms.Add(new Room
        {
            RoomId = "ROM_SCAN",
            CinemaId = cinemaId,
            RoomName = "Room Scan",
            Capacity = 10,
            RoomStatus = BookingConstants.ResourceStatus.Active
        });

        db.SeatTypes.Add(new SeatType
        {
            SeatTypeId = "ST_SCAN",
            TypeName = "Standard",
            ExtraFee = 0
        });

        db.Seats.Add(new Seat
        {
            SeatId = "SEA_SCAN",
            RoomId = "ROM_SCAN",
            SeatTypeId = "ST_SCAN",
            SeatCode = "A1",
            RowLabel = "A",
            SeatNumber = 1,
            IsActive = true
        });

        db.Movies.Add(new Movie
        {
            MovieId = "MOV_SCAN",
            Title = "Scan Movie",
            DurationMinutes = 120,
            MovieStatus = "NOW_SHOWING"
        });

        db.Showtimes.Add(new Showtime
        {
            ShowtimeId = "SHW_SCAN",
            MovieId = "MOV_SCAN",
            RoomId = "ROM_SCAN",
            StartTime = startTime ?? now.AddMinutes(5),
            EndTime = endTime ?? now.AddHours(2),
            BasePrice = 100000m,
            Status = showtimeStatus,
            CreatedAt = now
        });

        db.ShowtimeSeats.Add(new ShowtimeSeat
        {
            ShowtimeSeatId = "STS_SCAN",
            ShowtimeId = "SHW_SCAN",
            SeatId = "SEA_SCAN",
            SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked,
            RowVersion = new byte[8]
        });

        db.Bookings.Add(new Booking
        {
            BookingId = "BKG_SCAN",
            CustomerProfileId = "CUS_SCAN",
            ShowtimeId = "SHW_SCAN",
            BookingStatus = bookingStatus,
            TotalAmount = 100000m,
            CreatedAt = now,
            BookingChannel = BookingConstants.BookingChannel.Online
        });

        db.BookingSeats.Add(new BookingSeat
        {
            BookingSeatId = "BKS_SCAN",
            BookingId = "BKG_SCAN",
            ShowtimeSeatId = "STS_SCAN",
            SeatPrice = 100000m
        });

        db.Tickets.Add(new Ticket
        {
            TicketId = "TCK_SCAN",
            BookingSeatId = "BKS_SCAN",
            QrCode = qrCode,
            TicketStatus = ticketStatus,
            GeneratedAt = now
        });

        await db.SaveChangesAsync();
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<T>(JsonOptions);
    }
}
