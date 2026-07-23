using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Contracts.Compensations;
using CinemaSystem.Contracts.Refunds;
using CinemaSystem.Contracts.Showtimes;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CinemaSystem.Tests;

public sealed class ShowtimeCancellationApiIntegrationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public async Task CancelShowtime_ManagerAssignedCinema_IssuesCompensationWithoutCashRefund()
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
        Assert.Equal(1, body.Data.PaidBookingsCompensated);
        Assert.Equal(1, body.Data.TicketVouchersIssued);
        Assert.Equal(1, body.Data.ComboVouchersIssued);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var showtime = await db.Showtimes.SingleAsync(item => item.ShowtimeId == "SHW_CANCEL_A");
        Assert.Equal(BookingConstants.ShowtimeStatus.Cancelled, showtime.Status);

        var cancellation = await db.ShowtimeCancellations.SingleAsync(item => item.ShowtimeId == "SHW_CANCEL_A");
        Assert.Equal("Projector failure", cancellation.CancelReason);
        Assert.Equal("USR_TEST_MANAGER", cancellation.CancelledByUserId);
        Assert.NotNull(cancellation.CancelledByStaffId);

        var paidBooking = await db.Bookings.SingleAsync(item => item.BookingId == "BKG_CANCEL_A_PAID");
        Assert.Equal(BookingConstants.BookingStatus.Cancelled, paidBooking.BookingStatus);

        var pendingBooking = await db.Bookings.SingleAsync(item => item.BookingId == "BKG_CANCEL_A_PENDING");
        Assert.Equal(BookingConstants.BookingStatus.Cancelled, pendingBooking.BookingStatus);

        var pendingPayment = await db.Payments.SingleAsync(item => item.PaymentId == "PAY_CANCEL_A_PENDING");
        Assert.Equal(BookingConstants.PaymentStatus.Cancelled, pendingPayment.PaymentStatus);

        var ticket = await db.Tickets.SingleAsync(item => item.TicketId == "TCK_CANCEL_A_PAID");
        Assert.Equal(BookingConstants.TicketStatus.Cancelled, ticket.TicketStatus);

        Assert.True(await db.Refunds.AnyAsync(item => item.BookingId == "BKG_CANCEL_A_PAID"));
        Assert.Single(await db.RefundClaims.ToListAsync());

        var compensation = await db.CancellationCompensations
            .Include(item => item.Tickets)
            .Include(item => item.Combo)
            .SingleAsync(item => item.SourceBookingId == "BKG_CANCEL_A_PAID");
        Assert.Equal(
            DomainConstants.CancellationCompensationPolicy.Version,
            compensation.PolicyVersion);
        Assert.Equal(
            DomainConstants.CancellationCompensationStatus.Issued,
            compensation.Status);
        Assert.InRange(
            compensation.ExpiresAt,
            compensation.IssuedAt.AddDays(179),
            compensation.IssuedAt.AddDays(181));
        Assert.Single(compensation.Tickets);
        Assert.StartsWith("TICKET-", compensation.Tickets.Single().VoucherCode);
        Assert.NotNull(compensation.Combo);
        Assert.StartsWith("COMBO-", compensation.Combo!.VoucherCode);
        Assert.Equal(
            DomainConstants.CancellationCompensationPolicy.ComboDisplayName,
            compensation.Combo.DisplayName);

        Assert.Equal(2, await db.Notifications.CountAsync(item => item.UserId == "USR_CANCEL_CUSTOMER_A"));
        Assert.True(await db.AuditLogs.AnyAsync(item =>
            item.Action == "CANCEL_SHOWTIME"
            && item.EntityId == "SHW_CANCEL_A"));
        Assert.Equal(2, factory.EmailCapture.Emails.Count);
        Assert.DoesNotContain(
            factory.EmailCapture.Emails,
            item => item.Body.Contains("/refunds/claim", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task CancelShowtime_ZeroAmountPaidBookingWithoutPayment_IssuesCompensation()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);

        await using (var setupScope = factory.Services.CreateAsyncScope())
        {
            var db = setupScope.ServiceProvider.GetRequiredService<CinemaDbContext>();
            var now = DateTime.UtcNow;
            var showtimeSeat = await db.ShowtimeSeats.SingleAsync(item =>
                item.ShowtimeSeatId == "STS_CANCEL_EMPTY_A1");
            showtimeSeat.SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked;

            db.Bookings.Add(new Booking
            {
                BookingId = "BKG_CANCEL_ZERO_PAID",
                CustomerProfileId = "CUS_CANCEL_A",
                ShowtimeId = "SHW_CANCEL_EMPTY",
                BookingStatus = BookingConstants.BookingStatus.Paid,
                TotalAmount = 0m,
                CreatedAt = now,
                BookingChannel = BookingConstants.BookingChannel.Online
            });
            db.BookingSeats.Add(new BookingSeat
            {
                BookingSeatId = "BKS_CANCEL_ZERO_PAID",
                BookingId = "BKG_CANCEL_ZERO_PAID",
                ShowtimeSeatId = showtimeSeat.ShowtimeSeatId,
                SeatPrice = 100000m
            });
            db.Tickets.Add(new Ticket
            {
                TicketId = "TCK_CANCEL_ZERO_PAID",
                BookingSeatId = "BKS_CANCEL_ZERO_PAID",
                QrCode = "QR_CANCEL_ZERO_PAID",
                TicketStatus = BookingConstants.TicketStatus.Unused,
                GeneratedAt = now
            });
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        var response = await client.PostAsJsonAsync(
            "/api/manager/showtimes/SHW_CANCEL_EMPTY/cancel",
            new CancelShowtimeRequest { Reason = "Free booking cancellation coverage" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<CancelShowtimeResponse>>(response);
        Assert.True(body!.Success);
        Assert.Equal(1, body.Data!.PaidBookingsCompensated);
        Assert.Equal(1, body.Data.TicketVouchersIssued);
        Assert.Equal(1, body.Data.ComboVouchersIssued);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var booking = await verificationDb.Bookings.SingleAsync(item =>
            item.BookingId == "BKG_CANCEL_ZERO_PAID");
        var ticket = await verificationDb.Tickets.SingleAsync(item =>
            item.TicketId == "TCK_CANCEL_ZERO_PAID");
        var compensation = await verificationDb.CancellationCompensations
            .Include(item => item.Tickets)
            .Include(item => item.Combo)
            .SingleAsync(item => item.SourceBookingId == booking.BookingId);

        Assert.Equal(BookingConstants.BookingStatus.Cancelled, booking.BookingStatus);
        Assert.Equal(BookingConstants.TicketStatus.Cancelled, ticket.TicketStatus);
        Assert.False(await verificationDb.Payments.AnyAsync(item =>
            item.BookingId == booking.BookingId));
        Assert.False(await verificationDb.Refunds.AnyAsync(item =>
            item.BookingId == booking.BookingId));
        Assert.Single(compensation.Tickets);
        Assert.NotNull(compensation.Combo);
    }

    [Fact]
    public async Task LegacyDeleteShowtime_WithPaidBooking_TriggersRefundWorkflow()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);

        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());

        // The legacy Admin UI uses DELETE /api/showtimes/{id}; main keeps its
        // paid-booking cancellation contract on the REFUND_PENDING workflow.
        var response = await client.DeleteAsync("/api/showtimes/SHW_CANCEL_A");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await DeserializeAsync<ApiResponse<JsonElement>>(response);
        Assert.True(body!.Success);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();

        var booking = await db.Bookings.SingleAsync(item =>
            item.BookingId == "BKG_CANCEL_A_PAID");
        Assert.Equal(BookingConstants.BookingStatus.RefundPending, booking.BookingStatus);
        Assert.True(await db.Refunds.AnyAsync(item =>
            item.BookingId == booking.BookingId));
        Assert.False(await db.CancellationCompensations.AnyAsync(item =>
            item.SourceBookingId == booking.BookingId));
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
    public async Task CancelShowtime_SecondCall_ReturnsConflictAndDoesNotDuplicateCompensation()
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
        Assert.Single(await db.CancellationCompensations
            .Where(item => item.SourceBookingId == "BKG_CANCEL_A_PAID")
            .ToListAsync());
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
    public async Task CancelShowtime_LateSuccessfulPayment_IssuesCompensationWithoutCashRefund()
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
        var compensation = await db.CancellationCompensations
            .Include(item => item.Tickets)
            .Include(item => item.Combo)
            .SingleAsync(item => item.SourceBookingId == booking.BookingId);

        Assert.Equal(BookingConstants.PaymentStatus.Success, payment.PaymentStatus);
        Assert.Equal(BookingConstants.BookingStatus.Cancelled, booking.BookingStatus);
        Assert.Single(compensation.Tickets);
        Assert.NotNull(compensation.Combo);
        Assert.False(await db.Refunds.AnyAsync(item => item.PaymentId == payment.PaymentId));
        Assert.Single(await db.RefundClaims.ToListAsync());
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
    public async Task CancellationCompensation_EndToEnd_CoversCrossCinemaVipSeatAndCombo()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);

        using var managerClient = factory.CreateClient();
        managerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());
        var cancel = await managerClient.PostAsJsonAsync(
            "/api/manager/showtimes/SHW_CANCEL_A/cancel",
            new CancelShowtimeRequest { Reason = "Compensation end-to-end flow" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        using var customerClient = factory.CreateClient();
        customerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TestAuthTokens.Customer("USR_CANCEL_CUSTOMER_A"));

        var listResponse = await customerClient.GetAsync(
            "/api/customer/compensations");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listBody =
            await DeserializeAsync<ApiResponse<List<CompensationResponse>>>(
                listResponse);
        var compensation = Assert.Single(listBody!.Data!);
        var ticket = Assert.Single(compensation.Tickets);
        Assert.NotNull(compensation.Combo);
        Assert.Equal(
            DomainConstants.CompensationEntitlementStatus.Issued,
            ticket.Status);

        var stackingAttempt = await customerClient.PostAsJsonAsync(
            "/api/bookings",
            new CreateBookingRequest
            {
                ShowtimeId = "SHW_CANCEL_EMPTY_B",
                ShowtimeSeatIds = ["STS_CANCEL_EMPTY_B1"],
                VoucherCode = "STANDARD-VOUCHER",
                CompensationTicketCodes = [ticket.VoucherCode]
            });
        Assert.Equal(HttpStatusCode.BadRequest, stackingAttempt.StatusCode);
        var stackingBody =
            await DeserializeAsync<ApiResponse<BookingResponse>>(
                stackingAttempt);
        Assert.Equal("VOUCHER_STACKING_NOT_ALLOWED", stackingBody!.ErrorCode);

        var bookingResponse = await customerClient.PostAsJsonAsync(
            "/api/bookings",
            new CreateBookingRequest
            {
                ShowtimeId = "SHW_CANCEL_EMPTY_B",
                ShowtimeSeatIds = ["STS_CANCEL_EMPTY_B1"],
                CompensationTicketCodes = [ticket.VoucherCode]
            });
        Assert.Equal(HttpStatusCode.OK, bookingResponse.StatusCode);
        var bookingBody =
            await DeserializeAsync<ApiResponse<BookingResponse>>(
                bookingResponse);
        Assert.Equal(0m, bookingBody!.Data!.TotalAmount);
        Assert.Equal(
            BookingConstants.BookingStatus.Paid,
            bookingBody.Data.Status);

        var comboResponse = await managerClient.PostAsJsonAsync(
            "/api/staff/compensations/combos/redeem",
            new RedeemCompensationComboRequest
            {
                VoucherCode = compensation.Combo!.VoucherCode
            });
        Assert.Equal(HttpStatusCode.OK, comboResponse.StatusCode);

        var repeatedCombo = await managerClient.PostAsJsonAsync(
            "/api/staff/compensations/combos/redeem",
            new RedeemCompensationComboRequest
            {
                VoucherCode = compensation.Combo.VoucherCode
            });
        Assert.Equal(HttpStatusCode.Conflict, repeatedCombo.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var redeemedBooking = await db.Bookings
            .Include(item => item.BookingSeats)
                .ThenInclude(item => item.Ticket)
            .SingleAsync(item => item.BookingId == bookingBody.Data.BookingId);
        var redeemedCompensation = await db.CancellationCompensations
            .Include(item => item.Tickets)
            .Include(item => item.Combo)
            .SingleAsync(item =>
                item.CancellationCompensationId
                == compensation.CompensationId);
        Assert.Equal(0m, redeemedBooking.TotalAmount);
        Assert.Equal(300000m, redeemedBooking.CompensationDiscountAmount);
        Assert.Equal(300000m, redeemedBooking.BookingSeats.Single().SeatPrice);
        Assert.NotNull(redeemedBooking.BookingSeats.Single().Ticket);
        Assert.Equal(
            DomainConstants.CompensationEntitlementStatus.Redeemed,
            redeemedCompensation.Tickets.Single().Status);
        Assert.Equal(
            DomainConstants.CompensationEntitlementStatus.Redeemed,
            redeemedCompensation.Combo!.Status);
        Assert.Equal(
            DomainConstants.CancellationCompensationStatus.Used,
            redeemedCompensation.Status);
        Assert.Equal(
            "CIN_CANCEL_A",
            redeemedCompensation.Combo.RedeemedAtCinemaId);
        Assert.Single(await db.Refunds
            .Where(item => item.BookingId == "BKG_CANCEL_A_PAID")
            .ToListAsync());
    }

    [Fact]
    public async Task CompensationTicket_PendingCheckoutCancellation_ReturnsEntitlementToCustomer()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);

        using var managerClient = factory.CreateClient();
        managerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());
        var cancel = await managerClient.PostAsJsonAsync(
            "/api/manager/showtimes/SHW_CANCEL_A/cancel",
            new CancelShowtimeRequest { Reason = "Release reservation test" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        using var customerClient = factory.CreateClient();
        customerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TestAuthTokens.Customer("USR_CANCEL_CUSTOMER_A"));
        var listResponse = await customerClient.GetAsync(
            "/api/customer/compensations");
        var listBody =
            await DeserializeAsync<ApiResponse<List<CompensationResponse>>>(
                listResponse);
        var compensation = Assert.Single(listBody!.Data!);
        var ticket = Assert.Single(compensation.Tickets);

        var createResponse = await customerClient.PostAsJsonAsync(
            "/api/bookings",
            new CreateBookingRequest
            {
                ShowtimeId = "SHW_CANCEL_EMPTY",
                ShowtimeSeatIds =
                    ["STS_CANCEL_EMPTY_A1", "STS_CANCEL_EMPTY_A2"],
                CompensationTicketCodes = [ticket.VoucherCode]
            });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var createBody =
            await DeserializeAsync<ApiResponse<BookingResponse>>(
                createResponse);
        Assert.Equal(
            BookingConstants.BookingStatus.PendingPayment,
            createBody!.Data!.Status);
        Assert.Equal(100000m, createBody.Data.TotalAmount);

        var cancelBookingResponse = await customerClient.PostAsync(
            $"/api/bookings/{createBody.Data.BookingId}/cancel",
            content: null);
        Assert.Equal(HttpStatusCode.OK, cancelBookingResponse.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var releasedTicket = await db.CompensationTickets.SingleAsync(item =>
            item.CompensationTicketId == ticket.CompensationTicketId);
        var cancelledBooking = await db.Bookings.SingleAsync(item =>
            item.BookingId == createBody.Data.BookingId);
        Assert.Equal(
            DomainConstants.CompensationEntitlementStatus.Issued,
            releasedTicket.Status);
        Assert.Null(releasedTicket.ReservedBookingId);
        Assert.Null(releasedTicket.ReservedBookingSeatId);
        Assert.Equal(
            BookingConstants.BookingStatus.Cancelled,
            cancelledBooking.BookingStatus);
    }

    [Fact]
    public async Task CompensationTicket_PartialCheckoutPayment_ConfirmsEntitlement()
    {
        await using var factory = new CinemaWebApplicationFactory();
        await SeedCancellationDataAsync(factory);

        using var managerClient = factory.CreateClient();
        managerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", TestAuthTokens.Manager());
        var cancel = await managerClient.PostAsJsonAsync(
            "/api/manager/showtimes/SHW_CANCEL_A/cancel",
            new CancelShowtimeRequest { Reason = "Payment confirmation test" });
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);

        using var customerClient = factory.CreateClient();
        customerClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(
                "Bearer",
                TestAuthTokens.Customer("USR_CANCEL_CUSTOMER_A"));
        var listResponse = await customerClient.GetAsync(
            "/api/customer/compensations");
        var listBody =
            await DeserializeAsync<ApiResponse<List<CompensationResponse>>>(
                listResponse);
        var compensation = Assert.Single(listBody!.Data!);
        var ticket = Assert.Single(compensation.Tickets);

        var createResponse = await customerClient.PostAsJsonAsync(
            "/api/bookings",
            new CreateBookingRequest
            {
                ShowtimeId = "SHW_CANCEL_EMPTY",
                ShowtimeSeatIds =
                    ["STS_CANCEL_EMPTY_A1", "STS_CANCEL_EMPTY_A2"],
                CompensationTicketCodes = [ticket.VoucherCode]
            });
        var createBody =
            await DeserializeAsync<ApiResponse<BookingResponse>>(
                createResponse);
        Assert.Equal(100000m, createBody!.Data!.TotalAmount);

        var paymentResponse = await customerClient.PostAsJsonAsync(
            "/api/payment",
            new
            {
                BookingId = createBody.Data.BookingId,
                PaymentProviderId = "PAYPROV_CANCEL"
            });
        Assert.Equal(HttpStatusCode.OK, paymentResponse.StatusCode);
        var paymentBody = await DeserializeAsync<
            ApiResponse<CinemaSystem.Contracts.Payments.CreatePaymentResponse>>(
            paymentResponse);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var paymentService =
                scope.ServiceProvider.GetRequiredService<IPaymentService>();
            await paymentService.ConfirmPaymentAsync(
                $"Cinema {paymentBody!.Data!.TransactionCode}",
                100000m,
                "COMPENSATION-PARTIAL-PAYMENT");
        }

        await using var verificationScope =
            factory.Services.CreateAsyncScope();
        var db =
            verificationScope.ServiceProvider.GetRequiredService<CinemaDbContext>();
        var paidBooking = await db.Bookings.SingleAsync(item =>
            item.BookingId == createBody.Data.BookingId);
        var redeemedTicket = await db.CompensationTickets.SingleAsync(item =>
            item.CompensationTicketId == ticket.CompensationTicketId);
        var updatedCompensation =
            await db.CancellationCompensations.SingleAsync(item =>
                item.CancellationCompensationId
                == compensation.CompensationId);
        Assert.Equal(BookingConstants.BookingStatus.Paid, paidBooking.BookingStatus);
        Assert.Equal(
            DomainConstants.CompensationEntitlementStatus.Redeemed,
            redeemedTicket.Status);
        Assert.Equal(
            DomainConstants.CancellationCompensationStatus.PartiallyUsed,
            updatedCompensation.Status);
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
        db.SeatTypes.AddRange(
            new SeatType
            {
                SeatTypeId = "SEAT_TYPE_CANCEL",
                TypeName = "STANDARD",
                ExtraFee = 0
            },
            new SeatType
            {
                SeatTypeId = "SEAT_TYPE_CANCEL_VIP",
                TypeName = "VIP",
                ExtraFee = 200000m
            });
        db.Rooms.AddRange(
            new Room { RoomId = "ROOM_CANCEL_A", CinemaId = "CIN_CANCEL_A", RoomName = "Room A", Capacity = 2, RoomStatus = "ACTIVE" },
            new Room { RoomId = "ROOM_CANCEL_B", CinemaId = "CIN_CANCEL_B", RoomName = "Room B", Capacity = 1, RoomStatus = "ACTIVE" });
        db.Seats.AddRange(
            new Seat { SeatId = "SEAT_CANCEL_A1", RoomId = "ROOM_CANCEL_A", SeatTypeId = "SEAT_TYPE_CANCEL", RowLabel = "A", SeatNumber = 1, SeatCode = "A1", IsActive = true },
            new Seat { SeatId = "SEAT_CANCEL_A2", RoomId = "ROOM_CANCEL_A", SeatTypeId = "SEAT_TYPE_CANCEL", RowLabel = "A", SeatNumber = 2, SeatCode = "A2", IsActive = true },
            new Seat { SeatId = "SEAT_CANCEL_B1", RoomId = "ROOM_CANCEL_B", SeatTypeId = "SEAT_TYPE_CANCEL_VIP", RowLabel = "A", SeatNumber = 1, SeatCode = "A1", IsActive = true });
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
            },
            new Showtime
            {
                ShowtimeId = "SHW_CANCEL_EMPTY_B",
                MovieId = "MOV_CANCEL",
                RoomId = "ROOM_CANCEL_B",
                StartTime = now.AddDays(3),
                EndTime = now.AddDays(3).AddHours(2),
                BasePrice = 100000m,
                Status = BookingConstants.ShowtimeStatus.Open,
                CreatedAt = now
            });
        db.ShowtimeSeats.AddRange(
            new ShowtimeSeat { ShowtimeSeatId = "STS_CANCEL_A_PAID", ShowtimeId = "SHW_CANCEL_A", SeatId = "SEAT_CANCEL_A1", SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked, RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_CANCEL_A_PENDING", ShowtimeId = "SHW_CANCEL_A", SeatId = "SEAT_CANCEL_A2", SeatStatus = BookingConstants.ShowtimeSeatStatus.Locked, LockedUntil = now.AddMinutes(10), LockedByUserId = "USR_CANCEL_CUSTOMER_A", RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_CANCEL_B_PAID", ShowtimeId = "SHW_CANCEL_B", SeatId = "SEAT_CANCEL_B1", SeatStatus = BookingConstants.ShowtimeSeatStatus.Booked, RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_CANCEL_EMPTY_A1", ShowtimeId = "SHW_CANCEL_EMPTY", SeatId = "SEAT_CANCEL_A1", SeatStatus = BookingConstants.ShowtimeSeatStatus.Available, RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_CANCEL_EMPTY_A2", ShowtimeId = "SHW_CANCEL_EMPTY", SeatId = "SEAT_CANCEL_A2", SeatStatus = BookingConstants.ShowtimeSeatStatus.Available, RowVersion = new byte[8] },
            new ShowtimeSeat { ShowtimeSeatId = "STS_CANCEL_EMPTY_B1", ShowtimeId = "SHW_CANCEL_EMPTY_B", SeatId = "SEAT_CANCEL_B1", SeatStatus = BookingConstants.ShowtimeSeatStatus.Available, RowVersion = new byte[8] });

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
