using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Refunds;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

public sealed class ShowtimeCancellationApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task CancelShowtime_ManagerAssignedCinema_CancelsShowtimeAndCreatesRefundData()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.PostAsJsonAsync(
            "/api/manager/showtimes/SHW_CANCEL_A/cancel",
            new CancelShowtimeRequest { Reason = "Projector failure" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<CancelShowtimeResponse>>(response);
        Assert.True(body!.Success);
        Assert.Equal("SHW_CANCEL_A", body.Data!.ShowtimeId);
        Assert.Equal(BookingConstants.ShowtimeStatus.Cancelled, body.Data.ShowtimeStatus);
        Assert.Equal(1, body.Data.PaidBookingsMovedToRefundPending);
        Assert.Equal(1, body.Data.UnpaidBookingsCancelled);
        Assert.Equal(1, body.Data.RefundsCreated);
        Assert.Equal(100000m, body.Data.TotalRefundAmount);
        Assert.Equal(0, body.Data.RefundsSucceeded);
        Assert.Equal(0, body.Data.RefundsManualRequired);
        Assert.Equal(1, body.Data.RefundsPending);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var showtime = await db.Showtimes.SingleAsync(item => item.ShowtimeId == "SHW_CANCEL_A");
        Assert.Equal(BookingConstants.ShowtimeStatus.Cancelled, showtime.Status);

        var cancellation = await db.ShowtimeCancellations.SingleAsync(item => item.ShowtimeId == "SHW_CANCEL_A");
        Assert.Equal("Projector failure", cancellation.CancelReason);
        Assert.Equal("USR_TEST_MANAGER", cancellation.CancelledByUserId);
        Assert.NotNull(cancellation.CancelledByStaffId);

        var paidBooking = await db.Bookings.SingleAsync(item => item.BookingId == "BKG_CANCEL_A_PAID");
        Assert.Equal(BookingConstants.BookingStatus.RefundPending, paidBooking.BookingStatus);

        var pendingBooking = await db.Bookings.SingleAsync(item => item.BookingId == "BKG_CANCEL_A_PENDING");
        Assert.Equal(BookingConstants.BookingStatus.Cancelled, pendingBooking.BookingStatus);

        var pendingPayment = await db.Payments.SingleAsync(item => item.PaymentId == "PAY_CANCEL_A_PENDING");
        Assert.Equal(BookingConstants.PaymentStatus.Cancelled, pendingPayment.PaymentStatus);

        var ticket = await db.Tickets.SingleAsync(item => item.TicketId == "TCK_CANCEL_A_PAID");
        Assert.Equal(BookingConstants.TicketStatus.Cancelled, ticket.TicketStatus);

        var refund = await db.Refunds.SingleAsync(item => item.BookingId == "BKG_CANCEL_A_PAID");
        Assert.Equal(BookingConstants.RefundStatus.Pending, refund.RefundStatus);
        Assert.Equal(100000m, refund.RefundAmount);
        Assert.Equal(cancellation.ShowtimeCancellationId, refund.ShowtimeCancellationId);
        var claim = await db.RefundClaims.SingleAsync(item => item.RefundId == refund.RefundId);
        Assert.Equal(BookingConstants.RefundClaimStatus.PendingInfo, claim.ClaimStatus);
        Assert.Single(await db.RefundClaimTokens.Where(item => item.RefundClaimId == claim.RefundClaimId).ToListAsync());

        Assert.Equal(2, await db.Notifications.CountAsync(item => item.UserId == "USR_CANCEL_CUSTOMER_A"));
        Assert.True(await db.AuditLogs.AnyAsync(item =>
            item.Action == "CANCEL_SHOWTIME"
            && item.EntityId == "SHW_CANCEL_A"));
        Assert.Equal(2, factory.EmailCapture.Emails.Count);
    }

    [Fact]
    public async Task CancelShowtime_ManagerOtherCinema_ReturnsForbidden()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.PostAsJsonAsync(
            "/api/manager/showtimes/SHW_CANCEL_B/cancel",
            new CancelShowtimeRequest { Reason = "Out of scope" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("CINEMA_SCOPE_FORBIDDEN", body!.ErrorCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        Assert.False(await db.ShowtimeCancellations.AnyAsync(item => item.ShowtimeId == "SHW_CANCEL_B"));
    }

    [Fact]
    public async Task GetRefunds_ManagerScope_ReturnsOnlyAssignedCinemaRefunds()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);
        await SeedExistingRefundsAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.GetAsync("/api/manager/refunds");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<List<RefundResponse>>>(response);
        Assert.True(body!.Success);
        var refund = Assert.Single(body.Data!);
        Assert.Equal("REF_SCOPE_A", refund.RefundId);
        Assert.Equal("CIN_CANCEL_A", refund.CinemaId);
        Assert.Equal("PAYPROV_CANCEL", refund.PaymentProviderId);
        Assert.Equal("SEPAY_CANCEL", refund.PaymentProviderName);
        Assert.Equal(BookingConstants.BookingStatus.Paid, refund.BookingStatus);
    }

    [Fact]
    public async Task CancelShowtime_SecondCall_ReturnsConflictAndDoesNotDuplicateRefund()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var first = await client.PostAsJsonAsync(
            "/api/manager/showtimes/SHW_CANCEL_A/cancel",
            new CancelShowtimeRequest { Reason = "First cancel" });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await client.PostAsJsonAsync(
            "/api/manager/showtimes/SHW_CANCEL_A/cancel",
            new CancelShowtimeRequest { Reason = "Second cancel" });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(second);
        Assert.Equal("SHOWTIME_ALREADY_CANCELLED", body!.ErrorCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        Assert.Single(await db.Refunds
            .Where(item => item.BookingId == "BKG_CANCEL_A_PAID")
            .ToListAsync());
    }

    [Fact]
    public async Task CancelShowtime_AlreadyStartedShowtime_ReturnsConflict()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var showtime = await db.Showtimes.SingleAsync(item => item.ShowtimeId == "SHW_CANCEL_A");
            showtime.StartTime = DateTime.UtcNow.AddMinutes(-1);
            showtime.EndTime = DateTime.UtcNow.AddHours(1);
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.PostAsJsonAsync(
            "/api/manager/showtimes/SHW_CANCEL_A/cancel",
            new CancelShowtimeRequest { Reason = "Invalid started cancellation" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(response);
        Assert.Equal("SHOWTIME_ALREADY_STARTED", body!.ErrorCode);
    }

    [Fact]
    public async Task CancelShowtime_LateSuccessfulPayment_CreatesAndProcessesRefund()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var cancellationResponse = await client.PostAsJsonAsync(
            "/api/manager/showtimes/SHW_CANCEL_A/cancel",
            new CancelShowtimeRequest { Reason = "Power failure" });
        Assert.Equal(HttpStatusCode.OK, cancellationResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var paymentService = scope.ServiceProvider.GetRequiredService<IPaymentService>();
        await paymentService.ConfirmPaymentAsync(
            "Late transfer TCANCELA002",
            100000m,
            "LATE_PROVIDER_CODE");

        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var payment = await db.Payments.SingleAsync(item => item.PaymentId == "PAY_CANCEL_A_PENDING");
        var booking = await db.Bookings.SingleAsync(item => item.BookingId == "BKG_CANCEL_A_PENDING");
        var refund = await db.Refunds.SingleAsync(item => item.PaymentId == payment.PaymentId);

        Assert.Equal(BookingConstants.PaymentStatus.Success, payment.PaymentStatus);
        Assert.Equal(BookingConstants.BookingStatus.RefundPending, booking.BookingStatus);
        Assert.Equal(BookingConstants.RefundStatus.Pending, refund.RefundStatus);
        Assert.True(await db.RefundClaims.AnyAsync(item => item.RefundId == refund.RefundId));
    }

    [Fact]
    public async Task DeleteShowtime_WithCancellationHistory_ReturnsConflict()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var cancelResponse = await client.PostAsJsonAsync(
            "/api/manager/showtimes/SHW_CANCEL_EMPTY/cancel",
            new CancelShowtimeRequest { Reason = "No audience" });
        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);

        var deleteResponse = await client.DeleteAsync("/api/showtimes/SHW_CANCEL_EMPTY");

        Assert.Equal(HttpStatusCode.Conflict, deleteResponse.StatusCode);
        var body = await DeserializeAsync<ApiResponse<object>>(deleteResponse);
        Assert.Equal("RESOURCE_HAS_CANCELLATION_HISTORY", body!.ErrorCode);
    }

    [Fact]
    public async Task GetRefunds_ManagerFiltersByStatusAndShowtime()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);
        await SeedExistingRefundsAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.GetAsync(
            "/api/manager/refunds?status=PENDING&showtimeId=SHW_CANCEL_A");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<List<RefundResponse>>>(response);
        var refund = Assert.Single(body!.Data!);
        Assert.Equal("REF_SCOPE_A", refund.RefundId);
    }

    [Fact]
    public async Task ManualRefund_EndToEnd_EnforcesRolesAndCompletesAtomically()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);

        using var managerClient = factory.CreateClient();
        managerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());
        var cancel = await managerClient.PostAsJsonAsync(
            "/api/manager/showtimes/SHW_CANCEL_A/cancel",
            new CancelShowtimeRequest { Reason = "Manual refund flow" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        var claimEmail = factory.EmailCapture.Emails.Single(item =>
            item.Body.Contains("/refunds/claim?t=", StringComparison.Ordinal));
        var tokenMatch = Regex.Match(claimEmail.Body, @"[?&]t=([^\s]+)");
        Assert.True(tokenMatch.Success);
        var rawToken = Uri.UnescapeDataString(tokenMatch.Groups[1].Value);

        using var customerClient = factory.CreateClient();
        customerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TestAuthTokens.Customer("USR_CANCEL_CUSTOMER_A"));

        var resolve = await customerClient.PostAsJsonAsync(
            "/api/customer/refund-claims/resolve",
            new ResolveRefundClaimRequest { Token = rawToken });
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        var resolvedBody = await DeserializeAsync<ApiResponse<RefundClaimResponse>>(resolve);
        var claimId = resolvedBody!.Data!.RefundClaimId;

        var save = await customerClient.PutAsJsonAsync(
            $"/api/customer/refund-claims/{claimId}/bank-account",
            new SaveRefundBankAccountRequest
            {
                BankCode = "VCB",
                AccountNumber = "0123456789",
                AccountHolderName = "Nguyen Van A"
            });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var savedBody = await DeserializeAsync<ApiResponse<RefundClaimResponse>>(save);
        Assert.Equal("******6789", savedBody!.Data!.MaskedAccountNumber);
        Assert.Equal("NGUYEN VAN A", savedBody.Data.AccountHolderName);

        var submit = await customerClient.PostAsync(
            $"/api/customer/refund-claims/{claimId}/submit",
            null);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);

        var managerList = await managerClient.GetAsync("/api/manager/refunds?status=MANUAL_REQUIRED");
        Assert.Equal(HttpStatusCode.OK, managerList.StatusCode);
        var managerBody = await DeserializeAsync<ApiResponse<List<RefundResponse>>>(managerList);
        var managerRefund = Assert.Single(managerBody!.Data!);
        Assert.Equal("******6789", managerRefund.MaskedAccountNumber);
        Assert.Equal("MANUAL_REQUIRED", managerRefund.WorkflowStatus);

        var forbidden = await managerClient.GetAsync("/api/admin/refunds/manual");
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        using var adminClient = factory.CreateClient();
        adminClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Admin());
        var manualList = await adminClient.GetAsync("/api/admin/refunds/manual");
        Assert.True(
            manualList.StatusCode == HttpStatusCode.OK,
            await manualList.Content.ReadAsStringAsync());
        var manualBody = await DeserializeAsync<ApiResponse<List<ManualRefundResponse>>>(manualList);
        var manualRefund = Assert.Single(manualBody!.Data!);
        Assert.Equal("0123456789", manualRefund.AccountNumber);
        Assert.Equal("NGUYEN VAN A", manualRefund.AccountHolderName);

        var assign = await adminClient.PostAsync(
            $"/api/admin/refunds/{manualRefund.RefundId}/assign",
            null);
        Assert.Equal(HttpStatusCode.OK, assign.StatusCode);

        var confirm = await adminClient.PostAsJsonAsync(
            $"/api/admin/refunds/{manualRefund.RefundId}/manual-confirm",
            new ManualRefundConfirmationRequest
            {
                BankTransactionCode = "VCB-REFUND-0001",
                TransferredAmount = 100000m,
                ProofUrl = "https://secure.example/refund-proof/0001",
                Note = "Verified in bank portal"
            });
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);

        var repeated = await adminClient.PostAsJsonAsync(
            $"/api/admin/refunds/{manualRefund.RefundId}/manual-confirm",
            new ManualRefundConfirmationRequest
            {
                BankTransactionCode = "VCB-REFUND-0001",
                TransferredAmount = 100000m,
                ProofUrl = "https://secure.example/refund-proof/0001"
            });
        Assert.Equal(HttpStatusCode.OK, repeated.StatusCode);
        var repeatedBody = await DeserializeAsync<ApiResponse<RefundProcessingResponse>>(repeated);
        Assert.True(repeatedBody!.Data!.AlreadyProcessed);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var refund = await db.Refunds.SingleAsync(item => item.RefundId == manualRefund.RefundId);
        var booking = await db.Bookings.SingleAsync(item => item.BookingId == refund.BookingId);
        var ticket = await db.Tickets.SingleAsync(item => item.TicketId == "TCK_CANCEL_A_PAID");
        var process = await db.ManualRefundProcesses.SingleAsync(item => item.RefundId == refund.RefundId);
        Assert.Equal(BookingConstants.RefundStatus.Success, refund.RefundStatus);
        Assert.Equal(BookingConstants.BookingStatus.Refunded, booking.BookingStatus);
        Assert.Equal(BookingConstants.TicketStatus.Refunded, ticket.TicketStatus);
        Assert.Equal(BookingConstants.ManualRefundProcessStatus.Confirmed, process.ProcessStatus);
        Assert.Equal("VCB-REFUND-0001", refund.ProviderRefundCode);
    }

    private static async Task SeedCancellationDataAsync(CinemaWebApplicationFactory factory)
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
            UserId = "USR_CANCEL_CUSTOMER_A",
            RoleId = AuthConstants.RoleIds.Customer,
            Email = "cancel-a@test.com",
            PasswordHash = "HASH",
            FullName = "Cancel Customer A",
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = now
        });
        db.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerProfileId = "CUS_CANCEL_A",
            UserId = "USR_CANCEL_CUSTOMER_A",
            MemberLevel = "STANDARD",
            RewardPoints = 0
        });

        db.Users.Add(new User
        {
            UserId = "USR_CANCEL_CUSTOMER_B",
            RoleId = AuthConstants.RoleIds.Customer,
            Email = "cancel-b@test.com",
            PasswordHash = "HASH",
            FullName = "Cancel Customer B",
            Status = AuthConstants.UserStatus.Active,
            EmailVerified = true,
            CreatedAt = now
        });
        db.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerProfileId = "CUS_CANCEL_B",
            UserId = "USR_CANCEL_CUSTOMER_B",
            MemberLevel = "STANDARD",
            RewardPoints = 0
        });

        db.Cinemas.AddRange(
            new Cinema { CinemaId = "CIN_CANCEL_A", CinemaName = "Cancel A", Address = "A", City = "HCM", CinemaStatus = "ACTIVE" },
            new Cinema { CinemaId = "CIN_CANCEL_B", CinemaName = "Cancel B", Address = "B", City = "HCM", CinemaStatus = "ACTIVE" });
        db.Movies.Add(new Movie
        {
            MovieId = "MOV_CANCEL",
            Title = "Cancel Movie",
            DurationMinutes = 120,
            AgeRating = "T13",
            MovieStatus = "NOW_SHOWING"
        });
        db.SeatTypes.Add(new SeatType
        {
            SeatTypeId = "SEAT_TYPE_CANCEL",
            TypeName = "STANDARD",
            ExtraFee = 0
        });
        db.Rooms.AddRange(
            new Room { RoomId = "ROOM_CANCEL_A", CinemaId = "CIN_CANCEL_A", RoomName = "Room A", Capacity = 2, RoomStatus = "ACTIVE" },
            new Room { RoomId = "ROOM_CANCEL_B", CinemaId = "CIN_CANCEL_B", RoomName = "Room B", Capacity = 1, RoomStatus = "ACTIVE" });
        db.Seats.AddRange(
            new Seat { SeatId = "SEAT_CANCEL_A1", RoomId = "ROOM_CANCEL_A", SeatTypeId = "SEAT_TYPE_CANCEL", RowLabel = "A", SeatNumber = 1, SeatCode = "A1", IsActive = true },
            new Seat { SeatId = "SEAT_CANCEL_A2", RoomId = "ROOM_CANCEL_A", SeatTypeId = "SEAT_TYPE_CANCEL", RowLabel = "A", SeatNumber = 2, SeatCode = "A2", IsActive = true },
            new Seat { SeatId = "SEAT_CANCEL_B1", RoomId = "ROOM_CANCEL_B", SeatTypeId = "SEAT_TYPE_CANCEL", RowLabel = "A", SeatNumber = 1, SeatCode = "A1", IsActive = true });
        db.Showtimes.AddRange(
            new Showtime
            {
                ShowtimeId = "SHW_CANCEL_A",
                MovieId = "MOV_CANCEL",
                RoomId = "ROOM_CANCEL_A",
                StartTime = now.AddDays(1),
                EndTime = now.AddDays(1).AddHours(2),
                BasePrice = 100000m,
                Status = BookingConstants.ShowtimeStatus.Open,
                CreatedAt = now
            },
            new Showtime
            {
                ShowtimeId = "SHW_CANCEL_B",
                MovieId = "MOV_CANCEL",
                RoomId = "ROOM_CANCEL_B",
                StartTime = now.AddDays(1),
                EndTime = now.AddDays(1).AddHours(2),
                BasePrice = 100000m,
                Status = BookingConstants.ShowtimeStatus.Open,
                CreatedAt = now
            },
            new Showtime
            {
                ShowtimeId = "SHW_CANCEL_EMPTY",
                MovieId = "MOV_CANCEL",
                RoomId = "ROOM_CANCEL_A",
                StartTime = now.AddDays(2),
                EndTime = now.AddDays(2).AddHours(2),
                BasePrice = 100000m,
                Status = BookingConstants.ShowtimeStatus.Open,
                CreatedAt = now
            });
        db.ShowtimeSeats.AddRange(
            new ShowtimeSeat { ShowtimeSeatId = "STS_CANCEL_A_PAID", ShowtimeId = "SHW_CANCEL_A", SeatId = "SEAT_CANCEL_A1", SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked, RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_CANCEL_A_PENDING", ShowtimeId = "SHW_CANCEL_A", SeatId = "SEAT_CANCEL_A2", SeatStatus = BookingConstants.ShowtimeSeatStatus.Locked, LockedUntil = now.AddMinutes(10), LockedByUserId = "USR_CANCEL_CUSTOMER_A", RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_CANCEL_B_PAID", ShowtimeId = "SHW_CANCEL_B", SeatId = "SEAT_CANCEL_B1", SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked, RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_CANCEL_EMPTY_A1", ShowtimeId = "SHW_CANCEL_EMPTY", SeatId = "SEAT_CANCEL_A1", SeatStatus = BookingConstants.ShowtimeSeatStatus.Available, RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_CANCEL_EMPTY_A2", ShowtimeId = "SHW_CANCEL_EMPTY", SeatId = "SEAT_CANCEL_A2", SeatStatus = BookingConstants.ShowtimeSeatStatus.Available, RowVersion = new byte[8] });

        db.PaymentProviders.Add(new PaymentProvider
        {
            PaymentProviderId = "PAYPROV_CANCEL",
            ProviderName = "SEPAY_CANCEL",
            ProviderStatus = "ACTIVE"
        });
        db.BankDirectories.Add(new BankDirectory
        {
            BankCode = "VCB",
            BankBin = "970436",
            ShortName = "Vietcombank",
            FullName = "Joint Stock Commercial Bank for Foreign Trade of Vietnam",
            IsActive = true,
            CreatedAt = now
        });

        db.Bookings.AddRange(
            new Booking
            {
                BookingId = "BKG_CANCEL_A_PAID",
                CustomerProfileId = "CUS_CANCEL_A",
                ShowtimeId = "SHW_CANCEL_A",
                BookingStatus = BookingConstants.BookingStatus.Paid,
                TotalAmount = 100000m,
                CreatedAt = now,
                BookingChannel = BookingConstants.BookingChannel.Online
            },
            new Booking
            {
                BookingId = "BKG_CANCEL_A_PENDING",
                CustomerProfileId = "CUS_CANCEL_A",
                ShowtimeId = "SHW_CANCEL_A",
                BookingStatus = BookingConstants.BookingStatus.PendingPayment,
                TotalAmount = 100000m,
                CreatedAt = now,
                ExpiredAt = now.AddMinutes(10),
                BookingChannel = BookingConstants.BookingChannel.Online
            },
            new Booking
            {
                BookingId = "BKG_CANCEL_B_PAID",
                CustomerProfileId = "CUS_CANCEL_B",
                ShowtimeId = "SHW_CANCEL_B",
                BookingStatus = BookingConstants.BookingStatus.Paid,
                TotalAmount = 100000m,
                CreatedAt = now,
                BookingChannel = BookingConstants.BookingChannel.Online
            });
        db.BookingSeats.AddRange(
            new BookingSeat { BookingSeatId = "BKS_CANCEL_A_PAID", BookingId = "BKG_CANCEL_A_PAID", ShowtimeSeatId = "STS_CANCEL_A_PAID", SeatPrice = 100000m },
            new BookingSeat { BookingSeatId = "BKS_CANCEL_A_PENDING", BookingId = "BKG_CANCEL_A_PENDING", ShowtimeSeatId = "STS_CANCEL_A_PENDING", SeatPrice = 100000m },
            new BookingSeat { BookingSeatId = "BKS_CANCEL_B_PAID", BookingId = "BKG_CANCEL_B_PAID", ShowtimeSeatId = "STS_CANCEL_B_PAID", SeatPrice = 100000m });
        db.Tickets.AddRange(
            new Ticket { TicketId = "TCK_CANCEL_A_PAID", BookingSeatId = "BKS_CANCEL_A_PAID", QrCode = "QR_CANCEL_A", TicketStatus = BookingConstants.TicketStatus.Unused, GeneratedAt = now },
            new Ticket { TicketId = "TCK_CANCEL_B_PAID", BookingSeatId = "BKS_CANCEL_B_PAID", QrCode = "QR_CANCEL_B", TicketStatus = BookingConstants.TicketStatus.Unused, GeneratedAt = now });
        db.Payments.AddRange(
            new Payment
            {
                PaymentId = "PAY_CANCEL_A_SUCCESS",
                BookingId = "BKG_CANCEL_A_PAID",
                PaymentProviderId = "PAYPROV_CANCEL",
                Amount = 100000m,
                PaymentStatus = BookingConstants.PaymentStatus.Success,
                PaymentMethod = "SEPAY",
                TransactionCode = "TCANCELA001",
                CreatedAt = now,
                PaidAt = now
            },
            new Payment
            {
                PaymentId = "PAY_CANCEL_A_PENDING",
                BookingId = "BKG_CANCEL_A_PENDING",
                PaymentProviderId = "PAYPROV_CANCEL",
                Amount = 100000m,
                PaymentStatus = BookingConstants.PaymentStatus.Pending,
                PaymentMethod = "SEPAY",
                TransactionCode = "TCANCELA002",
                CreatedAt = now
            },
            new Payment
            {
                PaymentId = "PAY_CANCEL_B_SUCCESS",
                BookingId = "BKG_CANCEL_B_PAID",
                PaymentProviderId = "PAYPROV_CANCEL",
                Amount = 100000m,
                PaymentStatus = BookingConstants.PaymentStatus.Success,
                PaymentMethod = "SEPAY",
                TransactionCode = "TCANCELB001",
                CreatedAt = now,
                PaidAt = now
            });

        await db.SaveChangesAsync();
        await CinemaScopeTestData.SeedManagerScopeAsync(factory, "CIN_CANCEL_A");
    }

    private static async Task SeedExistingRefundsAsync(CinemaWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var now = DateTime.UtcNow;

        db.Refunds.AddRange(
            new Refund
            {
                RefundId = "REF_SCOPE_A",
                BookingId = "BKG_CANCEL_A_PAID",
                PaymentId = "PAY_CANCEL_A_SUCCESS",
                PaymentProviderId = "PAYPROV_CANCEL",
                RefundAmount = 100000m,
                RefundStatus = BookingConstants.RefundStatus.Pending,
                RefundReason = "Scope A",
                RequestedAt = now
            },
            new Refund
            {
                RefundId = "REF_SCOPE_B",
                BookingId = "BKG_CANCEL_B_PAID",
                PaymentId = "PAY_CANCEL_B_SUCCESS",
                PaymentProviderId = "PAYPROV_CANCEL",
                RefundAmount = 100000m,
                RefundStatus = BookingConstants.RefundStatus.Pending,
                RefundReason = "Scope B",
                RequestedAt = now
            });

        await db.SaveChangesAsync();
    }

    private static async Task<T?> DeserializeAsync<T>(HttpResponseMessage response)
    {
        return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStringAsync(), JsonOptions);
    }
}
