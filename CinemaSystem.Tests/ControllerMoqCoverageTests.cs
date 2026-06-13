using System.Security.Claims;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Auth;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Contracts.Cinemas;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Customers;
using CinemaSystem.Contracts.Movies;
using CinemaSystem.Contracts.Payments;
using CinemaSystem.Contracts.Rooms;
using CinemaSystem.Contracts.Seats;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace CinemaSystem.Tests;

public sealed class ControllerMoqCoverageTests
{
    [Fact]
    public async Task Auth_Register_ForwardsRequestAndReturnsCreated()
    {
        var service = new Mock<IAuthService>(MockBehavior.Strict);
        service
            .Setup(x => x.RegisterCustomerAsync(
                It.Is<RegisterRequest>(r => r.Email == "new@example.com" && r.FullName == "New Customer"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Ok(null, "Registered.", StatusCodes.Status201Created));

        var controller = new AuthController(service.Object);

        var result = await controller.Register(
            new RegisterRequest
            {
                Email = "new@example.com",
                Password = "Password123!",
                FullName = "New Customer"
            },
            CancellationToken.None);

        var response = AssertApiResponse<object>(result, StatusCodes.Status201Created, true);
        Assert.Equal("Registered.", response.Message);
        service.VerifyAll();
    }

    [Fact]
    public async Task Auth_VerifyEmail_ReturnsBadRequestWhenOtpInvalid()
    {
        var service = new Mock<IAuthService>(MockBehavior.Strict);
        service
            .Setup(x => x.VerifyEmailAsync(
                It.Is<VerifyEmailRequest>(r => r.Email == "user@example.com" && r.Otp == "000000"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Fail(
                StatusCodes.Status400BadRequest,
                "Invalid OTP.",
                "INVALID_OTP"));

        var controller = new AuthController(service.Object);

        var result = await controller.VerifyEmail(
            new VerifyEmailRequest { Email = "user@example.com", Otp = "000000" },
            CancellationToken.None);

        var response = AssertApiResponse<object>(result, StatusCodes.Status400BadRequest, false);
        Assert.Equal("INVALID_OTP", response.ErrorCode);
        service.VerifyAll();
    }

    [Fact]
    public async Task Auth_Login_ReturnsTokenPayload()
    {
        var auth = new AuthResponse
        {
            AccessToken = "access",
            RefreshToken = "refresh",
            ExpiresAt = DateTime.UtcNow.AddMinutes(15),
            UserId = "USR_1",
            Email = "customer@example.com",
            FullName = "Customer",
            Role = "Customer"
        };
        var service = new Mock<IAuthService>(MockBehavior.Strict);
        service
            .Setup(x => x.LoginAsync(
                It.Is<LoginRequest>(r => r.Email == "customer@example.com" && r.Password == "Password123!"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<AuthResponse>.Ok(auth, "Login successful."));

        var controller = new AuthController(service.Object);

        var result = await controller.Login(
            new LoginRequest { Email = "customer@example.com", Password = "Password123!" },
            CancellationToken.None);

        var response = AssertApiResponse<AuthResponse>(result, StatusCodes.Status200OK, true);
        Assert.Equal("access", response.Data!.AccessToken);
        Assert.Equal("Customer", response.Data.Role);
        service.VerifyAll();
    }

    [Fact]
    public async Task Auth_RefreshToken_ReturnsUnauthorizedForRevokedToken()
    {
        var service = new Mock<IAuthService>(MockBehavior.Strict);
        service
            .Setup(x => x.RefreshTokenAsync(
                It.Is<RefreshTokenRequest>(r => r.RefreshToken == "revoked"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<TokenResponse>.Fail(
                StatusCodes.Status401Unauthorized,
                "Invalid refresh token.",
                "INVALID_REFRESH_TOKEN"));

        var controller = new AuthController(service.Object);

        var result = await controller.RefreshToken(
            new RefreshTokenRequest { RefreshToken = "revoked" },
            CancellationToken.None);

        var response = AssertApiResponse<TokenResponse>(result, StatusCodes.Status401Unauthorized, false);
        Assert.Equal("INVALID_REFRESH_TOKEN", response.ErrorCode);
        service.VerifyAll();
    }

    [Fact]
    public async Task Auth_Logout_ResendForgotReset_ForwardToService()
    {
        var service = new Mock<IAuthService>(MockBehavior.Strict);
        service.Setup(x => x.LogoutAsync(It.IsAny<LogoutRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Ok(null, "Logged out."));
        service.Setup(x => x.ResendVerificationOtpAsync(It.IsAny<ResendVerificationOtpRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Fail(StatusCodes.Status429TooManyRequests, "Cooldown.", "OTP_COOLDOWN"));
        service.Setup(x => x.ForgotPasswordAsync(It.IsAny<ForgotPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Ok(null, "OTP sent."));
        service.Setup(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Weak password.", "WEAK_PASSWORD"));
        var controller = new AuthController(service.Object);

        AssertApiResponse<object>(
            await controller.Logout(new LogoutRequest { RefreshToken = "refresh" }, CancellationToken.None),
            StatusCodes.Status200OK,
            true);
        AssertApiResponse<object>(
            await controller.ResendVerificationOtp(new ResendVerificationOtpRequest { Email = "user@example.com" }, CancellationToken.None),
            StatusCodes.Status429TooManyRequests,
            false);
        AssertApiResponse<object>(
            await controller.ForgotPassword(new ForgotPasswordRequest { Email = "user@example.com" }, CancellationToken.None),
            StatusCodes.Status200OK,
            true);
        AssertApiResponse<object>(
            await controller.ResetPassword(
                new ResetPasswordRequest { Email = "user@example.com", Otp = "123456", NewPassword = "short" },
                CancellationToken.None),
            StatusCodes.Status400BadRequest,
            false);

        service.Verify(x => x.LogoutAsync(It.Is<LogoutRequest>(r => r.RefreshToken == "refresh"), It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(x => x.ResendVerificationOtpAsync(It.IsAny<ResendVerificationOtpRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(x => x.ForgotPasswordAsync(It.IsAny<ForgotPasswordRequest>(), It.IsAny<CancellationToken>()), Times.Once);
        service.Verify(x => x.ResetPasswordAsync(It.IsAny<ResetPasswordRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Admin_CreateStaff_InvalidModelState_ReturnsBadRequestWithoutCallingService()
    {
        var service = new Mock<IAdminService>(MockBehavior.Strict);
        var controller = new AdminController(service.Object);
        controller.ModelState.AddModelError(nameof(CreateStaffRequest.Email), "Required");

        var result = await controller.CreateStaff(new CreateStaffRequest(), CancellationToken.None);

        var response = AssertApiResponse<object>(result, StatusCodes.Status400BadRequest, false);
        Assert.Equal("VALIDATION_ERROR", response.ErrorCode);
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Admin_CreateStaff_ServiceConflict_ReturnsConflict()
    {
        var service = new Mock<IAdminService>(MockBehavior.Strict);
        service
            .Setup(x => x.CreateStaffAsync(
                It.Is<CreateStaffRequest>(r => r.Email == "staff@example.com"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Fail(StatusCodes.Status409Conflict, "Email exists.", "EMAIL_EXISTS"));
        var controller = new AdminController(service.Object);

        var result = await controller.CreateStaff(
            new CreateStaffRequest { Email = "staff@example.com", FullName = "Staff" },
            CancellationToken.None);

        var response = AssertApiResponse<object>(result, StatusCodes.Status409Conflict, false);
        Assert.Equal("EMAIL_EXISTS", response.ErrorCode);
        service.VerifyAll();
    }

    [Fact]
    public async Task Customer_ProfileEndpoints_ReturnUnauthorizedWhenUserIdMissing()
    {
        var service = new Mock<ICustomerService>(MockBehavior.Strict);
        var controller = WithUser(new CustomersController(service.Object));

        Assert.IsType<UnauthorizedResult>(await controller.GetProfile(CancellationToken.None));
        Assert.IsType<UnauthorizedResult>(await controller.UpdateProfile(new UpdateProfileRequest(), CancellationToken.None));
        Assert.IsType<UnauthorizedResult>(await controller.ChangePassword(new ChangePasswordRequest(), CancellationToken.None));
        Assert.IsType<UnauthorizedResult>(await controller.RequestEmailChange(new UpdateEmailRequest(), CancellationToken.None));
        Assert.IsType<UnauthorizedResult>(await controller.VerifyEmailChange(new VerifyEmailUpdateRequest(), CancellationToken.None));
        Assert.IsType<UnauthorizedResult>(await controller.GetBookingHistory(CancellationToken.None));
        service.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Customer_AllEndpoints_UseNameIdentifierClaimAndMapServiceResponses()
    {
        var service = new Mock<ICustomerService>(MockBehavior.Strict);
        service.Setup(x => x.GetProfileAsync("USR_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<CustomerProfileResponse>.Ok(new CustomerProfileResponse { UserId = "USR_1" }));
        service.Setup(x => x.UpdateProfileAsync("USR_1", It.IsAny<UpdateProfileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<CustomerProfileResponse>.Ok(new CustomerProfileResponse { FullName = "Updated" }));
        service.Setup(x => x.ChangePasswordAsync("USR_1", It.IsAny<ChangePasswordRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Fail(StatusCodes.Status400BadRequest, "Wrong password.", "WRONG_PASSWORD"));
        service.Setup(x => x.RequestEmailUpdateAsync("USR_1", It.IsAny<UpdateEmailRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Ok(null, "OTP sent."));
        service.Setup(x => x.VerifyEmailUpdateAsync("USR_1", It.IsAny<VerifyEmailUpdateRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Fail(StatusCodes.Status409Conflict, "Email exists.", "EMAIL_EXISTS"));
        service.Setup(x => x.GetBookingHistoryAsync("USR_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<List<BookingHistoryResponse>>.Ok([new BookingHistoryResponse { BookingId = "BKG_1" }]));
        var controller = WithUser(
            new CustomersController(service.Object),
            new Claim(ClaimTypes.NameIdentifier, "USR_1"));

        AssertApiResponse<CustomerProfileResponse>(await controller.GetProfile(CancellationToken.None), StatusCodes.Status200OK, true);
        AssertApiResponse<CustomerProfileResponse>(await controller.UpdateProfile(new UpdateProfileRequest { FullName = "Updated" }, CancellationToken.None), StatusCodes.Status200OK, true);
        AssertApiResponse<object>(await controller.ChangePassword(new ChangePasswordRequest { OldPassword = "old", NewPassword = "new" }, CancellationToken.None), StatusCodes.Status400BadRequest, false);
        AssertApiResponse<object>(await controller.RequestEmailChange(new UpdateEmailRequest { NewEmail = "new@example.com" }, CancellationToken.None), StatusCodes.Status200OK, true);
        AssertApiResponse<object>(await controller.VerifyEmailChange(new VerifyEmailUpdateRequest { NewEmail = "new@example.com", Otp = "123456" }, CancellationToken.None), StatusCodes.Status409Conflict, false);
        var history = AssertApiResponse<List<BookingHistoryResponse>>(await controller.GetBookingHistory(CancellationToken.None), StatusCodes.Status200OK, true);
        Assert.Single(history.Data!);
        service.VerifyAll();
    }

    [Fact]
    public async Task Payment_CreatePayment_ReturnsOkWithProviderPayload()
    {
        var payment = new Mock<IPaymentService>(MockBehavior.Strict);
        var webhook = new Mock<IPaymentWebhookService>(MockBehavior.Strict);
        payment
            .Setup(x => x.CreatePaymentAsync(
                It.Is<CreatePaymentRequest>(r => r.BookingId == "BKG_1" && r.PaymentProviderId == "SEPAY"),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatePaymentResponse
            {
                PaymentId = "PAY_1",
                Amount = 100000,
                TransactionCode = "BKG_1"
            });
        var controller = WithHttpContext(new PaymentController(payment.Object, webhook.Object));

        var result = await controller.CreatePayment(
            new CreatePaymentRequest { BookingId = "BKG_1", PaymentProviderId = "SEPAY" },
            CancellationToken.None);

        var response = AssertApiResponse<CreatePaymentResponse>(result, StatusCodes.Status200OK, true);
        Assert.Equal("PAY_1", response.Data!.PaymentId);
        payment.VerifyAll();
        webhook.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Payment_SepayWebhook_ForwardsRawJsonAndHeaders()
    {
        var payment = new Mock<IPaymentService>(MockBehavior.Strict);
        var webhook = new Mock<IPaymentWebhookService>(MockBehavior.Strict);
        webhook
            .Setup(x => x.HandleSepayWebhookAsync(
                "{\"content\":\"BKG_1\",\"transferAmount\":100000}",
                "sig",
                "ts",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Ok(new { paymentId = "PAY_1" }, "Payment confirmed."));
        var controller = WithHttpContext(new PaymentController(payment.Object, webhook.Object));
        using var document = JsonDocument.Parse("""{"content":"BKG_1","transferAmount":100000}""");

        var result = await controller.SepayWebhook(document.RootElement, "sig", "ts");

        var response = AssertApiResponse<object>(result, StatusCodes.Status200OK, true);
        Assert.Equal("Payment confirmed.", response.Message);
        webhook.VerifyAll();
        payment.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Payment_SepayWebhook_InvalidSignature_ReturnsUnauthorized()
    {
        var payment = new Mock<IPaymentService>(MockBehavior.Strict);
        var webhook = new Mock<IPaymentWebhookService>(MockBehavior.Strict);
        webhook
            .Setup(x => x.HandleSepayWebhookAsync(
                It.IsAny<string>(),
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Fail(
                StatusCodes.Status401Unauthorized,
                "Invalid signature.",
                "INVALID_SIGNATURE"));
        var controller = WithHttpContext(new PaymentController(payment.Object, webhook.Object));
        using var document = JsonDocument.Parse("""{"content":"BKG_1"}""");

        var result = await controller.SepayWebhook(document.RootElement);

        var response = AssertApiResponse<object>(result, StatusCodes.Status401Unauthorized, false);
        Assert.Equal("INVALID_SIGNATURE", response.ErrorCode);
        webhook.VerifyAll();
    }

    [Fact]
    public async Task Cinemas_GetCinemas_ReturnsListAndServiceErrors()
    {
        var service = new Mock<ICinemaService>(MockBehavior.Strict);
        service.SetupSequence(x => x.GetCinemasAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<IReadOnlyList<CinemaResponse>>.Ok(
                [new CinemaResponse { CinemaId = "CIN_1", CinemaName = "Cinema 1" }]))
            .ReturnsAsync(ServiceResult<IReadOnlyList<CinemaResponse>>.Fail(
                StatusCodes.Status500InternalServerError,
                "Database unavailable.",
                "DATABASE_ERROR"));
        var controller = new CinemasController(service.Object);

        var success = AssertApiResponse<IReadOnlyList<CinemaResponse>>(await controller.GetCinemas(CancellationToken.None), StatusCodes.Status200OK, true);
        Assert.Single(success.Data!);
        var failure = AssertApiResponse<IReadOnlyList<CinemaResponse>>(await controller.GetCinemas(CancellationToken.None), StatusCodes.Status500InternalServerError, false);
        Assert.Equal("DATABASE_ERROR", failure.ErrorCode);
        service.Verify(x => x.GetCinemasAsync(It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task Movies_GetMoviesAndDetail_ReturnSuccessAndNotFound()
    {
        var service = new Mock<IMovieService>(MockBehavior.Strict);
        service.Setup(x => x.GetMoviesAsync("NOW_SHOWING", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<IReadOnlyList<MovieResponse>>.Ok(
                [new MovieResponse { Id = "MOV_1", MovieNameVn = "Movie" }]));
        service.Setup(x => x.GetMovieByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<MovieDetailResponse>.Fail(StatusCodes.Status404NotFound, "Movie not found.", "MOVIE_NOT_FOUND"));
        var controller = new MoviesController(service.Object);

        var movies = AssertApiResponse<IReadOnlyList<MovieResponse>>(await controller.GetMovies("NOW_SHOWING", CancellationToken.None), StatusCodes.Status200OK, true);
        Assert.Single(movies.Data!);
        var missing = AssertApiResponse<MovieDetailResponse>(await controller.GetMovieById("missing", CancellationToken.None), StatusCodes.Status404NotFound, false);
        Assert.Equal("MOVIE_NOT_FOUND", missing.ErrorCode);
        service.VerifyAll();
    }

    [Fact]
    public async Task Rooms_AllEndpoints_ForwardRequestsAndMapStatuses()
    {
        var service = new Mock<IRoomService>(MockBehavior.Strict);
        service.Setup(x => x.GetRoomsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<IReadOnlyList<RoomResponse>>.Ok([new RoomResponse { RoomId = "ROOM_1" }]));
        service.Setup(x => x.GetRoomByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<RoomResponse>.Fail(StatusCodes.Status404NotFound, "Room not found.", "ROOM_NOT_FOUND"));
        service.Setup(x => x.CreateRoomAsync("CIN_1", It.Is<CreateRoomRequest>(r => r.RoomName == "Room A"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<RoomResponse>.Ok(new RoomResponse { RoomId = "ROOM_2" }, "Created.", StatusCodes.Status201Created));
        service.Setup(x => x.UpdateRoomAsync("ROOM_2", It.Is<UpdateRoomRequest>(r => r.RoomName == "Room B"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<RoomResponse>.Ok(new RoomResponse { RoomId = "ROOM_2", RoomName = "Room B" }));
        service.Setup(x => x.DeleteRoomAsync("ROOM_2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Fail(StatusCodes.Status409Conflict, "Room has future showtimes.", "ROOM_IN_USE"));
        service.Setup(x => x.GenerateSeatsAsync("ROOM_2", It.Is<GenerateSeatsRequest>(r => r.Rows == 2 && r.Columns == 3), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Ok(null, "Seats generated."));
        var controller = new RoomsController(service.Object);

        Assert.Single(AssertApiResponse<IReadOnlyList<RoomResponse>>(await controller.GetRooms(CancellationToken.None), StatusCodes.Status200OK, true).Data!);
        AssertApiResponse<RoomResponse>(await controller.GetRoomById("missing", CancellationToken.None), StatusCodes.Status404NotFound, false);
        AssertApiResponse<RoomResponse>(await controller.CreateRoom("CIN_1", new CreateRoomRequest { RoomName = "Room A", Capacity = 20 }, CancellationToken.None), StatusCodes.Status201Created, true);
        AssertApiResponse<RoomResponse>(await controller.UpdateRoom("ROOM_2", new UpdateRoomRequest { RoomName = "Room B", Capacity = 25 }, CancellationToken.None), StatusCodes.Status200OK, true);
        AssertApiResponse<object>(await controller.DeleteRoom("ROOM_2", CancellationToken.None), StatusCodes.Status409Conflict, false);
        AssertApiResponse<object>(await controller.GenerateSeats("ROOM_2", new GenerateSeatsRequest { Rows = 2, Columns = 3, SeatTypeId = "STD" }, CancellationToken.None), StatusCodes.Status200OK, true);
        service.VerifyAll();
    }

    [Fact]
    public async Task Showtimes_AllEndpoints_ForwardRequestsAndMapStatuses()
    {
        var service = new Mock<IShowtimeService>(MockBehavior.Strict);
        service.Setup(x => x.GetShowtimesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<IReadOnlyList<ShowtimeResponse>>.Ok([new ShowtimeResponse { ShowtimeId = "ST_1" }]));
        service.Setup(x => x.GetShowtimeByIdAsync("missing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<ShowtimeResponse>.Fail(StatusCodes.Status404NotFound, "Showtime not found.", "SHOWTIME_NOT_FOUND"));
        service.Setup(x => x.CreateShowtimeAsync(It.Is<CreateShowtimeRequest>(r => r.MovieId == "MOV_1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<ShowtimeResponse>.Ok(new ShowtimeResponse { ShowtimeId = "ST_2" }, "Created.", StatusCodes.Status201Created));
        service.Setup(x => x.UpdateShowtimeAsync("ST_2", It.Is<UpdateShowtimeRequest>(r => r.Status == "CLOSED"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<ShowtimeResponse>.Ok(new ShowtimeResponse { ShowtimeId = "ST_2", Status = "CLOSED" }));
        service.Setup(x => x.DeleteShowtimeAsync("ST_2", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<object>.Fail(StatusCodes.Status409Conflict, "Showtime has bookings.", "SHOWTIME_IN_USE"));
        var controller = new ShowtimesController(service.Object);

        Assert.Single(AssertApiResponse<IReadOnlyList<ShowtimeResponse>>(await controller.GetShowtimes(CancellationToken.None), StatusCodes.Status200OK, true).Data!);
        AssertApiResponse<ShowtimeResponse>(await controller.GetShowtimeById("missing", CancellationToken.None), StatusCodes.Status404NotFound, false);
        AssertApiResponse<ShowtimeResponse>(await controller.CreateShowtime(new CreateShowtimeRequest { MovieId = "MOV_1", RoomId = "ROOM_1", StartTime = DateTime.UtcNow, BasePrice = 1 }, CancellationToken.None), StatusCodes.Status201Created, true);
        AssertApiResponse<ShowtimeResponse>(await controller.UpdateShowtime("ST_2", new UpdateShowtimeRequest { MovieId = "MOV_1", RoomId = "ROOM_1", StartTime = DateTime.UtcNow, BasePrice = 1, Status = "CLOSED" }, CancellationToken.None), StatusCodes.Status200OK, true);
        AssertApiResponse<object>(await controller.DeleteShowtime("ST_2", CancellationToken.None), StatusCodes.Status409Conflict, false);
        service.VerifyAll();
    }

    [Fact]
    public async Task Seats_CrudAndMapEndpoints_UseUserClaimAndMapResponses()
    {
        var service = new Mock<ISeatService>(MockBehavior.Strict);
        service.Setup(x => x.CreateSeatAsync(It.Is<CreateSeatRequest>(r => r.RoomId == "ROOM_1"), "USR_MANAGER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true, "Created.", StatusCodes.Status201Created));
        service.Setup(x => x.UpdateSeatAsync(It.Is<UpdateSeatRequest>(r => r.SeatId == "SEAT_1" && r.SeatNumber == 2), "USR_MANAGER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Ok(true));
        service.Setup(x => x.DeleteSeatAsync("SEAT_1", "USR_MANAGER", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<bool>.Fail(StatusCodes.Status409Conflict, "Seat in use.", "SEAT_IN_USE"));
        service.Setup(x => x.GetSeatsByRoomAsync("ROOM_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<IEnumerable<SeatResponse>>.Ok([new SeatResponse { SeatId = "SEAT_1", RoomId = "ROOM_1" }]));
        service.Setup(x => x.GetSeatMapAsync("ST_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<SeatMapResponse>.Ok(new SeatMapResponse { ShowtimeId = "ST_1" }));
        var controller = WithUser(new SeatsController(service.Object), new Claim("userId", "USR_MANAGER"));

        AssertApiResponse<bool>(await controller.CreateSeatRequest(new CreateSeatRequest { RoomId = "ROOM_1", RowLabel = "A", SeatNumber = 1, SeatTypeId = "STD" }, CancellationToken.None), StatusCodes.Status201Created, true);
        AssertApiResponse<bool>(await controller.UpdateSeat("SEAT_1", new UpdateSeatRequest { RowLabel = "A", SeatNumber = 2, SeatTypeId = "STD" }, CancellationToken.None), StatusCodes.Status200OK, true);
        AssertApiResponse<bool>(await controller.DeleteSeat("SEAT_1", CancellationToken.None), StatusCodes.Status409Conflict, false);
        Assert.Single(AssertApiResponse<IEnumerable<SeatResponse>>(await controller.GetSeatsByRoom("ROOM_1", CancellationToken.None), StatusCodes.Status200OK, true).Data!);
        AssertApiResponse<SeatMapResponse>(await controller.GetSeatMap("ST_1", CancellationToken.None), StatusCodes.Status200OK, true);
        service.VerifyAll();
    }

    [Fact]
    public async Task Seats_LockSeat_UsesSubClaimAndReturnsConflictForLockedSeat()
    {
        var service = new Mock<ISeatService>(MockBehavior.Strict);
        service.Setup(x => x.LockSeatAsync(
                It.Is<LockSeatRequest>(r => r.ShowtimeId == "ST_1" && r.SeatId == "SEAT_1"),
                "USR_CUSTOMER",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<LockSeatResponse>.Fail(
                StatusCodes.Status409Conflict,
                "Seat is locked.",
                "SEAT_LOCKED"));
        var controller = WithUser(new SeatsController(service.Object), new Claim("sub", "USR_CUSTOMER"));

        var result = await controller.LockSeat(
            new LockSeatRequest { ShowtimeId = "ST_1", SeatId = "SEAT_1" },
            CancellationToken.None);

        var response = AssertApiResponse<LockSeatResponse>(result, StatusCodes.Status409Conflict, false);
        Assert.Equal("SEAT_LOCKED", response.ErrorCode);
        service.VerifyAll();
    }

    [Fact]
    public async Task Bookings_MissingUserClaim_ReturnsUnauthorizedWithoutCallingServices()
    {
        var booking = new Mock<IBookingService>(MockBehavior.Strict);
        var checkout = new Mock<ICheckoutService>(MockBehavior.Strict);
        var controller = WithUser(new BookingsController(booking.Object, checkout.Object));

        Assert.IsType<UnauthorizedResult>(await controller.CreateBooking(new CreateBookingRequest(), CancellationToken.None));
        Assert.IsType<UnauthorizedResult>(await controller.GetBookingDetails("BKG_1", CancellationToken.None));
        Assert.IsType<UnauthorizedResult>(await controller.GetMyBookings(CancellationToken.None));
        Assert.IsType<UnauthorizedObjectResult>(await controller.Checkout(new CheckoutRequest(), CancellationToken.None));
        booking.VerifyNoOtherCalls();
        checkout.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Bookings_AllEndpoints_WithUserClaim_MapSuccessAndFailures()
    {
        var booking = new Mock<IBookingService>(MockBehavior.Strict);
        var checkout = new Mock<ICheckoutService>(MockBehavior.Strict);
        booking.Setup(x => x.CreateBookingAsync(It.IsAny<CreateBookingRequest>(), "USR_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<BookingResponse>.Ok(new BookingResponse { BookingId = "BKG_1" }, "Created.", StatusCodes.Status201Created));
        booking.Setup(x => x.GetBookingDetailsAsync("missing", "USR_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<BookingDetailsResponse>.Fail(StatusCodes.Status404NotFound, "Booking not found.", "BOOKING_NOT_FOUND"));
        booking.Setup(x => x.GetMyBookingsAsync("USR_1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<IReadOnlyList<BookingResponse>>.Ok([new BookingResponse { BookingId = "BKG_1" }]));
        checkout.Setup(x => x.CheckoutAsync("USR_1", It.Is<CheckoutRequest>(r => r.ShowtimeId == "ST_1"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ServiceResult<CheckoutResponse>.Fail(StatusCodes.Status409Conflict, "Seat unavailable.", "SEAT_UNAVAILABLE"));
        var controller = WithUser(new BookingsController(booking.Object, checkout.Object), new Claim("userId", "USR_1"));

        AssertApiResponse<BookingResponse>(await controller.CreateBooking(new CreateBookingRequest { ShowtimeId = "ST_1", ShowtimeSeatIds = ["STS_1"] }, CancellationToken.None), StatusCodes.Status201Created, true);
        AssertApiResponse<BookingDetailsResponse>(await controller.GetBookingDetails("missing", CancellationToken.None), StatusCodes.Status404NotFound, false);
        Assert.Single(AssertApiResponse<IReadOnlyList<BookingResponse>>(await controller.GetMyBookings(CancellationToken.None), StatusCodes.Status200OK, true).Data!);
        AssertApiResponse<CheckoutResponse>(await controller.Checkout(new CheckoutRequest { ShowtimeId = "ST_1", ShowtimeSeatIds = ["STS_1"] }, CancellationToken.None), StatusCodes.Status409Conflict, false);
        booking.VerifyAll();
        checkout.VerifyAll();
    }

    [Fact]
    public async Task DbTest_GetMoviesCount_ReturnsMovieTableCount()
    {
        var diagnostics = new Mock<ICinemaDiagnosticsService>(MockBehavior.Strict);
        diagnostics
            .Setup(x => x.GetMoviesCountAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(7);
        var controller = WithHttpContext(new DbTestController(diagnostics.Object));

        var result = await controller.GetMoviesCount();

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal("MOVIE", GetAnonymousProperty<string>(ok.Value!, "table"));
        Assert.Equal(7, GetAnonymousProperty<int>(ok.Value!, "count"));
        diagnostics.VerifyAll();
    }

    [Fact]
    public void AuthPolicyTest_ReturnsCurrentUserFromClaims()
    {
        var controller = WithUser(
            new AuthPolicyTestController(),
            new Claim("userId", "USR_ADMIN"),
            new Claim(ClaimTypes.Email, "admin@example.com"),
            new Claim(ClaimTypes.Name, "Admin User"),
            new Claim(ClaimTypes.Role, "Admin"));

        var customer = AssertApiResponse<UserProfileResponse>(controller.CustomerOnly(), StatusCodes.Status200OK, true);
        var admin = AssertApiResponse<UserProfileResponse>(controller.AdminOnly(), StatusCodes.Status200OK, true);

        Assert.Equal("USR_ADMIN", customer.Data!.UserId);
        Assert.Equal("admin@example.com", admin.Data!.Email);
        Assert.Equal("Admin", admin.Data.Role);
    }

    private static TController WithUser<TController>(TController controller, params Claim[] claims)
        where TController : ControllerBase
    {
        WithHttpContext(controller);
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims, claims.Length == 0 ? null : "TestAuth"));
        return controller;
    }

    private static TController WithHttpContext<TController>(TController controller)
        where TController : ControllerBase
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
        return controller;
    }

    private static ApiResponse<T> AssertApiResponse<T>(
        IActionResult result,
        int expectedStatusCode,
        bool expectedSuccess)
    {
        var objectResult = Assert.IsAssignableFrom<ObjectResult>(result);
        Assert.Equal(expectedStatusCode, objectResult.StatusCode);
        var response = Assert.IsType<ApiResponse<T>>(objectResult.Value);
        Assert.Equal(expectedSuccess, response.Success);
        return response;
    }

    private static T GetAnonymousProperty<T>(object value, string propertyName)
    {
        var property = value.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        return Assert.IsType<T>(property.GetValue(value));
    }
}
