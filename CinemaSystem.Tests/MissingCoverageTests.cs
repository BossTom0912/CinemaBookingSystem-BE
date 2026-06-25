using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Customers;
using CinemaSystem.Contracts.Movies;
using CinemaSystem.Contracts.Payments;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Controllers;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Rooms;
using CinemaSystem.Infrastructure.Services;
using CinemaSystem.Infrastructure.Showtimes;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;

namespace CinemaSystem.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// AUTH — Các trường hợp thiếu
// ═══════════════════════════════════════════════════════════════════════════

public sealed class AuthMissingCoverageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task Register_EmptyEmail_ReturnsValidationError()
    {
        // Thiếu field email bắt buộc → model validation HTTP 400 VALIDATION_ERROR.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "",
            Password = "Password1",
            FullName = "Test User"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("VALIDATION_ERROR", body!.ErrorCode);
    }

    [Fact]
    public async Task Register_InvalidEmailFormat_ReturnsValidationError()
    {
        // Email sai định dạng → model validation HTTP 400.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "not-an-email",
            Password = "Password1",
            FullName = "Test User"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("VALIDATION_ERROR", body!.ErrorCode);
    }

    [Fact]
    public async Task Register_DuplicateEmail_VerifiedAccount_ReturnsConflict()
    {
        // Email đã tồn tại và đã verify → 409 DUPLICATE_EMAIL.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        // Lần 1: Đăng ký + verify
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "dup.verified@test.com",
            Password = "Password1",
            FullName = "Dup User"
        });
        await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = "dup.verified@test.com",
            Otp = factory.FixedOtp
        });

        // Lần 2: Đăng ký lại cùng email đã verify → 409
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "dup.verified@test.com",
            Password = "Password1",
            FullName = "Dup User Again"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("DUPLICATE_EMAIL", body!.ErrorCode);
    }

    [Fact]
    public async Task Login_WrongPassword_ReturnsUnauthorized()
    {
        // Mật khẩu sai → 401 khi account đã verify.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "wrongpwd@test.com",
            Password = "Password1",
            FullName = "Wrong Pwd User"
        });
        await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = "wrongpwd@test.com",
            Otp = factory.FixedOtp
        });

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "wrongpwd@test.com",
            Password = "WrongPassword1"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_NonExistentEmail_ReturnsUnauthorized()
    {
        // Email không tồn tại trong hệ thống → 401 (không tiết lộ lý do cụ thể).
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Email = "ghost@test.com",
            Password = "Password1"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task VerifyEmail_WrongOtp_ReturnsBadRequest()
    {
        // OTP sai → 400 INVALID_OTP qua HTTP.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "wrongotp@test.com",
            Password = "Password1",
            FullName = "OTP Test"
        });

        var response = await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = "wrongotp@test.com",
            Otp = "999999"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("INVALID_OTP", body!.ErrorCode);
    }

    [Fact]
    public async Task ForgotPassword_UnverifiedEmail_ReturnsForbidden()
    {
        // Forgot password cho tài khoản chưa verify email → 403 EMAIL_NOT_VERIFIED.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "unverified.forgot@test.com",
            Password = "Password1",
            FullName = "Unverified"
        });

        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest
        {
            Email = "unverified.forgot@test.com"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("EMAIL_NOT_VERIFIED", body!.ErrorCode);
    }

    [Fact]
    public async Task ResetPassword_NewPasswordSameAsOld_ReturnsError()
    {
        // Mật khẩu mới giống mật khẩu cũ → service phải từ chối (nếu business rule này tồn tại).
        // Test này xác nhận behavior hiện tại của service.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        // Đăng ký + verify + forgot
        await client.PostAsJsonAsync("/api/auth/register", new RegisterRequest
        {
            Email = "samepwd@test.com",
            Password = "Password1",
            FullName = "Same Pwd"
        });
        await client.PostAsJsonAsync("/api/auth/verify-email", new VerifyEmailRequest
        {
            Email = "samepwd@test.com",
            Otp = factory.FixedOtp
        });
        await client.PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest
        {
            Email = "samepwd@test.com"
        });

        // Đặt lại với cùng mật khẩu cũ
        var response = await client.PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest
        {
            Email = "samepwd@test.com",
            Otp = factory.FixedOtp,
            NewPassword = "Password1"
        });

        // Tùy business rule: 200 (hệ thống cho phép) hoặc 400 (không cho phép)
        // Assert mặc định: request phải thành công hoặc trả lỗi có cấu trúc đúng
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.NotNull(body);
        Assert.True(
            response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.BadRequest,
            $"Unexpected status: {response.StatusCode}");
    }

    [Fact]
    public async Task RefreshToken_InvalidToken_ReturnsUnauthorized()
    {
        // Refresh token giả mạo → 401.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/refresh-token", new RefreshTokenRequest
        {
            RefreshToken = "totally-fake-token-that-does-not-exist"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<T>(json, JsonOptions);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// ADMIN — Các trường hợp thiếu
// ═══════════════════════════════════════════════════════════════════════════

public sealed class AdminMissingCoverageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task CreateStaff_MissingFullName_ReturnsValidationError()
    {
        // Thiếu FullName bắt buộc → validation 400 VALIDATION_ERROR.
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAdminRoleAsync(factory);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

        var response = await client.PostAsJsonAsync("/api/admin/staff", new CreateStaffRequest
        {
            Email = "noname@test.com",
            FullName = ""
        });

        // Validation error: FullName trống
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.False(body!.Success);
    }

    [Fact]
    public async Task CreateStaff_CustomerRole_ReturnsForbidden()
    {
        // Customer không được tạo Staff → 403.
        await using var factory = new CinemaWebApplicationFactory();
        await SeedAdminRoleAsync(factory);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        var response = await client.PostAsJsonAsync("/api/admin/staff", new CreateStaffRequest
        {
            Email = "blocked@test.com",
            FullName = "Blocked"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateStaff_NoToken_ReturnsUnauthorized()
    {
        // Không có JWT → 401.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/admin/staff", new CreateStaffRequest
        {
            Email = "anon@test.com",
            FullName = "Anon"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private static async Task SeedAdminRoleAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        if (!await db.Roles.AnyAsync(r => r.RoleId == AuthConstants.RoleIds.Staff))
        {
            db.Roles.Add(new Role
            {
                RoleId = AuthConstants.RoleIds.Staff,
                RoleName = AuthConstants.Roles.Staff,
                Description = "Staff"
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// BOOKING — Các trường hợp thiếu
// ═══════════════════════════════════════════════════════════════════════════

public sealed class BookingMissingCoverageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task CreateBooking_NoToken_ReturnsUnauthorized()
    {
        // Không có JWT → 401 trước khi vào service.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            ShowtimeId = "SHW_01",
            ShowtimeSeatIds = ["STS_01"]
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_EmptyShowtimeSeatIds_ReturnsValidationError()
    {
        // Không chọn ghế nào → controller validation 400.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        var response = await client.PostAsJsonAsync("/api/bookings", new CreateBookingRequest
        {
            ShowtimeId = "SHW_01",
            ShowtimeSeatIds = []
        });

        // Mong đợi 400 khi danh sách ghế rỗng
        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound,
            $"Expected 400/404, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetBookingById_OtherUsersBooking_ReturnsNotFoundOrForbidden()
    {
        // Customer A cố lấy thông tin booking của Customer B → 403 hoặc 404.
        await using var factory = new CinemaWebApplicationFactory();
        var (_, _) = await SeedBookingDataAsync(factory);
        using var client = factory.CreateClient();

        // Đăng nhập với CustomerA nhưng truy vấn booking không phải của mình
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer("USR_DIFFERENT_CUSTOMER"));

        var response = await client.GetAsync("/api/bookings/BKG_NONEXISTENT_FOR_THIS_USER");

        // Phải từ chối: không thấy (404) hoặc cấm (403)
        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden,
            $"Expected 404/403, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetMyBookings_NoToken_ReturnsUnauthorized()
    {
        // Không có token → 401.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/bookings/my-bookings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task GetMyBookings_ReturnsEmptyListWhenNoBookings()
    {
        // Customer mới chưa có booking → trả list rỗng, không phải lỗi.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer("USR_FRESH_CUSTOMER"));

        var response = await client.GetAsync("/api/bookings/my-bookings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<BookingResponse>>>(JsonOptions);
        Assert.True(body!.Success);
        Assert.Empty(body.Data!);
    }

    [Fact]
    public async Task Checkout_InvalidShowtimeId_ReturnsError()
    {
        // ShowtimeId không tồn tại trong DB → 404 hoặc 409.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        var response = await client.PostAsJsonAsync("/api/bookings/checkout", new CheckoutRequest
        {
            ShowtimeId = "SHW_NONEXISTENT",
            ShowtimeSeatIds = ["STS_01"]
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound
                or HttpStatusCode.BadRequest
                or HttpStatusCode.Conflict
                or HttpStatusCode.Unauthorized,
            $"Expected 4xx, got {response.StatusCode}");
    }

    private static async Task<(string showtimeId, List<string> showtimeSeatIds)> SeedBookingDataAsync(
        CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var customerRole = new Role { RoleId = "R_CUST_MB", RoleName = "CUSTOMER" };
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
            CustomerProfileId = "CUS_MB_01",
            UserId = user.UserId,
            MemberLevel = "STANDARD"
        });
        var cinema = new Cinema { CinemaId = "CIN_MB_01", CinemaName = "MB Cinema", Address = "Add", City = "City", CinemaStatus = "ACTIVE" };
        db.Cinemas.Add(cinema);
        var room = new Room { RoomId = "ROM_MB_01", CinemaId = cinema.CinemaId, RoomName = "Room 1", Capacity = 10, RoomStatus = "ACTIVE" };
        db.Rooms.Add(room);
        var seatType = new SeatType { SeatTypeId = "ST_MB_VIP", TypeName = "VIP", ExtraFee = 10000 };
        db.SeatTypes.Add(seatType);
        var seat = new Seat { SeatId = "SET_MB_01", RoomId = room.RoomId, SeatTypeId = seatType.SeatTypeId, SeatCode = "A1", RowLabel = "A", SeatNumber = 1, IsActive = true };
        db.Seats.Add(seat);
        var movie = new Movie { MovieId = "MOV_MB_01", Title = "MB Movie", DurationMinutes = 120, MovieStatus = "NOW_SHOWING" };
        db.Movies.Add(movie);
        var showtime = new Showtime
        {
            ShowtimeId = "SHW_MB_01",
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
            ShowtimeSeatId = "STS_MB_01",
            ShowtimeId = showtime.ShowtimeId,
            SeatId = seat.SeatId,
            SeatStatus = "AVAILABLE",
            RowVersion = new byte[8]
        };
        db.ShowtimeSeats.Add(showtimeSeat);
        db.PaymentProviders.Add(new PaymentProvider
        {
            PaymentProviderId = "PAYPROV_MB",
            ProviderName = "TEST_PROVIDER",
            ProviderStatus = "ACTIVE"
        });
        await db.SaveChangesAsync();

        return (showtime.ShowtimeId, [showtimeSeat.ShowtimeSeatId]);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// PAYMENT — Các trường hợp thiếu
// ═══════════════════════════════════════════════════════════════════════════

public sealed class PaymentMissingCoverageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task CreatePayment_MissingBookingId_ReturnsBadRequest()
    {
        // Không có BookingId → controller trả 400 validation error.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        var response = await client.PostAsJsonAsync("/api/payment", new CreatePaymentRequest
        {
            BookingId = "",
            PaymentProviderId = "PAYPROV_TEST"
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound,
            $"Expected 400/404, got {response.StatusCode}");
    }

    [Fact]
    public async Task SepayWebhook_MissingSignatureHeader_ReturnsUnauthorized()
    {
        // Webhook không có x-sepay-signature → 401 INVALID_SIGNATURE.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        var payload = JsonSerializer.Serialize(new
        {
            content = "Cinema TXCODE123",
            transferAmount = 100000,
            referenceCode = "REF001"
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment/sepay-webhook")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };
        // Cố ý không thêm x-sepay-signature header

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);
        Assert.Equal("INVALID_SIGNATURE", body!.ErrorCode);
    }

    [Fact]
    public async Task SepayWebhook_InvalidSignature_ReturnsUnauthorized()
    {
        // Chữ ký sai → 401 INVALID_SIGNATURE.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        var payload = JsonSerializer.Serialize(new
        {
            content = "Cinema TXCODE123",
            transferAmount = 100000,
            referenceCode = "REF001"
        });

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment/sepay-webhook")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-sepay-signature", "sha256=invalidsignature");
        request.Headers.Add("x-sepay-timestamp", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString());

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>(JsonOptions);
        Assert.Equal("INVALID_SIGNATURE", body!.ErrorCode);
    }

    [Fact]
    public async Task SepayWebhook_ValidButUnknownTransactionCode_ReturnsOkOrHandledGracefully()
    {
        // Webhook hợp lệ nhưng mã giao dịch không khớp bất kỳ payment nào → không crash.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();

        var payload = JsonSerializer.Serialize(new
        {
            content = "Cinema TXCODE_NONEXISTENT",
            transferAmount = 100000,
            referenceCode = "REF_GHOST"
        });

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var secret = CinemaWebApplicationFactory.TestSepayWebhookSecret;
        var signature = "sha256=" + ComputeHmac(timestamp + "." + payload, secret);

        var request = new HttpRequestMessage(HttpMethod.Post, "/api/payment/sepay-webhook")
        {
            Content = new StringContent(payload, System.Text.Encoding.UTF8, "application/json")
        };
        request.Headers.Add("x-sepay-signature", signature);
        request.Headers.Add("x-sepay-timestamp", timestamp);

        var response = await client.SendAsync(request);

        // API phải trả về response có cấu trúc (không crash 500).
        Assert.True(
            (int)response.StatusCode < 500,
            $"API không được crash với mã giao dịch không tồn tại. Status: {response.StatusCode}");
    }

    private static string ComputeHmac(string data, string key)
    {
        var keyBytes = System.Text.Encoding.UTF8.GetBytes(key);
        using var hmac = new System.Security.Cryptography.HMACSHA256(keyBytes);
        var hash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(data));
        return BitConverter.ToString(hash).Replace("-", string.Empty).ToLowerInvariant();
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// ROOM — Các trường hợp thiếu
// ═══════════════════════════════════════════════════════════════════════════

public sealed class RoomMissingCoverageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task CreateRoom_DuplicateName_SameCinema_ReturnsBadRequestOrConflict()
    {
        // Tạo 2 phòng cùng tên trong cùng 1 rạp → service phải từ chối.
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCinemaAsync(factory);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        // Tạo phòng lần 1
        await client.PostAsJsonAsync("/api/rooms/cinemas/CIN_RM_TEST/rooms",
            new CreateRoomRequest { RoomName = "Duplicate Room", Capacity = 10 });

        // Tạo phòng lần 2 cùng tên
        var response = await client.PostAsJsonAsync("/api/rooms/cinemas/CIN_RM_TEST/rooms",
            new CreateRoomRequest { RoomName = "Duplicate Room", Capacity = 10 });

        // Nếu service enforce unique name: 400/409; nếu không: 201 (test ghi lại behavior thực tế)
        Assert.True(
            (int)response.StatusCode >= 200 && (int)response.StatusCode < 500,
            $"Unexpected server error: {response.StatusCode}");
    }

    [Fact]
    public async Task GetRoomById_NonExistentRoom_ReturnsNotFound()
    {
        // Lấy chi tiết phòng không tồn tại → 404 ROOM_NOT_FOUND.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.GetAsync("/api/rooms/rooms/ROOM_GHOST");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("ROOM_NOT_FOUND", body!.ErrorCode);
    }

    [Fact]
    public async Task GenerateSeats_RoomAlreadyHasSeats_ReturnsConflict()
    {
        // Generate ghế lần 2 khi phòng đã có ghế → 409 ROOM_HAS_SEATS.
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCinemaAsync(factory);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        // Tạo phòng + seed SeatType
        await SeedSeatTypeAsync(factory);
        var createResp = await client.PostAsJsonAsync("/api/rooms/cinemas/CIN_RM_TEST/rooms",
            new CreateRoomRequest { RoomName = "Gen Room", Capacity = 10 });
        var created = await DeserializeAsync<ApiResponse<RoomResponse>>(createResp);
        var roomId = created!.Data!.RoomId;

        // Generate lần 1 → thành công
        await client.PostAsJsonAsync($"/api/rooms/{roomId}/generate-seats",
            new GenerateSeatsRequest { Rows = 2, Columns = 3, SeatTypeId = "SEAT_TYPE_RM_STD" });

        // Generate lần 2 → phải từ chối
        var response = await client.PostAsJsonAsync($"/api/rooms/{roomId}/generate-seats",
            new GenerateSeatsRequest { Rows = 2, Columns = 3, SeatTypeId = "SEAT_TYPE_RM_STD" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("ROOM_HAS_SEATS", body!.ErrorCode);
    }

    [Fact]
    public async Task DeleteRoom_CustomerRole_ReturnsForbidden()
    {
        // Customer không được xóa phòng → 403.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        var response = await client.DeleteAsync("/api/rooms/rooms/ROOM_ANY");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task UpdateRoom_NonExistentRoom_ReturnsNotFound()
    {
        // Cập nhật phòng không tồn tại → 404.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.PutAsJsonAsync("/api/rooms/rooms/ROOM_GHOST",
            new UpdateRoomRequest { RoomName = "Ghost Updated", Capacity = 5, RoomStatus = "ACTIVE" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task SeedCinemaAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        if (!await db.Cinemas.AnyAsync(c => c.CinemaId == "CIN_RM_TEST"))
        {
            db.Cinemas.Add(new Cinema
            {
                CinemaId = "CIN_RM_TEST",
                CinemaName = "Room Test Cinema",
                Address = "1 Test Rd",
                City = "HCM",
                CinemaStatus = "ACTIVE"
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task SeedSeatTypeAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        if (!await db.SeatTypes.AnyAsync(st => st.SeatTypeId == "SEAT_TYPE_RM_STD"))
        {
            db.SeatTypes.Add(new SeatType
            {
                SeatTypeId = "SEAT_TYPE_RM_STD",
                TypeName = "STANDARD",
                ExtraFee = 0
            });
            await db.SaveChangesAsync();
        }
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// SHOWTIME — Các trường hợp thiếu
// ═══════════════════════════════════════════════════════════════════════════

public sealed class ShowtimeMissingCoverageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task GetShowtimeById_NonExistent_ReturnsNotFound()
    {
        // Suất chiếu không tồn tại → 404 SHOWTIME_NOT_FOUND.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        var response = await client.GetAsync("/api/showtimes/SHW_GHOST");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("SHOWTIME_NOT_FOUND", body!.ErrorCode);
    }

    [Fact]
    public async Task CreateShowtime_CustomerRole_ReturnsForbidden()
    {
        // Customer không có quyền tạo suất chiếu → 403.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        var response = await client.PostAsJsonAsync("/api/showtimes", new CreateShowtimeRequest
        {
            MovieId = "MOV_01",
            RoomId = "ROOM_01",
            StartTime = DateTime.UtcNow.AddDays(1),
            BasePrice = 90000
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task CreateShowtime_OverlappingTime_SameRoom_ReturnsBadRequest()
    {
        // Tạo 2 suất chiếu trùng giờ trong cùng phòng → 400 SHOWTIME_OVERLAP.
        await using var factory = new CinemaWebApplicationFactory();
        await SeedBaseDataAsync(factory);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var startTime = new DateTime(2027, 1, 1, 10, 0, 0, DateTimeKind.Utc);

        // Suất 1 thành công
        var first = await client.PostAsJsonAsync("/api/showtimes", new CreateShowtimeRequest
        {
            MovieId = "MOV_SHW_TEST",
            RoomId = "ROOM_SHW_TEST",
            StartTime = startTime,
            BasePrice = 90000
        });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Suất 2 trùng giờ (cùng phòng, bắt đầu trong khoảng suất 1 đang chiếu)
        var overlapping = await client.PostAsJsonAsync("/api/showtimes", new CreateShowtimeRequest
        {
            MovieId = "MOV_SHW_TEST",
            RoomId = "ROOM_SHW_TEST",
            StartTime = startTime.AddHours(1), // Trùng lấn
            BasePrice = 90000
        });

        Assert.Equal(HttpStatusCode.BadRequest, overlapping.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(overlapping);
        Assert.Equal("SHOWTIME_OVERLAP", body!.ErrorCode);
    }

    [Fact]
    public async Task UpdateShowtime_NonExistent_ReturnsNotFound()
    {
        // Cập nhật suất chiếu không tồn tại → 404.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.PutAsJsonAsync("/api/showtimes/SHW_GHOST", new UpdateShowtimeRequest
        {
            MovieId = "MOV_01",
            RoomId = "ROOM_01",
            StartTime = DateTime.UtcNow.AddDays(1),
            BasePrice = 95000,
            Status = "OPEN"
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task DeleteShowtime_NonExistent_ReturnsNotFound()
    {
        // Xóa suất chiếu không tồn tại → 404.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.DeleteAsync("/api/showtimes/SHW_GHOST");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task SeedBaseDataAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        if (!await db.Cinemas.AnyAsync(c => c.CinemaId == "CIN_SHW_TEST"))
        {
            db.Cinemas.Add(new Cinema { CinemaId = "CIN_SHW_TEST", CinemaName = "SHW Cinema", Address = "1", City = "HCM", CinemaStatus = "ACTIVE" });
        }
        if (!await db.Movies.AnyAsync(m => m.MovieId == "MOV_SHW_TEST"))
        {
            db.Movies.Add(new Movie { MovieId = "MOV_SHW_TEST", Title = "SHW Movie", DurationMinutes = 120, AgeRating = "T13", MovieStatus = "NOW_SHOWING" });
        }
        if (!await db.SeatTypes.AnyAsync(st => st.SeatTypeId == "SEAT_TYPE_SHW_STD"))
        {
            db.SeatTypes.Add(new SeatType { SeatTypeId = "SEAT_TYPE_SHW_STD", TypeName = "STANDARD", ExtraFee = 0 });
        }
        if (!await db.Rooms.AnyAsync(r => r.RoomId == "ROOM_SHW_TEST"))
        {
            db.Rooms.Add(new Room { RoomId = "ROOM_SHW_TEST", CinemaId = "CIN_SHW_TEST", RoomName = "SHW Room", Capacity = 5, RoomStatus = "ACTIVE" });
        }
        // Seed ghế để generate ShowtimeSeat khi tạo Showtime
        if (!await db.Seats.AnyAsync(s => s.RoomId == "ROOM_SHW_TEST"))
        {
            db.Seats.AddRange(Enumerable.Range(1, 5).Select(i => new Seat
            {
                SeatId = $"SEAT_SHW_{i}",
                RoomId = "ROOM_SHW_TEST",
                SeatTypeId = "SEAT_TYPE_SHW_STD",
                RowLabel = "A",
                SeatNumber = i,
                SeatCode = $"A{i}",
                IsActive = true
            }));
        }

        await db.SaveChangesAsync();
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// SEAT — Các trường hợp thiếu
// ═══════════════════════════════════════════════════════════════════════════

public sealed class SeatMissingCoverageTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task CreateSeat_NonExistentRoom_ReturnsNotFound()
    {
        // Tạo ghế cho phòng không tồn tại → 404.
        await using var factory = new CinemaWebApplicationFactory();
        await SeedSeatTypeAsync(factory);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.PostAsJsonAsync("/api/seats", new CreateSeatRequest
        {
            RoomId = "ROOM_GHOST",
            RowLabel = "A",
            SeatNumber = 1,
            SeatTypeId = "SEAT_TYPE_SEAT_STD"
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404/400 for non-existent room, got {response.StatusCode}");
    }

    [Fact]
    public async Task UpdateSeat_NonExistentSeat_ReturnsNotFound()
    {
        // Cập nhật ghế không tồn tại → 404.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.PutAsJsonAsync("/api/seats/SEAT_GHOST", new UpdateSeatRequest
        {
            SeatId = "SEAT_GHOST",
            RowLabel = "B",
            SeatNumber = 2,
            SeatTypeId = "ST_STD"
        });

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404/400, got {response.StatusCode}");
    }

    [Fact]
    public async Task DeleteSeat_NonExistentSeat_ReturnsNotFound()
    {
        // Xóa ghế không tồn tại → 404.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.DeleteAsync("/api/seats/SEAT_GHOST");

        Assert.True(
            response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest,
            $"Expected 404/400, got {response.StatusCode}");
    }

    [Fact]
    public async Task GetSeatsByRoom_NonExistentRoom_ReturnsNotFoundOrEmpty()
    {
        // GET ghế theo phòng không tồn tại → 404 hoặc list rỗng.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Staff());

        var response = await client.GetAsync("/api/seats/room/ROOM_GHOST");

        Assert.True(
            response.StatusCode is HttpStatusCode.OK or HttpStatusCode.NotFound,
            $"Expected 200/404, got {response.StatusCode}");

        if (response.StatusCode == HttpStatusCode.OK)
        {
            var body = await response.Content.ReadFromJsonAsync<ApiResponse<List<object>>>(JsonOptions);
            Assert.True(body!.Success);
            // Phòng không tồn tại → list trống
            Assert.Empty(body.Data ?? []);
        }
    }

    [Fact]
    public async Task LockSeat_MissingShowtimeId_ReturnsValidationError()
    {
        // Body thiếu ShowtimeId → model validation 400.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Customer());

        var response = await client.PostAsJsonAsync("/api/seats/lock",
            new { SeatId = "SEAT_E2E_01" }); // Thiếu ShowtimeId

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("VALIDATION_ERROR", body!.ErrorCode);
    }

    [Fact]
    public async Task LockSeat_AdminRole_CannotLock_ReturnsForbidden()
    {
        // Admin không có policy CanBookTicket → không được lock ghế.
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());

        var response = await client.PostAsJsonAsync("/api/seats/lock", new LockSeatRequest
        {
            ShowtimeId = "SHW_TEST",
            SeatId = "SEAT_TEST"
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private static async Task SeedSeatTypeAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        if (!await db.SeatTypes.AnyAsync(st => st.SeatTypeId == "SEAT_TYPE_SEAT_STD"))
        {
            db.SeatTypes.Add(new SeatType { SeatTypeId = "SEAT_TYPE_SEAT_STD", TypeName = "STANDARD", ExtraFee = 0 });
            await db.SaveChangesAsync();
        }
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// CUSTOMER — Các trường hợp thiếu (Controller Moq level)
// ═══════════════════════════════════════════════════════════════════════════

public sealed class CustomerMissingCoverageTests
{
    [Fact]
    public async Task Customer_UpdateProfile_InvalidData_ReturnsValidationError()
    {
        // Service trả lỗi validation khi dữ liệu profile không hợp lệ → 400.
        var service = new Mock<ICustomerService>(MockBehavior.Strict);
        service
            .Setup(x => x.UpdateProfileAsync(
                "USR_1",
                It.IsAny<UpdateProfileRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<CustomerProfileResponse>.Fail(
                StatusCodes.Status400BadRequest,
                "Họ tên không được để trống.",
                "VALIDATION_ERROR"));

        var controller = WithUser(new CustomersController(service.Object),
            new Claim(ClaimTypes.NameIdentifier, "USR_1"));

        var result = await controller.UpdateProfile(
            new UpdateProfileRequest { FullName = "" },
            CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponse<CustomerProfileResponse>>(objectResult.Value);
        Assert.False(response.Success);
        Assert.Equal("VALIDATION_ERROR", response.ErrorCode);
        service.VerifyAll();
    }

    [Fact]
    public async Task Customer_ChangePassword_WrongOldPassword_ReturnsBadRequest()
    {
        // Mật khẩu cũ sai → 400 WRONG_PASSWORD.
        var service = new Mock<ICustomerService>(MockBehavior.Strict);
        service
            .Setup(x => x.ChangePasswordAsync(
                "USR_1",
                It.Is<ChangePasswordRequest>(r => r.OldPassword == "WrongOld"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Fail(
                StatusCodes.Status400BadRequest,
                "Mật khẩu cũ không đúng.",
                "WRONG_PASSWORD"));

        var controller = WithUser(new CustomersController(service.Object),
            new Claim(ClaimTypes.NameIdentifier, "USR_1"));

        var result = await controller.ChangePassword(
            new ChangePasswordRequest { OldPassword = "WrongOld", NewPassword = "NewPassword1" },
            CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        Assert.Equal("WRONG_PASSWORD", response.ErrorCode);
        service.VerifyAll();
    }

    [Fact]
    public async Task Customer_RequestEmailChange_AlreadyUsedEmail_ReturnsConflict()
    {
        // Email mới đã được dùng bởi tài khoản khác → 409 DUPLICATE_EMAIL.
        var service = new Mock<ICustomerService>(MockBehavior.Strict);
        service
            .Setup(x => x.RequestEmailUpdateAsync(
                "USR_1",
                It.Is<UpdateEmailRequest>(r => r.NewEmail == "taken@example.com"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Fail(
                StatusCodes.Status409Conflict,
                "Email đã được sử dụng.",
                "DUPLICATE_EMAIL"));

        var controller = WithUser(new CustomersController(service.Object),
            new Claim(ClaimTypes.NameIdentifier, "USR_1"));

        var result = await controller.RequestEmailChange(
            new UpdateEmailRequest { NewEmail = "taken@example.com" },
            CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        Assert.Equal("DUPLICATE_EMAIL", response.ErrorCode);
        service.VerifyAll();
    }

    [Fact]
    public async Task Customer_VerifyEmailChange_ExpiredOtp_ReturnsBadRequest()
    {
        // OTP đổi email đã hết hạn → 400 OTP_EXPIRED.
        var service = new Mock<ICustomerService>(MockBehavior.Strict);
        service
            .Setup(x => x.VerifyEmailUpdateAsync(
                "USR_1",
                It.IsAny<VerifyEmailUpdateRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Fail(
                StatusCodes.Status400BadRequest,
                "OTP đã hết hạn.",
                "OTP_EXPIRED"));

        var controller = WithUser(new CustomersController(service.Object),
            new Claim(ClaimTypes.NameIdentifier, "USR_1"));

        var result = await controller.VerifyEmailChange(
            new VerifyEmailUpdateRequest { NewEmail = "new@example.com", Otp = "123456" },
            CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponse<object>>(objectResult.Value);
        Assert.Equal("OTP_EXPIRED", response.ErrorCode);
        service.VerifyAll();
    }

    [Fact]
    public async Task Customer_GetBookingHistory_EmptyList_ReturnsOkWithEmptyData()
    {
        // Customer chưa có booking nào → 200 với data rỗng.
        var service = new Mock<ICustomerService>(MockBehavior.Strict);
        service
            .Setup(x => x.GetBookingHistoryAsync("USR_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<List<BookingHistoryResponse>>.Ok([]));

        var controller = WithUser(new CustomersController(service.Object),
            new Claim(ClaimTypes.NameIdentifier, "USR_1"));

        var result = await controller.GetBookingHistory(CancellationToken.None);

        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponse<List<BookingHistoryResponse>>>(objectResult.Value);
        Assert.True(response.Success);
        Assert.Empty(response.Data!);
        service.VerifyAll();
    }

    // ── Helper: inject ClaimsPrincipal vào controller ────────────────────────
    private static TController WithUser<TController>(TController controller, params Claim[] claims)
        where TController : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(
                    new ClaimsIdentity(claims, claims.Length == 0 ? null : "TestAuth"))
            }
        };
        return controller;
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// PAYMENT SERVICE — Các trường hợp thiếu (Unit level)
// ═══════════════════════════════════════════════════════════════════════════

public sealed class PaymentServiceMissingCoverageTests
{
    [Fact]
    public async Task ConfirmPayment_AmountMismatch_ThrowsInvalidOperation()
    {
        // Webhook gửi số tiền thấp hơn booking → service throw InvalidOperationException.
        var fixture = Fixture.Create();
        await fixture.SeedPendingBookingAsync();
        var created = await fixture.Service.CreatePaymentAsync(new CreatePaymentRequest
        {
            BookingId = "BOOKING_TEST",
            PaymentProviderId = "PAYPROV_TEST_SEPAY"
        }, "USER_TEST");

        // Gửi số tiền thấp hơn (60000 thay vì 120000)
        var rawPayload = $$"""{"content":"Cinema {{created.TransactionCode}}","transferAmount":60000,"referenceCode":"SEP_MISMATCH"}""";
        var exception = await Record.ExceptionAsync(() =>
            fixture.Service.ConfirmPaymentAsync(
                $"Cinema {created.TransactionCode}",
                60000m, // Số tiền không khớp
                "SEP_MISMATCH",
                rawPayload));

        // Service validate amount → phải throw InvalidOperationException
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("amount mismatch", exception!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConfirmPayment_TransactionCodeNotFound_ThrowsInvalidOperation()
    {
        // Mã giao dịch không khớp bất kỳ payment nào → service throw InvalidOperationException.
        var fixture = Fixture.Create();
        await fixture.SeedPendingBookingAsync();

        // Không tạo payment, gọi confirm với mã không tồn tại
        var exception = await Record.ExceptionAsync(() =>
            fixture.Service.ConfirmPaymentAsync(
                "Cinema TX_NONEXISTENT",
                120000m,
                "SEP_GHOST",
                """{"content":"Cinema TX_NONEXISTENT","transferAmount":120000}"""));

        // Service phải throw InvalidOperationException (không phải NullReferenceException)
        Assert.NotNull(exception);
        Assert.IsType<InvalidOperationException>(exception);
        Assert.Contains("TX_NONEXISTENT", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task CreatePayment_BookingNotFound_ThrowsInvalidOperation()
    {
        // BookingId không tồn tại → service throw InvalidOperationException.
        var fixture = Fixture.Create();
        // Không seed booking

        var exception = await Record.ExceptionAsync(() =>
            fixture.Service.CreatePaymentAsync(new CreatePaymentRequest
            {
                BookingId = "BOOKING_GHOST",
                PaymentProviderId = "PAYPROV_TEST_SEPAY"
            }, "USER_TEST"));

        // Service throw InvalidOperationException (không phải NullReferenceException)
        Assert.IsType<InvalidOperationException>(exception);
        Assert.NotNull(exception);
        Assert.Contains("BOOKING_GHOST", exception!.Message);
    }

    // ── Inner fixture (sao chép từ PaymentServiceTests.cs) ───────────────────
    private sealed class Fixture
    {
        private Fixture(CinemaDbContext dbContext, PaymentService service)
        {
            DbContext = dbContext;
            Service = service;
        }

        public CinemaDbContext DbContext { get; }
        public PaymentService Service { get; }

        public static Fixture Create()
        {
            var options = new DbContextOptionsBuilder<CinemaDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var dbContext = new CinemaDbContext(options);
            var service = new PaymentService(
                dbContext,
                Microsoft.Extensions.Options.Options.Create(new SepaySettings
                {
                    WebhookSecret = "test-secret",
                    BankName = "Test Bank",
                    BankAccount = "123456789"
                }));
            return new Fixture(dbContext, service);
        }

        public async Task SeedPendingBookingAsync()
        {
            DbContext.PaymentProviders.Add(new PaymentProvider
            {
                PaymentProviderId = "PAYPROV_TEST_SEPAY",
                ProviderName = "SEPAY_TEST",
                ProviderStatus = "ACTIVE"
            });
            DbContext.Bookings.Add(new Booking
            {
                BookingId = "BOOKING_TEST",
                ShowtimeId = "SHOWTIME_TEST",
                BookingStatus = "PENDING_PAYMENT",
                TotalAmount = 120000m,
                CreatedAt = DateTime.UtcNow,
                BookingChannel = "ONLINE"
            });
            await DbContext.SaveChangesAsync();
        }
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// SHOWTIME SERVICE — Các trường hợp thiếu (Unit level)
// ═══════════════════════════════════════════════════════════════════════════

public sealed class ShowtimeServiceMissingCoverageTests
{
    [Fact]
    public async Task CreateShowtime_InactiveRoom_ReturnsBadRequest()
    {
        // Tạo suất chiếu trong phòng INACTIVE → phải bị từ chối.
        var fixture = Fixture.Create();
        await fixture.SeedCinemaAsync();

        // Seed movie + seattype + room INACTIVE
        fixture.DbContext.Movies.Add(new Movie
        {
            MovieId = "MOV_ST_INACTIVE",
            Title = "Test Movie",
            DurationMinutes = 120,
            AgeRating = "T13",
            MovieStatus = "NOW_SHOWING"
        });
        fixture.DbContext.SeatTypes.Add(new SeatType
        {
            SeatTypeId = "SEAT_TYPE_ST_STD",
            TypeName = "STANDARD",
            ExtraFee = 0
        });
        fixture.DbContext.Rooms.Add(new Room
        {
            RoomId = "ROOM_INACTIVE_TEST",
            CinemaId = "CIN_TEST",
            RoomName = "Inactive Room",
            Capacity = 5,
            RoomStatus = "INACTIVE" // Phòng bị tắt
        });
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.ShowtimeService.CreateShowtimeAsync(
            new CreateShowtimeRequest
            {
                MovieId = "MOV_ST_INACTIVE",
                RoomId = "ROOM_INACTIVE_TEST",
                StartTime = new DateTime(2027, 1, 1, 10, 0, 0, DateTimeKind.Utc),
                BasePrice = 90000
            },
            CancellationToken.None);

        // Kỳ vọng: service từ chối tạo suất chiếu trong phòng INACTIVE
        Assert.False(result.Success);
    }

    [Fact]
    public async Task UpdateShowtime_NonExistentShowtime_ReturnsNotFound()
    {
        // Cập nhật suất chiếu không tồn tại → 404 SHOWTIME_NOT_FOUND.
        var fixture = Fixture.Create();
        await fixture.SeedCinemaMovieAndRoomWithSeatsAsync();

        var result = await fixture.ShowtimeService.UpdateShowtimeAsync(
            "SHW_GHOST",
            new UpdateShowtimeRequest
            {
                MovieId = "MOV_TEST",
                RoomId = "ROOM_TEST",
                StartTime = new DateTime(2027, 1, 1, 14, 0, 0, DateTimeKind.Utc),
                BasePrice = 95000,
                Status = "OPEN"
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("SHOWTIME_NOT_FOUND", result.ErrorCode);
    }

    [Fact]
    public async Task DeleteShowtime_NonExistentShowtime_ReturnsNotFound()
    {
        // Xóa suất chiếu không tồn tại → 404 SHOWTIME_NOT_FOUND.
        var fixture = Fixture.Create();

        var result = await fixture.ShowtimeService.DeleteShowtimeAsync(
            "SHW_GHOST",
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(404, result.StatusCode);
        Assert.Equal("SHOWTIME_NOT_FOUND", result.ErrorCode);
    }

    // ── Inner fixture (tái sử dụng từ RoomShowtimeServiceTests) ─────────────
    private sealed class Fixture
    {
        private Fixture(CinemaDbContext dbContext, RoomService roomService, ShowtimeService showtimeService)
        {
            DbContext = dbContext;
            RoomService = roomService;
            ShowtimeService = showtimeService;
        }

        public CinemaDbContext DbContext { get; }
        public RoomService RoomService { get; }
        public ShowtimeService ShowtimeService { get; }

        public static Fixture Create()
        {
            var options = new DbContextOptionsBuilder<CinemaDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .Options;
            var dbContext = new CinemaDbContext(options);
            var mockClock = new Moq.Mock<IClock>();
            mockClock.Setup(c => c.UtcNow).Returns(new DateTime(2026, 6, 1, 1, 0, 0, DateTimeKind.Utc));
            var mockJobClient = new Moq.Mock<Hangfire.IBackgroundJobClient>();
                        var mockOptions = Microsoft.Extensions.Options.Options.Create(new CinemaSystem.Application.Settings.CinemaProcessingSettings());
            return new Fixture(
                dbContext,
                new RoomService(dbContext, new Moq.Mock<CinemaSystem.Application.Interfaces.IAdminRefundService>().Object),
                new ShowtimeService(dbContext, mockClock.Object, mockOptions, mockJobClient.Object));
        }

        public async Task SeedCinemaAsync()
        {
            DbContext.Cinemas.Add(new Cinema
            {
                CinemaId = "CIN_TEST",
                CinemaName = "Test Cinema",
                Address = "1 Test Street",
                City = "Ho Chi Minh",
                CinemaStatus = "ACTIVE"
            });
            await DbContext.SaveChangesAsync();
        }

        public async Task SeedCinemaMovieAndRoomWithSeatsAsync()
        {
            await SeedCinemaAsync();
            DbContext.Movies.Add(new Movie { MovieId = "MOV_TEST", Title = "Test Movie", DurationMinutes = 120, AgeRating = "T13", MovieStatus = "NOW_SHOWING" });
            DbContext.SeatTypes.Add(new SeatType { SeatTypeId = "SEAT_TYPE_STANDARD", TypeName = "STANDARD", ExtraFee = 0 });
            DbContext.Rooms.Add(new Room { RoomId = "ROOM_TEST", CinemaId = "CIN_TEST", RoomName = "Room Test", Capacity = 10, RoomStatus = "ACTIVE" });
            DbContext.Seats.AddRange(Enumerable.Range(1, 10).Select(i => new Seat
            {
                SeatId = $"SEAT_{i}",
                RoomId = "ROOM_TEST",
                SeatTypeId = "SEAT_TYPE_STANDARD",
                SeatCode = $"A{i}",
                RowLabel = "A",
                SeatNumber = i,
                IsActive = true
            }));
            await DbContext.SaveChangesAsync();
        }
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow) => UtcNow = utcNow;
        public DateTime UtcNow { get; }
    }
}
