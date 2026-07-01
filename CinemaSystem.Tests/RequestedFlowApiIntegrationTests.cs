using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Movies;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

public sealed class RequestedFlowApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task DeleteMovie_WithFutureShowtimes_MovesPaidBookingToRefundPending()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedBaseDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

        using var form = new MultipartFormDataContent();
        AddFormField(form, "Title", "Requested Flow Movie");
        AddFormField(form, "DurationMinutes", "120");
        AddFormField(form, "IsDurationConfirmed", "true");
        AddFormField(form, "GenreIds", "1");
        AddFormField(form, "Language", "VN");
        AddFormField(form, "ReleaseDate", "2026-06-26");
        AddFormField(form, "AgeRating", "T13");
        AddFormField(form, "Description", "Movie created by requested refund flow test.");

        var createMovie = await client.PostAsync("/api/movies", form);
        Assert.Equal(HttpStatusCode.Created, createMovie.StatusCode);
        var createdMovie = await DeserializeAsync<ApiResponse<MovieDetailResponse>>(createMovie);
        var movieId = createdMovie!.Data!.MovieId;

        var firstStartTime = DateTime.UtcNow.Date.AddDays(1).AddHours(10);
        var secondStartTime = firstStartTime.AddDays(1);
        var showtime27 = await CreateShowtimeAsync(
            client,
            movieId,
            "ROOM_FLOW_A",
            firstStartTime);
        var showtime28 = await CreateShowtimeAsync(
            client,
            movieId,
            "ROOM_FLOW_A",
            secondStartTime);

        var paidShowtimeSeatId = await GetFirstShowtimeSeatIdAsync(factory, showtime27.ShowtimeId);
        await SeedPaidBookingAsync(factory, showtime27.ShowtimeId, paidShowtimeSeatId, "BKG_PAID_27", "PAY_PAID_27");

        var deleteMovie = await client.DeleteAsync($"/api/movies/{movieId}");

        Assert.Equal(HttpStatusCode.OK, deleteMovie.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var movie = await db.Movies.SingleAsync(item => item.MovieId == movieId);
        Assert.Equal(DomainConstants.EntityStatus.Inactive, movie.MovieStatus);

        var firstShowtime = await db.Showtimes.SingleAsync(item => item.ShowtimeId == showtime27.ShowtimeId);
        var secondShowtime = await db.Showtimes.SingleAsync(item => item.ShowtimeId == showtime28.ShowtimeId);
        Assert.Equal(BookingConstants.ShowtimeStatus.Cancelled, firstShowtime.Status);
        Assert.Equal(BookingConstants.ShowtimeStatus.Cancelled, secondShowtime.Status);

        var booking = await db.Bookings.SingleAsync(item => item.BookingId == "BKG_PAID_27");
        Assert.Equal(BookingConstants.BookingStatus.RefundPending, booking.BookingStatus);

        var refund = await db.Refunds.SingleAsync(item => item.BookingId == "BKG_PAID_27");
        Assert.Equal(BookingConstants.RefundStatus.Pending, refund.RefundStatus);
        Assert.Equal(120000m, refund.RefundAmount);
        Assert.NotNull(refund.ShowtimeCancellationId);
    }

    [Fact]
    public async Task UpdateShowtimeRoom_WithPaidBooking_CurrentlyMarksProcessingUnstableAndKeepsOriginalRoom()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedBookedRoomFlowAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

        var update = await client.PutAsJsonAsync("/api/showtimes/SHW_ROOM_FLOW", new UpdateShowtimeRequest
        {
            MovieId = "MOV_ROOM_FLOW",
            RoomId = "ROOM_FLOW_B",
            StartTime = new DateTime(2026, 6, 27, 12, 0, 0, DateTimeKind.Utc),
            BasePrice = 120000m,
            Status = BookingConstants.ShowtimeStatus.Open
        });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ShowtimeResponse>>(update);
        Assert.Equal(DomainConstants.EntityStatus.ProcessingUnstable, body!.Data!.Status);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var showtime = await db.Showtimes.SingleAsync(item => item.ShowtimeId == "SHW_ROOM_FLOW");
        Assert.Equal("ROOM_FLOW_A", showtime.RoomId);
        Assert.Equal(DomainConstants.EntityStatus.ProcessingUnstable, showtime.Status);

        var booking = await db.Bookings.SingleAsync(item => item.BookingId == "BKG_ROOM_FLOW");
        Assert.Equal(DomainConstants.EntityStatus.ProcessingUnstable, booking.BookingStatus);

        var showtimeSeat = await db.ShowtimeSeats.SingleAsync(item => item.ShowtimeSeatId == "STS_ROOM_FLOW_A1");
        Assert.Equal("SEAT_FLOW_A1", showtimeSeat.SeatId);
    }

    [Fact]
    public async Task UpdateShowtimeDateTimeAndPrice_WithPaidBooking_MarksProcessingUnstableAndSendsWaitingEmail()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedBookedRoomFlowAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

        var update = await client.PutAsJsonAsync("/api/showtimes/SHW_ROOM_FLOW", new UpdateShowtimeRequest
        {
            MovieId = "MOV_ROOM_FLOW",
            RoomId = "ROOM_FLOW_A",
            StartTime = new DateTime(2026, 6, 27, 14, 0, 0, DateTimeKind.Utc),
            BasePrice = 150000m,
            Status = BookingConstants.ShowtimeStatus.Open
        });

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ShowtimeResponse>>(update);
        Assert.Equal(DomainConstants.EntityStatus.ProcessingUnstable, body!.Data!.Status);
        Assert.Equal("Showtime unstable. Manual processing required.", body.Message);

        await WaitForEmailCountAsync(factory, 1);
        var email = Assert.Single(factory.EmailCapture.Emails);
        Assert.Equal("flow-customer@test.com", email.ToEmail);
        Assert.Equal("Unexpected Update Notification", email.Subject);
        Assert.Contains("Start time changed to 27/06/2026 14:00", email.Body);
        Assert.Contains("Please wait for the cinema to handle it.", email.Body);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var showtime = await db.Showtimes.SingleAsync(item => item.ShowtimeId == "SHW_ROOM_FLOW");
        Assert.Equal(DomainConstants.EntityStatus.ProcessingUnstable, showtime.Status);
        Assert.Equal(new DateTime(2026, 6, 27, 10, 0, 0, DateTimeKind.Utc), showtime.StartTime);
        Assert.Equal(120000m, showtime.BasePrice);

        var booking = await db.Bookings.SingleAsync(item => item.BookingId == "BKG_ROOM_FLOW");
        Assert.Equal(DomainConstants.EntityStatus.ProcessingUnstable, booking.BookingStatus);
    }

    [Fact]
    public async Task DeleteRoom_WithPaidBookedShowtime_SuspendsShowtimeForManualHandling()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedBookedRoomFlowAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

        var delete = await client.DeleteAsync("/api/rooms/rooms/ROOM_FLOW_A");

        Assert.Equal(HttpStatusCode.OK, delete.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var oldRoom = await db.Rooms.SingleAsync(item => item.RoomId == "ROOM_FLOW_A");
        Assert.Equal(DomainConstants.EntityStatus.Inactive, oldRoom.RoomStatus);

        var replacementRoom = await db.Rooms.SingleAsync(item => item.RoomId == "ROOM_FLOW_B");
        Assert.Equal(DomainConstants.EntityStatus.Active, replacementRoom.RoomStatus);

        var showtime = await db.Showtimes.SingleAsync(item => item.ShowtimeId == "SHW_ROOM_FLOW");
        Assert.Equal("ROOM_FLOW_A", showtime.RoomId);
        Assert.Equal(DomainConstants.EntityStatus.Suspended, showtime.Status);

        var booking = await db.Bookings.SingleAsync(item => item.BookingId == "BKG_ROOM_FLOW");
        Assert.Equal(BookingConstants.BookingStatus.Paid, booking.BookingStatus);
        Assert.False(await db.Refunds.AnyAsync(item => item.BookingId == "BKG_ROOM_FLOW"));
    }

    private static async Task<ShowtimeResponse> CreateShowtimeAsync(
        HttpClient client,
        string movieId,
        string roomId,
        DateTime startTime)
    {
        var response = await client.PostAsJsonAsync("/api/showtimes", new CreateShowtimeRequest
        {
            MovieId = movieId,
            RoomId = roomId,
            StartTime = startTime,
            BasePrice = 120000m,
            Status = BookingConstants.ShowtimeStatus.Open
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<ShowtimeResponse>>(response);
        return body!.Data!;
    }

    private static async Task SeedBaseDataAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var now = DateTime.UtcNow;

        db.Roles.AddRange(
            new Role
            {
                RoleId = AuthConstants.RoleIds.Admin,
                RoleName = AuthConstants.Roles.Admin,
                Description = "Admin"
            },
            new Role
            {
                RoleId = AuthConstants.RoleIds.Customer,
                RoleName = AuthConstants.Roles.Customer,
                Description = "Customer"
            });
        db.Users.AddRange(
            new User
            {
                UserId = "USR_TEST_ADMIN",
                RoleId = AuthConstants.RoleIds.Admin,
                Email = "admin@test.com",
                PasswordHash = "HASH",
                FullName = "Test Admin",
                Status = AuthConstants.UserStatus.Active,
                EmailVerified = true,
                CreatedAt = now
            },
            new User
            {
                UserId = "USR_FLOW_CUSTOMER",
                RoleId = AuthConstants.RoleIds.Customer,
                Email = "flow-customer@test.com",
                PasswordHash = "HASH",
                FullName = "Flow Customer",
                Status = AuthConstants.UserStatus.Active,
                EmailVerified = true,
                CreatedAt = now
            });
        db.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerProfileId = "CUS_FLOW",
            UserId = "USR_FLOW_CUSTOMER",
            MemberLevel = "STANDARD",
            RewardPoints = 0
        });
        db.Cinemas.Add(new Cinema
        {
            CinemaId = "CIN_FLOW",
            CinemaName = "Flow Cinema",
            Address = "1 Flow Street",
            City = "HCM",
            CinemaStatus = DomainConstants.EntityStatus.Active
        });
        db.SeatTypes.Add(new SeatType
        {
            SeatTypeId = "SEAT_TYPE_FLOW",
            TypeName = "STANDARD",
            ExtraFee = 0
        });
        db.Rooms.AddRange(
            new Room
            {
                RoomId = "ROOM_FLOW_A",
                CinemaId = "CIN_FLOW",
                RoomName = "Flow Room A",
                Capacity = 2,
                RoomStatus = DomainConstants.EntityStatus.Active
            },
            new Room
            {
                RoomId = "ROOM_FLOW_B",
                CinemaId = "CIN_FLOW",
                RoomName = "Flow Room B",
                Capacity = 2,
                RoomStatus = DomainConstants.EntityStatus.Active
            });
        db.Seats.AddRange(
            new Seat { SeatId = "SEAT_FLOW_A1", RoomId = "ROOM_FLOW_A", SeatTypeId = "SEAT_TYPE_FLOW", RowLabel = "A", SeatNumber = 1, SeatCode = "A1", IsActive = true },
            new Seat { SeatId = "SEAT_FLOW_A2", RoomId = "ROOM_FLOW_A", SeatTypeId = "SEAT_TYPE_FLOW", RowLabel = "A", SeatNumber = 2, SeatCode = "A2", IsActive = true },
            new Seat { SeatId = "SEAT_FLOW_B1", RoomId = "ROOM_FLOW_B", SeatTypeId = "SEAT_TYPE_FLOW", RowLabel = "A", SeatNumber = 1, SeatCode = "A1", IsActive = true },
            new Seat { SeatId = "SEAT_FLOW_B2", RoomId = "ROOM_FLOW_B", SeatTypeId = "SEAT_TYPE_FLOW", RowLabel = "A", SeatNumber = 2, SeatCode = "A2", IsActive = true });
        db.PaymentProviders.Add(new PaymentProvider
        {
            PaymentProviderId = "PAYPROV_FLOW",
            ProviderName = "SEPAY_FLOW",
            ProviderStatus = DomainConstants.EntityStatus.Active
        });
        db.Genres.Add(new Genre
        {
            GenreId = 1,
            Name = "Drama"
        });
        db.Languages.Add(new Language
        {
            LanguageId = "VN",
            Name = "Vietnamese"
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedBookedRoomFlowAsync(CinemaWebApplicationFactory factory)
    {
        await SeedBaseDataAsync(factory);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var now = DateTime.UtcNow;

        db.Movies.Add(new Movie
        {
            MovieId = "MOV_ROOM_FLOW",
            Title = "Room Flow Movie",
            DurationMinutes = 120,
            AgeRating = "T13",
            MovieStatus = DomainConstants.EntityStatus.Active
        });
        db.Showtimes.Add(new Showtime
        {
            ShowtimeId = "SHW_ROOM_FLOW",
            MovieId = "MOV_ROOM_FLOW",
            RoomId = "ROOM_FLOW_A",
            StartTime = new DateTime(2026, 6, 27, 10, 0, 0, DateTimeKind.Utc),
            EndTime = new DateTime(2026, 6, 27, 12, 0, 0, DateTimeKind.Utc),
            BasePrice = 120000m,
            Status = BookingConstants.ShowtimeStatus.Open,
            CreatedAt = now
        });
        db.ShowtimeSeats.AddRange(
            new ShowtimeSeat
            {
                ShowtimeSeatId = "STS_ROOM_FLOW_A1",
                ShowtimeId = "SHW_ROOM_FLOW",
                SeatId = "SEAT_FLOW_A1",
                SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked,
                RowVersion = new byte[8]
            },
            new ShowtimeSeat
            {
                ShowtimeSeatId = "STS_ROOM_FLOW_A2",
                ShowtimeId = "SHW_ROOM_FLOW",
                SeatId = "SEAT_FLOW_A2",
                SeatStatus = BookingConstants.ShowtimeSeatStatus.Available,
                RowVersion = new byte[8]
            });
        await db.SaveChangesAsync();

        await SeedPaidBookingAsync(factory, "SHW_ROOM_FLOW", "STS_ROOM_FLOW_A1", "BKG_ROOM_FLOW", "PAY_ROOM_FLOW");
    }

    private static async Task SeedPaidBookingAsync(
        CinemaWebApplicationFactory factory,
        string showtimeId,
        string showtimeSeatId,
        string bookingId,
        string paymentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var now = DateTime.UtcNow;

        var showtimeSeat = await db.ShowtimeSeats.SingleAsync(item => item.ShowtimeSeatId == showtimeSeatId);
        showtimeSeat.SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked;

        db.Bookings.Add(new Booking
        {
            BookingId = bookingId,
            CustomerProfileId = "CUS_FLOW",
            ShowtimeId = showtimeId,
            BookingStatus = BookingConstants.BookingStatus.Paid,
            TotalAmount = 120000m,
            CreatedAt = now,
            BookingChannel = BookingConstants.BookingChannel.Online
        });
        db.BookingSeats.Add(new BookingSeat
        {
            BookingSeatId = $"BKS_{bookingId}",
            BookingId = bookingId,
            ShowtimeSeatId = showtimeSeatId,
            SeatPrice = 120000m
        });
        db.Payments.Add(new Payment
        {
            PaymentId = paymentId,
            BookingId = bookingId,
            PaymentProviderId = "PAYPROV_FLOW",
            Amount = 120000m,
            PaymentStatus = BookingConstants.PaymentStatus.Success,
            PaymentMethod = "SEPAY",
            TransactionCode = $"TXN_{paymentId}",
            CreatedAt = now,
            PaidAt = now
        });

        await db.SaveChangesAsync();
    }

    private static async Task<string> GetFirstShowtimeSeatIdAsync(
        CinemaWebApplicationFactory factory,
        string showtimeId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        return await db.ShowtimeSeats
            .Where(item => item.ShowtimeId == showtimeId)
            .OrderBy(item => item.ShowtimeSeatId)
            .Select(item => item.ShowtimeSeatId)
            .FirstAsync();
    }

    private static void AddFormField(MultipartFormDataContent form, string name, string value)
    {
        form.Add(new StringContent(value), name);
    }

    private static async Task WaitForEmailCountAsync(CinemaWebApplicationFactory factory, int expectedCount)
    {
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (factory.EmailCapture.Emails.Count < expectedCount && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
    }
}
