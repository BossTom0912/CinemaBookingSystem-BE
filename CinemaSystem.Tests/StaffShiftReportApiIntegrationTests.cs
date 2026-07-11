using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Dashboard;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

public sealed class StaffShiftReportApiIntegrationTests
{
    private const string From = "2026-07-11T00:00:00Z";
    private const string To = "2026-07-12T00:00:00Z";
    private const string StaffA = "STF_SHIFT_A";
    private const string StaffB = "STF_SHIFT_B";
    private const string StaffC = "STF_SHIFT_C";
    private const string CinemaA = "CIN_SHIFT_A";
    private const string CinemaB = "CIN_SHIFT_B";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task GetShiftReport_StaffOwnReport_ReturnsOkWithCalculatedMetrics()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedShiftReportDataAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Staff());

        var response = await client.GetAsync(
            $"/api/dashboard/staff/shift-report?from={From}&to={To}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<StaffShiftReportResponse>>(response);

        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.Equal(StaffA, body.Data.StaffProfileId);
        Assert.Equal(CinemaA, body.Data.CinemaId);
        Assert.Equal(2, body.Data.CheckedInTicketCount);
        Assert.Equal(3, body.Data.FulfilledFbOrderCount);
        Assert.Equal(2, body.Data.CounterFbOrderCount);
        Assert.Equal(1, body.Data.OnlineFulfilledFbOrderCount);
        Assert.Equal(200m, body.Data.TotalCounterRevenue);
        Assert.Equal(120m, body.Data.CashRevenue);
        Assert.Equal(80m, body.Data.TransferRevenue);
        Assert.Equal(0m, body.Data.UnclassifiedCounterRevenue);
        Assert.Equal(5, body.Data.Transactions.Count);
    }

    [Fact]
    public async Task GetShiftReport_StaffRequestsAnotherStaff_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedShiftReportDataAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Staff());

        var response = await client.GetAsync(
            $"/api/dashboard/staff/shift-report?from={From}&to={To}&staffProfileId={StaffB}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetShiftReport_ManagerRequestsStaffInSameCinema_ReturnsOk()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedShiftReportDataAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Manager());

        var response = await client.GetAsync(
            $"/api/dashboard/staff/shift-report?from={From}&to={To}&staffProfileId={StaffA}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<StaffShiftReportResponse>>(response);
        Assert.Equal(StaffA, body!.Data!.StaffProfileId);
        Assert.Equal(200m, body.Data.TotalCounterRevenue);
    }

    [Fact]
    public async Task GetShiftReport_ManagerRequestsStaffInAnotherCinema_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedShiftReportDataAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Manager());

        var response = await client.GetAsync(
            $"/api/dashboard/staff/shift-report?from={From}&to={To}&staffProfileId={StaffC}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetShiftReport_Customer_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedShiftReportDataAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Customer());

        var response = await client.GetAsync(
            $"/api/dashboard/staff/shift-report?from={From}&to={To}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task GetShiftReport_AdminWithoutFilter_ReturnsOkAcrossAllCinemas()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedShiftReportDataAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Admin());

        var response = await client.GetAsync(
            $"/api/dashboard/staff/shift-report?from={From}&to={To}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<StaffShiftReportResponse>>(response);
        Assert.Null(body!.Data!.CinemaId);
        Assert.Equal(4, body.Data.CheckedInTicketCount);
        Assert.Equal(770m, body.Data.TotalCounterRevenue);
    }

    [Theory]
    [InlineData("/api/dashboard/staff/shift-report?from=2026-07-12T00:00:00Z&to=2026-07-11T00:00:00Z")]
    [InlineData("/api/dashboard/staff/shift-report?from=2026-07-11T00:00:00Z")]
    public async Task GetShiftReport_InvalidDateRange_ReturnsBadRequest(string url)
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedShiftReportDataAsync(factory);

        using var client = CreateClient(factory, TestAuthTokens.Admin());

        var response = await client.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static HttpClient CreateClient(CinemaWebApplicationFactory factory, string token)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return client;
    }

    private static async Task SeedShiftReportDataAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        db.Roles.AddRange(
            NewRole(AuthConstants.RoleIds.Staff, AuthConstants.Roles.Staff),
            NewRole(AuthConstants.RoleIds.Manager, AuthConstants.Roles.Manager),
            NewRole(AuthConstants.RoleIds.Admin, AuthConstants.Roles.Admin),
            NewRole(AuthConstants.RoleIds.Customer, AuthConstants.Roles.Customer));

        db.Cinemas.AddRange(
            new Cinema
            {
                CinemaId = CinemaA,
                CinemaName = "Shift Cinema A",
                Address = "A",
                City = "HCM",
                CinemaStatus = DomainConstants.CinemaStatus.Active
            },
            new Cinema
            {
                CinemaId = CinemaB,
                CinemaName = "Shift Cinema B",
                Address = "B",
                City = "HCM",
                CinemaStatus = DomainConstants.CinemaStatus.Active
            });

        db.Users.AddRange(
            NewUser("USR_TEST_STAFF", AuthConstants.RoleIds.Staff, "Staff A"),
            NewUser("USR_SHIFT_STAFF_B", AuthConstants.RoleIds.Staff, "Staff B"),
            NewUser("USR_SHIFT_STAFF_C", AuthConstants.RoleIds.Staff, "Staff C"),
            NewUser("USR_TEST_MANAGER", AuthConstants.RoleIds.Manager, "Manager A"));

        db.StaffProfiles.AddRange(
            NewStaffProfile(StaffA, "USR_TEST_STAFF", CinemaA, "Staff"),
            NewStaffProfile(StaffB, "USR_SHIFT_STAFF_B", CinemaA, "Staff"),
            NewStaffProfile(StaffC, "USR_SHIFT_STAFF_C", CinemaB, "Staff"),
            NewStaffProfile("STF_SHIFT_MANAGER", "USR_TEST_MANAGER", CinemaA, "Manager"));

        db.PaymentProviders.Add(new PaymentProvider
        {
            PaymentProviderId = "PAY_PROVIDER_SHIFT",
            ProviderName = "Shift Provider",
            ProviderStatus = DomainConstants.ResourceStatus.Active
        });

        db.CheckinLogs.AddRange(
            NewCheckIn("CIL_SHIFT_A_1", StaffA, "USR_TEST_STAFF", "TCK_SHIFT_A_1", 9, BookingConstants.CheckInResult.Success),
            NewCheckIn("CIL_SHIFT_A_2", StaffA, "USR_TEST_STAFF", "TCK_SHIFT_A_2", 10, BookingConstants.CheckInResult.Success),
            NewCheckIn("CIL_SHIFT_A_FAILED", StaffA, "USR_TEST_STAFF", "TCK_SHIFT_A_3", 11, BookingConstants.CheckInResult.Failed),
            NewCheckIn("CIL_SHIFT_B_1", StaffB, "USR_SHIFT_STAFF_B", "TCK_SHIFT_B_1", 12, BookingConstants.CheckInResult.Success),
            NewCheckIn("CIL_SHIFT_C_1", StaffC, "USR_SHIFT_STAFF_C", "TCK_SHIFT_C_1", 13, BookingConstants.CheckInResult.Success),
            new CheckinLog
            {
                CheckInLogId = "CIL_SHIFT_A_OUTSIDE",
                StaffProfileId = StaffA,
                ScannedByUserId = "USR_TEST_STAFF",
                TicketId = "TCK_SHIFT_A_OUTSIDE",
                Result = BookingConstants.CheckInResult.Success,
                ScanTime = new DateTime(2026, 7, 12, 1, 0, 0, DateTimeKind.Utc)
            });

        AddCounterBooking(db, "BKG_SHIFT_A_CASH", StaffA, 9, 30, 120m, "CASH");
        AddCounterBooking(db, "BKG_SHIFT_A_TRANSFER", StaffA, 10, 30, 80m, "BANK_TRANSFER");
        AddCounterBooking(db, "BKG_SHIFT_B_COUNTER", StaffB, 11, 30, 70m, "CASH");
        AddCounterBooking(db, "BKG_SHIFT_C_COUNTER", StaffC, 12, 30, 500m, "BANK_TRANSFER");
        AddCounterBooking(db, "BKG_SHIFT_A_OUTSIDE", StaffA, 0, 30, 999m, "CASH", new DateTime(2026, 7, 12, 0, 30, 0, DateTimeKind.Utc));

        AddOnlineFulfilledBooking(db, "BKG_SHIFT_A_ONLINE_FB", StaffA, 14, 80m);

        await db.SaveChangesAsync();
    }

    private static Role NewRole(string roleId, string roleName)
    {
        return new Role
        {
            RoleId = roleId,
            RoleName = roleName,
            Description = $"{roleName} role"
        };
    }

    private static User NewUser(string userId, string roleId, string fullName)
    {
        return new User
        {
            UserId = userId,
            RoleId = roleId,
            Email = $"{userId.ToLowerInvariant()}@test.com",
            PasswordHash = "TEST_HASH",
            FullName = fullName,
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc)
        };
    }

    private static StaffProfile NewStaffProfile(
        string staffProfileId,
        string userId,
        string cinemaId,
        string position)
    {
        return new StaffProfile
        {
            StaffProfileId = staffProfileId,
            UserId = userId,
            CinemaId = cinemaId,
            Position = position,
            EmploymentStatus = DomainConstants.StaffEmploymentStatus.Active
        };
    }

    private static CheckinLog NewCheckIn(
        string checkInLogId,
        string staffProfileId,
        string userId,
        string ticketId,
        int hour,
        string result)
    {
        return new CheckinLog
        {
            CheckInLogId = checkInLogId,
            StaffProfileId = staffProfileId,
            ScannedByUserId = userId,
            TicketId = ticketId,
            Result = result,
            ScanTime = new DateTime(2026, 7, 11, hour, 0, 0, DateTimeKind.Utc)
        };
    }

    private static void AddCounterBooking(
        CinemaDbContext db,
        string bookingId,
        string staffProfileId,
        int hour,
        int minute,
        decimal amount,
        string paymentMethod,
        DateTime? createdAt = null)
    {
        var bookingTime = createdAt ?? new DateTime(2026, 7, 11, hour, minute, 0, DateTimeKind.Utc);
        var booking = new Booking
        {
            BookingId = bookingId,
            CreatedByStaffProfileId = staffProfileId,
            FbFulfilledByStaffProfileId = staffProfileId,
            BookingChannel = BookingConstants.BookingChannel.Counter,
            BookingStatus = BookingConstants.BookingStatus.Paid,
            FbFulfillmentStatus = FbConstants.FulfillmentStatus.Fulfilled,
            FbFulfilledAt = bookingTime,
            TotalAmount = amount,
            CreatedAt = bookingTime
        };

        booking.Payments.Add(new Payment
        {
            PaymentId = $"PAY_{bookingId}",
            BookingId = bookingId,
            PaymentProviderId = "PAY_PROVIDER_SHIFT",
            Amount = amount,
            PaymentStatus = BookingConstants.PaymentStatus.Success,
            PaymentMethod = paymentMethod,
            CreatedAt = bookingTime,
            PaidAt = bookingTime
        });

        db.Bookings.Add(booking);
    }

    private static void AddOnlineFulfilledBooking(
        CinemaDbContext db,
        string bookingId,
        string staffProfileId,
        int hour,
        decimal amount)
    {
        var fulfilledAt = new DateTime(2026, 7, 11, hour, 0, 0, DateTimeKind.Utc);
        var booking = new Booking
        {
            BookingId = bookingId,
            CustomerProfileId = "CUS_SHIFT_TEST",
            ShowtimeId = "SHW_SHIFT_TEST",
            BookingChannel = BookingConstants.BookingChannel.Online,
            BookingStatus = BookingConstants.BookingStatus.Paid,
            FbFulfillmentStatus = FbConstants.FulfillmentStatus.Fulfilled,
            FbFulfilledAt = fulfilledAt,
            FbFulfilledByStaffProfileId = staffProfileId,
            TotalAmount = amount,
            CreatedAt = fulfilledAt.AddHours(-1)
        };

        booking.BookingFbItems.Add(new BookingFbItem
        {
            BookingFbitemId = $"BFI_{bookingId}",
            BookingId = bookingId,
            FbItemId = "FB_SHIFT_POPCORN",
            Quantity = 1,
            UnitPrice = amount,
            Subtotal = amount
        });

        db.Bookings.Add(booking);
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<T>(
            await response.Content.ReadAsStringAsync(),
            JsonOptions);
    }
}
