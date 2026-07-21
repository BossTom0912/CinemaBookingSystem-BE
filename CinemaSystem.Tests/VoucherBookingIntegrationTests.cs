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

public sealed class VoucherBookingIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task BookWith100PercentVoucher_ReturnsPaid_AndConfirmedVoucher()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var (showtimeId, showtimeSeatIds, voucher100Code, _) = await SeedBookingDataWithVouchersAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        var request = new CreateBookingRequest
        {
            ShowtimeId = showtimeId,
            ShowtimeSeatIds = showtimeSeatIds,
            VoucherCode = voucher100Code
        };

        var response = await client.PostAsJsonAsync("/api/bookings", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BookingResponse>>(JsonOptions);
        Assert.True(body!.Success);
        
        // 100% discount means totalAmount = 0, and status should be PAID immediately
        Assert.Equal("PAID", body.Data!.Status);
        Assert.Equal(0m, body.Data.TotalAmount);

        // Verify Database State
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var booking = await db.Bookings
            .Include(b => b.VoucherUsage)
                .ThenInclude(vu => vu!.Voucher)
            .Include(b => b.BookingSeats)
                .ThenInclude(bs => bs.Ticket)
            .FirstOrDefaultAsync(b => b.BookingId == body.Data.BookingId);

        Assert.NotNull(booking);
        Assert.Equal("PAID", booking.BookingStatus);
        Assert.Equal(0m, booking.TotalAmount);
        
        // Check VoucherUsage is Confirmed immediately
        Assert.NotNull(booking.VoucherUsage);
        Assert.Equal("CONFIRMED", booking.VoucherUsage.UsageStatus);
        Assert.NotNull(booking.VoucherUsage.UsedAt);
        Assert.Equal(1, booking.VoucherUsage.Voucher!.UsedCount);
        Assert.Equal("CV_100", booking.VoucherUsage.CustomerVoucherId);

        var claimedVoucher = await db.CustomerVouchers
            .SingleAsync(item => item.CustomerVoucherId == "CV_100");
        Assert.True(claimedVoucher.IsUsed);
        Assert.NotNull(claimedVoucher.UsedAt);

        // Check tickets are generated immediately
        Assert.Single(booking.BookingSeats);
        Assert.NotNull(booking.BookingSeats.First().Ticket);
        Assert.Equal("UNUSED", booking.BookingSeats.First().Ticket!.TicketStatus);

        // Check showtime seat status is Booked
        var sts = await db.ShowtimeSeats.FirstAsync(s => s.ShowtimeSeatId == showtimeSeatIds.First());
        Assert.Equal("BOOKED", sts.SeatStatus);
    }

    [Fact]
    public async Task BookWith10PercentVoucher_ReturnsPendingPayment_AndConfirmsOnPayment()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var (showtimeId, showtimeSeatIds, _, voucher10Code) = await SeedBookingDataWithVouchersAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        var request = new CreateBookingRequest
        {
            ShowtimeId = showtimeId,
            ShowtimeSeatIds = showtimeSeatIds,
            VoucherCode = voucher10Code
        };

        var response = await client.PostAsJsonAsync("/api/bookings", request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<BookingResponse>>(JsonOptions);
        Assert.True(body!.Success);
        
        // Base price is 100k + 10k extra = 110k. 10% off is 99k.
        Assert.Equal("PENDING_PAYMENT", body.Data!.Status);
        Assert.Equal(99000m, body.Data.TotalAmount);

        // Verify Database State before payment
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

            var bookingBefore = await db.Bookings
                .Include(b => b.VoucherUsage)
                .FirstOrDefaultAsync(b => b.BookingId == body.Data.BookingId);

            Assert.NotNull(bookingBefore);
            Assert.Equal("PENDING_PAYMENT", bookingBefore.BookingStatus);
            Assert.NotNull(bookingBefore.VoucherUsage);
            Assert.Equal("APPLIED", bookingBefore.VoucherUsage.UsageStatus);
            Assert.Null(bookingBefore.VoucherUsage.UsedAt);
            Assert.Equal("CV_10", bookingBefore.VoucherUsage.CustomerVoucherId);

            var claimedVoucherBefore = await db.CustomerVouchers
                .SingleAsync(item => item.CustomerVoucherId == "CV_10");
            Assert.False(claimedVoucherBefore.IsUsed);
            Assert.Null(claimedVoucherBefore.UsedAt);

            var stsBefore = await db.ShowtimeSeats.FirstAsync(s => s.ShowtimeSeatId == showtimeSeatIds.First());
            Assert.Equal("LOCKED", stsBefore.SeatStatus);
        }

        // Create Payment
        var createPaymentRequest = new { BookingId = body.Data.BookingId, PaymentProviderId = "PAYPROV_TEST" };
        var paymentResponse = await client.PostAsJsonAsync("/api/payment", createPaymentRequest);
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        var paymentBody = await paymentResponse.Content.ReadFromJsonAsync<ApiResponse<CinemaSystem.Contracts.Payments.CreatePaymentResponse>>(JsonOptions);
        var transactionCode = paymentBody!.Data!.TransactionCode;

        // Simulate Webhook confirming 99,000 VND payment
        var webhookPayload = new
        {
            content = $"Cinema {transactionCode}",
            transferAmount = 99000,
            referenceCode = "SEP123"
        };
        var payloadJson = JsonSerializer.Serialize(webhookPayload);
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var secret = CinemaWebApplicationFactory.TestSepayWebhookSecret;
        var signature = "sha256=" + ComputeHmacSha256(timestamp + "." + payloadJson, secret);

        var webhookRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payment/sepay-webhook")
        {
            Content = new StringContent(payloadJson, System.Text.Encoding.UTF8, "application/json")
        };
        webhookRequest.Headers.Add("x-sepay-signature", signature);
        webhookRequest.Headers.Add("x-sepay-timestamp", timestamp);

        var webhookResponse = await client.SendAsync(webhookRequest);
        Assert.Equal(HttpStatusCode.OK, webhookResponse.StatusCode);

        // Verify Database State after payment
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

            var bookingAfter = await db.Bookings
                .Include(b => b.VoucherUsage)
                    .ThenInclude(vu => vu!.Voucher)
                .Include(b => b.BookingSeats)
                    .ThenInclude(bs => bs.Ticket)
                .FirstOrDefaultAsync(b => b.BookingId == body.Data.BookingId);

            Assert.NotNull(bookingAfter);
            Assert.Equal("PAID", bookingAfter.BookingStatus);
            
            // Voucher should be confirmed and count incremented
            Assert.NotNull(bookingAfter.VoucherUsage);
            Assert.Equal("CONFIRMED", bookingAfter.VoucherUsage.UsageStatus);
            Assert.NotNull(bookingAfter.VoucherUsage.UsedAt);
            Assert.Equal(1, bookingAfter.VoucherUsage.Voucher!.UsedCount);
            Assert.Equal("CV_10", bookingAfter.VoucherUsage.CustomerVoucherId);

            var claimedVoucherAfter = await db.CustomerVouchers
                .SingleAsync(item => item.CustomerVoucherId == "CV_10");
            Assert.True(claimedVoucherAfter.IsUsed);
            Assert.NotNull(claimedVoucherAfter.UsedAt);

            // Ticket generated and seat booked
            Assert.Single(bookingAfter.BookingSeats);
            Assert.NotNull(bookingAfter.BookingSeats.First().Ticket);
            Assert.Equal("UNUSED", bookingAfter.BookingSeats.First().Ticket!.TicketStatus);

            var stsAfter = await db.ShowtimeSeats.FirstAsync(s => s.ShowtimeSeatId == showtimeSeatIds.First());
            Assert.Equal("BOOKED", stsAfter.SeatStatus);
        }
    }

    [Fact]
    public async Task CancelPendingBooking_ReleasesExactReservation_WithoutTouchingAnotherUsedClaim()
    {
        await using var factory = new CinemaWebApplicationFactory();
        var (showtimeId, showtimeSeatIds, _, voucher10Code) =
            await SeedBookingDataWithVouchersAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        var createResponse = await client.PostAsJsonAsync(
            "/api/bookings",
            new CreateBookingRequest
            {
                ShowtimeId = showtimeId,
                ShowtimeSeatIds = showtimeSeatIds,
                VoucherCode = voucher10Code
            });

        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var createBody = await createResponse.Content
            .ReadFromJsonAsync<ApiResponse<BookingResponse>>(JsonOptions);
        Assert.NotNull(createBody?.Data);

        var cancelResponse = await client.PostAsync(
            $"/api/bookings/{createBody.Data.BookingId}/cancel",
            content: null);

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var usage = await db.VoucherUsages
            .SingleAsync(item => item.BookingId == createBody.Data.BookingId);
        var reservedClaim = await db.CustomerVouchers
            .SingleAsync(item => item.CustomerVoucherId == "CV_10");
        var previouslyUsedClaim = await db.CustomerVouchers
            .SingleAsync(item => item.CustomerVoucherId == "CV_10_USED");

        Assert.Equal("CANCELLED", usage.UsageStatus);
        Assert.Equal("CV_10", usage.CustomerVoucherId);
        Assert.False(reservedClaim.IsUsed);
        Assert.Null(reservedClaim.UsedAt);
        Assert.True(previouslyUsedClaim.IsUsed);
        Assert.NotNull(previouslyUsedClaim.UsedAt);
    }

    private static string ComputeHmacSha256(string data, string key)
    {
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        var bytes = System.Text.Encoding.UTF8.GetBytes(data);
        var hash = hmac.ComputeHash(bytes);
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }

    private async Task<(string showtimeId, List<string> showtimeSeatIds, string voucher100Code, string voucher10Code)> SeedBookingDataWithVouchersAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        // Clear tables to avoid key conflicts
        db.VoucherUsages.RemoveRange(db.VoucherUsages);
        db.CustomerVouchers.RemoveRange(db.CustomerVouchers);
        db.Vouchers.RemoveRange(db.Vouchers);
        db.Tickets.RemoveRange(db.Tickets);
        db.BookingSeats.RemoveRange(db.BookingSeats);
        db.Bookings.RemoveRange(db.Bookings);
        db.ShowtimeSeats.RemoveRange(db.ShowtimeSeats);
        db.Showtimes.RemoveRange(db.Showtimes);
        db.Movies.RemoveRange(db.Movies);
        db.Seats.RemoveRange(db.Seats);
        db.Rooms.RemoveRange(db.Rooms);
        db.Cinemas.RemoveRange(db.Cinemas);
        db.CustomerProfiles.RemoveRange(db.CustomerProfiles);
        db.Users.RemoveRange(db.Users);
        db.Roles.RemoveRange(db.Roles);
        db.PaymentProviders.RemoveRange(db.PaymentProviders);

        await db.SaveChangesAsync();

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

        var customer = new CustomerProfile
        {
            CustomerProfileId = "CUS_01",
            UserId = user.UserId,
            MemberLevel = "STANDARD"
        };
        db.CustomerProfiles.Add(customer);

        // Seed Vouchers
        var voucher100 = new Voucher
        {
            VoucherId = "V_100",
            VoucherCode = "FREE100",
            Title = "Free 100%",
            DiscountType = "PERCENT",
            DiscountValue = 100,
            UsageLimit = 100,
            UsedCount = 0,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            VoucherStatus = "ACTIVE"
        };
        db.Vouchers.Add(voucher100);

        var voucher10 = new Voucher
        {
            VoucherId = "V_10",
            VoucherCode = "OFF10",
            Title = "Off 10%",
            DiscountType = "PERCENT",
            DiscountValue = 10,
            UsageLimit = 100,
            UsedCount = 0,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10),
            VoucherStatus = "ACTIVE"
        };
        db.Vouchers.Add(voucher10);

        db.CustomerVouchers.AddRange(
            new CustomerVoucher
            {
                CustomerVoucherId = "CV_100",
                CustomerProfileId = customer.CustomerProfileId,
                VoucherId = voucher100.VoucherId,
                ClaimedAt = DateTime.UtcNow.AddHours(-3),
                IsUsed = false
            },
            new CustomerVoucher
            {
                CustomerVoucherId = "CV_10_USED",
                CustomerProfileId = customer.CustomerProfileId,
                VoucherId = voucher10.VoucherId,
                ClaimedAt = DateTime.UtcNow.AddHours(-3),
                IsUsed = true,
                UsedAt = DateTime.UtcNow.AddHours(-2)
            },
            new CustomerVoucher
            {
                CustomerVoucherId = "CV_10",
                CustomerProfileId = customer.CustomerProfileId,
                VoucherId = voucher10.VoucherId,
                ClaimedAt = DateTime.UtcNow.AddHours(-1),
                IsUsed = false
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

        return (showtime.ShowtimeId, new List<string> { showtimeSeat.ShowtimeSeatId }, voucher100.VoucherCode, voucher10.VoucherCode);
    }
}
