using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CinemaSystem.Tests;

public sealed class BookingApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task CreateBooking_ValidRequest_ReturnsCreated()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var (showtimeId, showtimeSeatIds) = await SeedBookingDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        var request = new CreateBookingRequest
        {
            ShowtimeId = showtimeId,
            ShowtimeSeatIds = showtimeSeatIds
        };

        var response = await client.PostAsJsonAsync("/api/bookings", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BookingResponse>>(JsonOptions);
        Assert.True(body!.Success);
        Assert.Equal("PENDING_PAYMENT", body.Data!.Status);
        Assert.Equal(110000m, body.Data.TotalAmount); // 100000 (base) + 10000 (extra)
    }

    [Fact]
    public async Task GetBookingDetails_ExistingBooking_ReturnsDetails()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var (showtimeId, showtimeSeatIds) = await SeedBookingDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        // Create booking first
        var createRequest = new CreateBookingRequest
        {
            ShowtimeId = showtimeId,
            ShowtimeSeatIds = showtimeSeatIds
        };
        var createResponse = await client.PostAsJsonAsync("/api/bookings", createRequest);
        var createBody = await createResponse.Content.ReadFromJsonAsync<ApiResponse<BookingResponse>>(JsonOptions);
        var bookingId = createBody!.Data!.BookingId;

        // Get details
        var response = await client.GetAsync($"/api/bookings/{bookingId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BookingDetailsResponse>>(JsonOptions);
        Assert.True(body!.Success);
        Assert.Equal(bookingId, body.Data!.BookingId);
        Assert.Single(body.Data.Seats);
        Assert.Equal("1", body.Data.Seats[0].SeatNumber);
        Assert.Equal("A", body.Data.Seats[0].RowLabel);
    }

    [Fact]
    public async Task GetMyBookings_ReturnsUserBookings()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var (showtimeId, showtimeSeatIds) = await SeedBookingDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        // Create booking
        var createRequest = new CreateBookingRequest
        {
            ShowtimeId = showtimeId,
            ShowtimeSeatIds = showtimeSeatIds
        };
        await client.PostAsJsonAsync("/api/bookings", createRequest);

        // Get my bookings
        var response = await client.GetAsync("/api/bookings/my-bookings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<BookingResponse>>>(JsonOptions);
        Assert.True(body!.Success);
        Assert.Single(body.Data!);
    }

    [Fact]
    public async Task FullBookingPaymentFlow_E2E()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var (showtimeId, showtimeSeatIds) = await SeedBookingDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        // 1. Create booking
        var createBookingRequest = new CreateBookingRequest
        {
            ShowtimeId = showtimeId,
            ShowtimeSeatIds = showtimeSeatIds
        };
        var bookingResponse = await client.PostAsJsonAsync("/api/bookings", createBookingRequest);
        var bookingBody = await bookingResponse.Content.ReadFromJsonAsync<ApiResponse<BookingResponse>>(JsonOptions);
        var bookingId = bookingBody!.Data!.BookingId;

        // 2. Create payment
        var createPaymentRequest = new { BookingId = bookingId, PaymentProviderId = "PAYPROV_TEST" };
        var paymentResponse = await client.PostAsJsonAsync("/api/payment", createPaymentRequest);
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        var paymentBody = await paymentResponse.Content.ReadFromJsonAsync<ApiResponse<CinemaSystem.Contracts.Payments.CreatePaymentResponse>>(JsonOptions);
        var transactionCode = paymentBody!.Data!.TransactionCode;

        // 3. Simulate Webhook
        var webhookPayload = new
        {
            content = $"Cinema {transactionCode}",
            transferAmount = 110000,
            referenceCode = "SEP123"
        };
        var payloadJson = JsonSerializer.Serialize(webhookPayload);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var secret = "Admin@123";
        var signature = "sha256=" + ComputeHmacSha256(timestamp + "." + payloadJson, secret);

        var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payment/sepay-webhook")
        {
            Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json")
        };
        webhookRequest.Headers.Add("x-sepay-signature", signature);
        webhookRequest.Headers.Add("x-sepay-timestamp", timestamp);

        var webhookResponse = await client.SendAsync(webhookRequest);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        // 4. Verify booking status
        var statusResponse = await client.GetAsync($"/api/bookings/{bookingId}");
        var statusBody = await statusResponse.Content.ReadFromJsonAsync<ApiResponse<BookingDetailsResponse>>(JsonOptions);
        Assert.Equal("PAID", statusBody!.Data!.Status);
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        var bytes = System.Text.Encoding.UTF8.GetBytes(data);
        var hash = hmac.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private async Task<(string showtimeId, List<string> showtimeSeatIds)> SeedBookingDataAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        // Seed Role, User, CustomerProfile
        var customerRole = new Role { RoleId = "R_CUST", RoleName = "CUSTOMER" };
        db.Roles.Add(customerRole);
        
        var user = new User
        {
            UserId = "USR_TEST_CUSTOMER",
            RoleId = customerRole.RoleId,
            Email = "customer@test.com",
            PasswordHash = "HASH",
            FullName = "Test Customer",
            Status = "ACTIVE",
            EmailVerified = true
        };
        db.Users.Add(user);

        db.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerProfileId = "CUS_01",
            UserId = user.UserId,
            MemberLevel = "STANDARD"
        });

        // Seed Cinema, Room, SeatType, Seat
        var cinema = new Cinema { CinemaId = "CIN_01", CinemaName = "Test Cinema", Address = "Add", City = "City", CinemaStatus = "ACTIVE" };
        db.Cinemas.Add(cinema);

        var room = new Room { RoomId = "ROM_01", CinemaId = cinema.CinemaId, RoomName = "Room 1", Capacity = 10, RoomStatus = "ACTIVE" };
        db.Rooms.Add(room);

        var seatType = new SeatType { SeatTypeId = "ST_VIP", TypeName = "VIP", ExtraFee = 10000 };
        db.SeatTypes.Add(seatType);

        var seat = new Seat { SeatId = "SET_01", RoomId = room.RoomId, SeatTypeId = seatType.SeatTypeId, SeatCode = "A1", RowLabel = "A", SeatNumber = 1, IsActive = true };
        db.Seats.Add(seat);

        // Seed Movie, Showtime, ShowtimeSeat
        var movie = new Movie { MovieId = "MOV_01", Title = "Test Movie", DurationMinutes = 120, MovieStatus = "NOW_SHOWING" };
        db.Movies.Add(movie);

        var showtime = new Showtime
        {
            ShowtimeId = "SHW_01",
            MovieId = movie.MovieId,
            RoomId = room.RoomId,
            StartTime = DateTime.UtcNow.AddHours(2),
            EndTime = DateTime.UtcNow.AddHours(4),
            BasePrice = 100000,
            Status = "OPEN"
        };
        db.Showtimes.Add(showtime);

        var showtimeSeat = new ShowtimeSeat
        {
            ShowtimeSeatId = "STS_01",
            ShowtimeId = showtime.ShowtimeId,
            SeatId = seat.SeatId,
            SeatStatus = "AVAILABLE",
            RowVersion = new byte[8]
        };
        db.ShowtimeSeats.Add(showtimeSeat);

        db.PaymentProviders.Add(new PaymentProvider
        {
            PaymentProviderId = "PAYPROV_TEST",
            ProviderName = "TEST_PROVIDER",
            ProviderStatus = "ACTIVE"
        });

        await db.SaveChangesAsync();

        return (showtime.ShowtimeId, new List<string> { showtimeSeat.ShowtimeSeatId });
    }
}
