using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Dashboard;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

public sealed class ManagerDashboardApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public async Task GetDashboard_ManagerScope_ReturnsNetRevenueTicketsAndOccupancyForAssignedCinema()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedDashboardDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.GetAsync(
            "/api/manager/dashboard?from=2026-06-01T00:00:00Z&to=2026-07-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ManagerDashboardResponse>>(response);

        Assert.True(body!.Success);
        Assert.NotNull(body.Data);
        Assert.Equal("CIN_DASH_A", body.Data.CinemaId);
        Assert.Equal("Dashboard Cinema A", body.Data.CinemaName);
        Assert.Equal(400m, body.Data.GrossRevenue);
        Assert.Equal(100m, body.Data.RefundedAmount);
        Assert.Equal(100m, body.Data.PendingRefundAmount);
        Assert.Equal(0m, body.Data.ManualRefundAmount);
        Assert.Equal(300m, body.Data.NetRevenue);
        Assert.Equal(4, body.Data.GrossTicketsSold);
        Assert.Equal(1, body.Data.RefundedTickets);
        Assert.Equal(3, body.Data.NetTicketsSold);
        Assert.Equal(4, body.Data.SellableSeatCapacity);
        Assert.Equal(2, body.Data.OccupiedSeats);
        Assert.Equal(50m, body.Data.OccupancyRate);
    }

    [Fact]
    public async Task GetDashboard_ManagerScope_DoesNotAcceptDataFromAnotherCinema()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedDashboardDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.GetAsync(
            "/api/manager/dashboard?from=2026-06-01T00:00:00Z&to=2026-07-01T00:00:00Z&movieId=MOV_DASH");

        var body = await DeserializeAsync<ApiResponse<ManagerDashboardResponse>>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(400m, body!.Data!.GrossRevenue);
        Assert.Equal("CIN_DASH_A", body.Data.CinemaId);
    }

    [Fact]
    public async Task GetDashboard_Admin_BypassesCinemaScope()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedDashboardDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

        var response = await client.GetAsync(
            "/api/manager/dashboard?from=2026-06-01T00:00:00Z&to=2026-07-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ManagerDashboardResponse>>(response);
        Assert.Null(body!.Data!.CinemaId);
        Assert.Equal("All cinemas", body.Data.CinemaName);
        Assert.Equal(1399m, body.Data.GrossRevenue);
    }

    [Fact]
    public async Task GetDashboard_PartialSuccessfulRefund_DoesNotMarkAllBookingTicketsAsRefunded()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedDashboardDataAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var refund = await db.Refunds.FindAsync("REF_BKG_DASH_A_REFUNDED");
            refund!.RefundAmount = 50m;
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.GetAsync(
            "/api/manager/dashboard?from=2026-06-01T00:00:00Z&to=2026-07-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ManagerDashboardResponse>>(response);
        Assert.Equal(50m, body!.Data!.RefundedAmount);
        Assert.Equal(0, body.Data.RefundedTickets);
        Assert.Equal(4, body.Data.NetTicketsSold);
    }

    [Fact]
    public async Task GetDashboard_InvalidDateRange_ReturnsBadRequest()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedDashboardDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.GetAsync(
            "/api/manager/dashboard?from=2026-07-01T00:00:00Z&to=2026-06-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("INVALID_DATE_RANGE", body!.ErrorCode);
    }

    [Fact]
    public async Task GetDashboard_Customer_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        var response = await client.GetAsync(
            "/api/manager/dashboard?from=2026-06-01T00:00:00Z&to=2026-07-01T00:00:00Z");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task SeedDashboardDataAsync(CinemaWebApplicationFactory factory)
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var showtimeStart = new DateTime(2026, 6, 15, 10, 0, 0, DateTimeKind.Utc);
            var cancelledStart = new DateTime(2026, 6, 20, 10, 0, 0, DateTimeKind.Utc);

            db.Cinemas.AddRange(
                new Cinema
                {
                    CinemaId = "CIN_DASH_A",
                    CinemaName = "Dashboard Cinema A",
                    Address = "A",
                    City = "HCM",
                    CinemaStatus = "ACTIVE"
                },
                new Cinema
                {
                    CinemaId = "CIN_DASH_B",
                    CinemaName = "Dashboard Cinema B",
                    Address = "B",
                    City = "HCM",
                    CinemaStatus = "ACTIVE"
                });
            db.Movies.Add(new Movie
            {
                MovieId = "MOV_DASH",
                Title = "Dashboard Movie",
                DurationMinutes = 120,
                MovieStatus = "NOW_SHOWING"
            });
            db.SeatTypes.Add(new SeatType
            {
                SeatTypeId = "SEAT_TYPE_DASH",
                TypeName = "STANDARD",
                ExtraFee = 0
            });
            db.Rooms.AddRange(
                new Room
                {
                    RoomId = "ROOM_DASH_A",
                    CinemaId = "CIN_DASH_A",
                    RoomName = "Room A",
                    Capacity = 6,
                    RoomStatus = "ACTIVE"
                },
                new Room
                {
                    RoomId = "ROOM_DASH_B",
                    CinemaId = "CIN_DASH_B",
                    RoomName = "Room B",
                    Capacity = 1,
                    RoomStatus = "ACTIVE"
                });

            var seatsA = Enumerable.Range(1, 6)
                .Select(index => NewSeat(
                    $"SEAT_DASH_A_{index}",
                    "ROOM_DASH_A",
                    index))
                .ToList();
            var seatB = NewSeat("SEAT_DASH_B_1", "ROOM_DASH_B", 1);
            db.Seats.AddRange(seatsA);
            db.Seats.Add(seatB);

            db.Showtimes.AddRange(
                NewShowtime(
                    "SHW_DASH_A_OPEN",
                    "ROOM_DASH_A",
                    showtimeStart,
                    BookingConstants.ShowtimeStatus.Open),
                NewShowtime(
                    "SHW_DASH_A_CANCELLED",
                    "ROOM_DASH_A",
                    cancelledStart,
                    BookingConstants.ShowtimeStatus.Cancelled),
                NewShowtime(
                    "SHW_DASH_B_OPEN",
                    "ROOM_DASH_B",
                    showtimeStart,
                    BookingConstants.ShowtimeStatus.Open));

            var openShowtimeSeats = seatsA.Take(4)
                .Select((seat, index) => NewShowtimeSeat(
                    $"STS_DASH_A_OPEN_{index + 1}",
                    "SHW_DASH_A_OPEN",
                    seat.SeatId))
                .ToList();
            var cancelledShowtimeSeats = seatsA.Skip(4)
                .Select((seat, index) => NewShowtimeSeat(
                    $"STS_DASH_A_CANCELLED_{index + 1}",
                    "SHW_DASH_A_CANCELLED",
                    seat.SeatId,
                    BookingConstants.ShowtimeSeatStatus.Unavailable))
                .ToList();
            var showtimeSeatB = NewShowtimeSeat(
                "STS_DASH_B_OPEN_1",
                "SHW_DASH_B_OPEN",
                seatB.SeatId);

            db.ShowtimeSeats.AddRange(openShowtimeSeats);
            db.ShowtimeSeats.AddRange(cancelledShowtimeSeats);
            db.ShowtimeSeats.Add(showtimeSeatB);

            db.PaymentProviders.Add(new PaymentProvider
            {
                PaymentProviderId = "PAY_PROVIDER_DASH",
                ProviderName = "Dashboard Provider",
                ProviderStatus = "ACTIVE"
            });

            AddPaidBooking(
                db,
                "BKG_DASH_A_SOLD",
                "SHW_DASH_A_OPEN",
                "PAY_DASH_A_SOLD",
                200m,
                openShowtimeSeats.Take(2).ToArray());
            AddPaidBooking(
                db,
                "BKG_DASH_A_REFUNDED",
                "SHW_DASH_A_CANCELLED",
                "PAY_DASH_A_REFUNDED",
                100m,
                [cancelledShowtimeSeats[0]],
                BookingConstants.RefundStatus.Success);
            AddPaidBooking(
                db,
                "BKG_DASH_A_PENDING_REFUND",
                "SHW_DASH_A_CANCELLED",
                "PAY_DASH_A_PENDING_REFUND",
                100m,
                [cancelledShowtimeSeats[1]],
                BookingConstants.RefundStatus.Pending);
            AddPaidBooking(
                db,
                "BKG_DASH_B_SOLD",
                "SHW_DASH_B_OPEN",
                "PAY_DASH_B_SOLD",
                999m,
                [showtimeSeatB]);

            await db.SaveChangesAsync();
        }

        await CinemaScopeTestData.SeedManagerScopeAsync(factory, "CIN_DASH_A");
    }

    private static Seat NewSeat(string seatId, string roomId, int number)
    {
        return new Seat
        {
            SeatId = seatId,
            RoomId = roomId,
            SeatTypeId = "SEAT_TYPE_DASH",
            SeatCode = $"A{number}",
            RowLabel = "A",
            SeatNumber = number,
            IsActive = true
        };
    }

    private static Showtime NewShowtime(
        string showtimeId,
        string roomId,
        DateTime startTime,
        string status)
    {
        return new Showtime
        {
            ShowtimeId = showtimeId,
            MovieId = "MOV_DASH",
            RoomId = roomId,
            StartTime = startTime,
            EndTime = startTime.AddHours(2),
            BasePrice = 100m,
            Status = status,
            CreatedAt = startTime.AddDays(-1)
        };
    }

    private static ShowtimeSeat NewShowtimeSeat(
        string showtimeSeatId,
        string showtimeId,
        string seatId,
        string status = BookingConstants.ShowtimeSeatStatus.Booked)
    {
        return new ShowtimeSeat
        {
            ShowtimeSeatId = showtimeSeatId,
            ShowtimeId = showtimeId,
            SeatId = seatId,
            SeatStatus = status,
            RowVersion = []
        };
    }

    private static void AddPaidBooking(
        CinemaDbContext db,
        string bookingId,
        string showtimeId,
        string paymentId,
        decimal paymentAmount,
        IReadOnlyList<ShowtimeSeat> showtimeSeats,
        string? refundStatus = null)
    {
        var booking = new Booking
        {
            BookingId = bookingId,
            ShowtimeId = showtimeId,
            BookingStatus = refundStatus is null
                ? BookingConstants.BookingStatus.Paid
                : BookingConstants.BookingStatus.RefundPending,
            BookingChannel = BookingConstants.BookingChannel.Counter,
            TotalAmount = paymentAmount,
            CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc)
        };

        foreach (var showtimeSeat in showtimeSeats)
        {
            booking.BookingSeats.Add(new BookingSeat
            {
                BookingSeatId = $"BKS_{bookingId}_{showtimeSeat.ShowtimeSeatId}",
                BookingId = bookingId,
                ShowtimeSeatId = showtimeSeat.ShowtimeSeatId,
                SeatPrice = paymentAmount / showtimeSeats.Count
            });
        }

        var payment = new Payment
        {
            PaymentId = paymentId,
            BookingId = bookingId,
            PaymentProviderId = "PAY_PROVIDER_DASH",
            Amount = paymentAmount,
            PaymentStatus = BookingConstants.PaymentStatus.Success,
            CreatedAt = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            PaidAt = new DateTime(2026, 6, 1, 0, 1, 0, DateTimeKind.Utc)
        };

        if (refundStatus is not null)
        {
            payment.Refunds.Add(new Refund
            {
                RefundId = $"REF_{bookingId}",
                BookingId = bookingId,
                PaymentId = paymentId,
                PaymentProviderId = "PAY_PROVIDER_DASH",
                RefundAmount = paymentAmount,
                RefundStatus = refundStatus,
                RequestedAt = new DateTime(2026, 6, 21, 0, 0, 0, DateTimeKind.Utc),
                RefundedAt = refundStatus == BookingConstants.RefundStatus.Success
                    ? new DateTime(2026, 6, 22, 0, 0, 0, DateTimeKind.Utc)
                    : null
            });
        }

        booking.Payments.Add(payment);
        db.Bookings.Add(booking);
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<T>(
            await response.Content.ReadAsStringAsync(),
            JsonOptions);
    }
}
