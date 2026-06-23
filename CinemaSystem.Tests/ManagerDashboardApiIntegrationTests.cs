using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Dashboard;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

public sealed class ManagerDashboardApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetDashboard_ManagerScope_ReturnsRevenueTicketsAndOccupancyForAssignedCinema()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var range = await SeedDashboardDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.GetAsync(
            $"/api/manager/dashboard?from={Uri.EscapeDataString(range.From.ToString("O"))}&to={Uri.EscapeDataString(range.To.ToString("O"))}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ManagerDashboardOverviewResponse>>(response);

        Assert.True(body!.Success);
        Assert.Equal("CIN_DASH_A", body.Data!.CinemaId);
        Assert.Equal("Dashboard Cinema A", body.Data.CinemaName);
        Assert.Equal(range.From, body.Data.From);
        Assert.Equal(range.To, body.Data.To);
        Assert.Equal(300000m, body.Data.GrossRevenue);
        Assert.Equal(100000m, body.Data.RefundedAmount);
        Assert.Equal(200000m, body.Data.TotalRevenue);
        Assert.Equal(2, body.Data.TicketsSold);
        Assert.Equal(4, body.Data.TotalShowtimeSeats);
        Assert.Equal(50m, body.Data.RoomOccupancyRate);
    }

    [Fact]
    public async Task GetDashboard_InvalidDateRange_ReturnsBadRequest()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedMinimalManagerScopeAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var from = new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);

        var response = await client.GetAsync(
            $"/api/manager/dashboard?from={Uri.EscapeDataString(from.ToString("O"))}&to={Uri.EscapeDataString(to.ToString("O"))}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.False(body!.Success);
        Assert.Equal("INVALID_DATE_RANGE", body.ErrorCode);
    }

    [Fact]
    public async Task GetDashboard_ManagerWithoutActiveStaffProfile_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.GetAsync("/api/manager/dashboard");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.False(body!.Success);
        Assert.Equal("STAFF_PROFILE_SCOPE_NOT_FOUND", body.ErrorCode);
    }

    private static async Task<DateRange> SeedDashboardDataAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var from = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = new DateTime(2026, 6, 30, 23, 59, 59, DateTimeKind.Utc);
        var now = new DateTime(2026, 6, 10, 12, 0, 0, DateTimeKind.Utc);

        db.Roles.Add(new Role
        {
            RoleId = AuthConstants.RoleIds.Customer,
            RoleName = AuthConstants.Roles.Customer,
            Description = "Customer"
        });

        db.Users.AddRange(
            new User
            {
                UserId = "USR_DASH_CUSTOMER_A",
                RoleId = AuthConstants.RoleIds.Customer,
                Email = "dash-a@test.com",
                PasswordHash = "HASH",
                FullName = "Dashboard Customer A",
                Status = AuthConstants.UserStatus.Active,
                EmailVerified = true,
                CreatedAt = now
            },
            new User
            {
                UserId = "USR_DASH_CUSTOMER_B",
                RoleId = AuthConstants.RoleIds.Customer,
                Email = "dash-b@test.com",
                PasswordHash = "HASH",
                FullName = "Dashboard Customer B",
                Status = AuthConstants.UserStatus.Active,
                EmailVerified = true,
                CreatedAt = now
            });
        db.CustomerProfiles.AddRange(
            new CustomerProfile
            {
                CustomerProfileId = "CUS_DASH_A",
                UserId = "USR_DASH_CUSTOMER_A",
                MemberLevel = "STANDARD",
                RewardPoints = 0
            },
            new CustomerProfile
            {
                CustomerProfileId = "CUS_DASH_B",
                UserId = "USR_DASH_CUSTOMER_B",
                MemberLevel = "STANDARD",
                RewardPoints = 0
            });

        db.Cinemas.AddRange(
            new Cinema { CinemaId = "CIN_DASH_A", CinemaName = "Dashboard Cinema A", Address = "A", City = "HCM", CinemaStatus = "ACTIVE" },
            new Cinema { CinemaId = "CIN_DASH_B", CinemaName = "Dashboard Cinema B", Address = "B", City = "HCM", CinemaStatus = "ACTIVE" });
        db.Movies.Add(new Movie
        {
            MovieId = "MOV_DASH",
            Title = "Dashboard Movie",
            DurationMinutes = 120,
            AgeRating = "T13",
            MovieStatus = "NOW_SHOWING"
        });
        db.SeatTypes.Add(new SeatType
        {
            SeatTypeId = "SEAT_TYPE_DASH",
            TypeName = "STANDARD_DASH",
            ExtraFee = 0
        });
        db.Rooms.AddRange(
            new Room { RoomId = "ROOM_DASH_A", CinemaId = "CIN_DASH_A", RoomName = "Room A", Capacity = 4, RoomStatus = "ACTIVE" },
            new Room { RoomId = "ROOM_DASH_B", CinemaId = "CIN_DASH_B", RoomName = "Room B", Capacity = 1, RoomStatus = "ACTIVE" });

        db.Seats.AddRange(
            new Seat { SeatId = "SEAT_DASH_A1", RoomId = "ROOM_DASH_A", SeatTypeId = "SEAT_TYPE_DASH", RowLabel = "A", SeatNumber = 1, SeatCode = "A1", IsActive = true },
            new Seat { SeatId = "SEAT_DASH_A2", RoomId = "ROOM_DASH_A", SeatTypeId = "SEAT_TYPE_DASH", RowLabel = "A", SeatNumber = 2, SeatCode = "A2", IsActive = true },
            new Seat { SeatId = "SEAT_DASH_A3", RoomId = "ROOM_DASH_A", SeatTypeId = "SEAT_TYPE_DASH", RowLabel = "A", SeatNumber = 3, SeatCode = "A3", IsActive = true },
            new Seat { SeatId = "SEAT_DASH_A4", RoomId = "ROOM_DASH_A", SeatTypeId = "SEAT_TYPE_DASH", RowLabel = "A", SeatNumber = 4, SeatCode = "A4", IsActive = true },
            new Seat { SeatId = "SEAT_DASH_B1", RoomId = "ROOM_DASH_B", SeatTypeId = "SEAT_TYPE_DASH", RowLabel = "A", SeatNumber = 1, SeatCode = "A1", IsActive = true });

        db.Showtimes.AddRange(
            new Showtime
            {
                ShowtimeId = "SHW_DASH_A_RANGE",
                MovieId = "MOV_DASH",
                RoomId = "ROOM_DASH_A",
                StartTime = new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 6, 15, 11, 0, 0, DateTimeKind.Utc),
                BasePrice = 100000m,
                Status = BookingConstants.ShowtimeStatus.Open,
                CreatedAt = now
            },
            new Showtime
            {
                ShowtimeId = "SHW_DASH_A_OLD",
                MovieId = "MOV_DASH",
                RoomId = "ROOM_DASH_A",
                StartTime = new DateTime(2026, 5, 15, 9, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 5, 15, 11, 0, 0, DateTimeKind.Utc),
                BasePrice = 100000m,
                Status = BookingConstants.ShowtimeStatus.Completed,
                CreatedAt = now
            },
            new Showtime
            {
                ShowtimeId = "SHW_DASH_B_RANGE",
                MovieId = "MOV_DASH",
                RoomId = "ROOM_DASH_B",
                StartTime = new DateTime(2026, 6, 15, 9, 0, 0, DateTimeKind.Utc),
                EndTime = new DateTime(2026, 6, 15, 11, 0, 0, DateTimeKind.Utc),
                BasePrice = 100000m,
                Status = BookingConstants.ShowtimeStatus.Open,
                CreatedAt = now
            });

        db.ShowtimeSeats.AddRange(
            new ShowtimeSeat { ShowtimeSeatId = "STS_DASH_A1", ShowtimeId = "SHW_DASH_A_RANGE", SeatId = "SEAT_DASH_A1", SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked, RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_DASH_A2", ShowtimeId = "SHW_DASH_A_RANGE", SeatId = "SEAT_DASH_A2", SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked, RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_DASH_A3", ShowtimeId = "SHW_DASH_A_RANGE", SeatId = "SEAT_DASH_A3", SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked, RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_DASH_A4", ShowtimeId = "SHW_DASH_A_RANGE", SeatId = "SEAT_DASH_A4", SeatStatus = BookingConstants.ShowtimeSeatStatus.Locked, LockedUntil = now.AddMinutes(10), LockedByUserId = "USR_DASH_CUSTOMER_A", RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_DASH_OLD", ShowtimeId = "SHW_DASH_A_OLD", SeatId = "SEAT_DASH_A1", SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked, RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_DASH_B1", ShowtimeId = "SHW_DASH_B_RANGE", SeatId = "SEAT_DASH_B1", SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked, RowVersion = new byte[8] });

        db.PaymentProviders.Add(new PaymentProvider
        {
            PaymentProviderId = "PAYPROV_DASH",
            ProviderName = "SEPAY_DASH",
            ProviderStatus = "ACTIVE"
        });

        db.Bookings.AddRange(
            new Booking
            {
                BookingId = "BKG_DASH_A_PAID",
                CustomerProfileId = "CUS_DASH_A",
                ShowtimeId = "SHW_DASH_A_RANGE",
                BookingStatus = BookingConstants.BookingStatus.Paid,
                TotalAmount = 200000m,
                CreatedAt = now,
                BookingChannel = BookingConstants.BookingChannel.Online
            },
            new Booking
            {
                BookingId = "BKG_DASH_A_REFUNDED",
                CustomerProfileId = "CUS_DASH_A",
                ShowtimeId = "SHW_DASH_A_RANGE",
                BookingStatus = BookingConstants.BookingStatus.Refunded,
                TotalAmount = 100000m,
                CreatedAt = now,
                BookingChannel = BookingConstants.BookingChannel.Online
            },
            new Booking
            {
                BookingId = "BKG_DASH_A_PENDING",
                CustomerProfileId = "CUS_DASH_A",
                ShowtimeId = "SHW_DASH_A_RANGE",
                BookingStatus = BookingConstants.BookingStatus.PendingPayment,
                TotalAmount = 100000m,
                CreatedAt = now,
                ExpiredAt = now.AddMinutes(10),
                BookingChannel = BookingConstants.BookingChannel.Online
            },
            new Booking
            {
                BookingId = "BKG_DASH_A_OLD",
                CustomerProfileId = "CUS_DASH_A",
                ShowtimeId = "SHW_DASH_A_OLD",
                BookingStatus = BookingConstants.BookingStatus.Paid,
                TotalAmount = 500000m,
                CreatedAt = now,
                BookingChannel = BookingConstants.BookingChannel.Online
            },
            new Booking
            {
                BookingId = "BKG_DASH_B_PAID",
                CustomerProfileId = "CUS_DASH_B",
                ShowtimeId = "SHW_DASH_B_RANGE",
                BookingStatus = BookingConstants.BookingStatus.Paid,
                TotalAmount = 900000m,
                CreatedAt = now,
                BookingChannel = BookingConstants.BookingChannel.Online
            });

        db.BookingSeats.AddRange(
            new BookingSeat { BookingSeatId = "BKS_DASH_A1", BookingId = "BKG_DASH_A_PAID", ShowtimeSeatId = "STS_DASH_A1", SeatPrice = 100000m },
            new BookingSeat { BookingSeatId = "BKS_DASH_A2", BookingId = "BKG_DASH_A_PAID", ShowtimeSeatId = "STS_DASH_A2", SeatPrice = 100000m },
            new BookingSeat { BookingSeatId = "BKS_DASH_A3", BookingId = "BKG_DASH_A_REFUNDED", ShowtimeSeatId = "STS_DASH_A3", SeatPrice = 100000m },
            new BookingSeat { BookingSeatId = "BKS_DASH_A4", BookingId = "BKG_DASH_A_PENDING", ShowtimeSeatId = "STS_DASH_A4", SeatPrice = 100000m },
            new BookingSeat { BookingSeatId = "BKS_DASH_OLD", BookingId = "BKG_DASH_A_OLD", ShowtimeSeatId = "STS_DASH_OLD", SeatPrice = 100000m },
            new BookingSeat { BookingSeatId = "BKS_DASH_B1", BookingId = "BKG_DASH_B_PAID", ShowtimeSeatId = "STS_DASH_B1", SeatPrice = 100000m });

        db.Tickets.AddRange(
            new Ticket { TicketId = "TCK_DASH_A1", BookingSeatId = "BKS_DASH_A1", QrCode = "QR_DASH_A1", TicketStatus = BookingConstants.TicketStatus.Unused, GeneratedAt = now },
            new Ticket { TicketId = "TCK_DASH_A2", BookingSeatId = "BKS_DASH_A2", QrCode = "QR_DASH_A2", TicketStatus = BookingConstants.TicketStatus.CheckedIn, GeneratedAt = now },
            new Ticket { TicketId = "TCK_DASH_A3", BookingSeatId = "BKS_DASH_A3", QrCode = "QR_DASH_A3", TicketStatus = BookingConstants.TicketStatus.Refunded, GeneratedAt = now },
            new Ticket { TicketId = "TCK_DASH_OLD", BookingSeatId = "BKS_DASH_OLD", QrCode = "QR_DASH_OLD", TicketStatus = BookingConstants.TicketStatus.Unused, GeneratedAt = now },
            new Ticket { TicketId = "TCK_DASH_B1", BookingSeatId = "BKS_DASH_B1", QrCode = "QR_DASH_B1", TicketStatus = BookingConstants.TicketStatus.Unused, GeneratedAt = now });

        db.Payments.AddRange(
            new Payment
            {
                PaymentId = "PAY_DASH_A_SUCCESS",
                BookingId = "BKG_DASH_A_PAID",
                PaymentProviderId = "PAYPROV_DASH",
                Amount = 200000m,
                PaymentStatus = BookingConstants.PaymentStatus.Success,
                PaymentMethod = "SEPAY",
                TransactionCode = "TDASHA001",
                CreatedAt = now,
                PaidAt = now
            },
            new Payment
            {
                PaymentId = "PAY_DASH_A_REFUNDED",
                BookingId = "BKG_DASH_A_REFUNDED",
                PaymentProviderId = "PAYPROV_DASH",
                Amount = 100000m,
                PaymentStatus = BookingConstants.PaymentStatus.Success,
                PaymentMethod = "SEPAY",
                TransactionCode = "TDASHA002",
                CreatedAt = now,
                PaidAt = now
            },
            new Payment
            {
                PaymentId = "PAY_DASH_A_PENDING",
                BookingId = "BKG_DASH_A_PENDING",
                PaymentProviderId = "PAYPROV_DASH",
                Amount = 100000m,
                PaymentStatus = BookingConstants.PaymentStatus.Pending,
                PaymentMethod = "SEPAY",
                TransactionCode = "TDASHA003",
                CreatedAt = now
            },
            new Payment
            {
                PaymentId = "PAY_DASH_A_OLD",
                BookingId = "BKG_DASH_A_OLD",
                PaymentProviderId = "PAYPROV_DASH",
                Amount = 500000m,
                PaymentStatus = BookingConstants.PaymentStatus.Success,
                PaymentMethod = "SEPAY",
                TransactionCode = "TDASHA004",
                CreatedAt = now,
                PaidAt = now
            },
            new Payment
            {
                PaymentId = "PAY_DASH_B_SUCCESS",
                BookingId = "BKG_DASH_B_PAID",
                PaymentProviderId = "PAYPROV_DASH",
                Amount = 900000m,
                PaymentStatus = BookingConstants.PaymentStatus.Success,
                PaymentMethod = "SEPAY",
                TransactionCode = "TDASHB001",
                CreatedAt = now,
                PaidAt = now
            });

        db.Refunds.Add(new Refund
        {
            RefundId = "REF_DASH_A_SUCCESS",
            BookingId = "BKG_DASH_A_REFUNDED",
            PaymentId = "PAY_DASH_A_REFUNDED",
            PaymentProviderId = "PAYPROV_DASH",
            RefundAmount = 100000m,
            RefundStatus = BookingConstants.RefundStatus.Success,
            RefundReason = "Customer refund",
            RequestedAt = now,
            RefundedAt = now
        });

        await db.SaveChangesAsync();
        await CinemaScopeTestData.SeedManagerScopeAsync(factory, "CIN_DASH_A");

        return new DateRange(from, to);
    }

    private static async Task SeedMinimalManagerScopeAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        db.Cinemas.Add(new Cinema
        {
            CinemaId = "CIN_DASH_MIN",
            CinemaName = "Dashboard Minimal Cinema",
            Address = "A",
            City = "HCM",
            CinemaStatus = "ACTIVE"
        });
        await db.SaveChangesAsync();

        await CinemaScopeTestData.SeedManagerScopeAsync(factory, "CIN_DASH_MIN");
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
    }

    private sealed record DateRange(DateTime From, DateTime To);
}
