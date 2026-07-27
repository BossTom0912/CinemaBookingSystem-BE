using CinemaSystem.Contracts.Payments;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Services;
using CinemaSystem.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CinemaSystem.Tests;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task CreatePayment_CreatesPendingPaymentWithBankInfo()
    {
        var fixture = Fixture.Create();
        await fixture.SeedPendingBookingAsync();

        var result = await fixture.Service.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                BookingId = "BOOKING_TEST",
                PaymentProviderId = "PAYPROV_TEST_SEPAY"
            },
            "USER_TEST");

        Assert.Equal(120000m, result.Amount);
        Assert.Equal("Test Bank", result.BankName);
        Assert.Equal("123456789", result.BankAccount);
        Assert.Matches("^T[A-Z0-9]{10}$", result.TransactionCode);

        var payment = await fixture.DbContext.Payments.SingleAsync();
        Assert.Equal("PENDING", payment.PaymentStatus);
        Assert.Equal("SEPAY_TEST", payment.PaymentMethod);
    }

    [Fact]
    public async Task CreatePayment_ExistingPendingPayment_ReturnsExistingPayment()
    {
        var fixture = Fixture.Create();
        await fixture.SeedPendingBookingAsync();

        var first = await fixture.Service.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                BookingId = "BOOKING_TEST",
                PaymentProviderId = "PAYPROV_TEST_SEPAY"
            },
            "USER_TEST");
        var second = await fixture.Service.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                BookingId = "BOOKING_TEST",
                PaymentProviderId = "PAYPROV_TEST_SEPAY"
            },
            "USER_TEST");

        Assert.Equal(first.PaymentId, second.PaymentId);
        Assert.Equal(first.TransactionCode, second.TransactionCode);
        Assert.Single(await fixture.DbContext.Payments.ToListAsync());
    }

    [Fact]
    public async Task CreatePayment_DevelopmentPaymentAmountOverride_UsesConfiguredAmount()
    {
        var fixture = Fixture.Create(3000m);
        await fixture.SeedPendingBookingAsync();

        var result = await fixture.Service.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                BookingId = "BOOKING_TEST",
                PaymentProviderId = "PAYPROV_TEST_SEPAY"
            },
            "USER_TEST");

        Assert.Equal(3000m, result.Amount);

        var payment = await fixture.DbContext.Payments.SingleAsync();
        Assert.Equal(3000m, payment.Amount);
    }

    [Fact]
    public async Task CreatePayment_BookingOwnedByAnotherCustomer_ThrowsUnauthorized()
    {
        var fixture = Fixture.Create();
        await fixture.SeedPendingBookingAsync();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            fixture.Service.CreatePaymentAsync(
                new CreatePaymentRequest
                {
                    BookingId = "BOOKING_TEST",
                    PaymentProviderId = "PAYPROV_TEST_SEPAY"
                },
                "USER_OTHER"));

        Assert.Empty(await fixture.DbContext.Payments.ToListAsync());
    }

    [Fact]
    public async Task ConfirmPayment_ValidWebhookContent_MarksPaymentAndBookingPaid()
    {
        var fixture = Fixture.Create();
        await fixture.SeedPendingBookingAsync();
        var created = await fixture.Service.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                BookingId = "BOOKING_TEST",
                PaymentProviderId = "PAYPROV_TEST_SEPAY"
            },
            "USER_TEST");
        var rawPayload = $$"""{"content":"Cinema {{created.TransactionCode}}","transferAmount":120000,"referenceCode":"SEP123"}""";

        await fixture.Service.ConfirmPaymentAsync(
            $"Cinema {created.TransactionCode}",
            120000m,
            "SEP123",
            rawPayload);

        var payment = await fixture.DbContext.Payments.SingleAsync();
        var booking = await fixture.DbContext.Bookings.SingleAsync();
        Assert.Equal("SUCCESS", payment.PaymentStatus);
        Assert.Equal("SEP123", payment.ProviderTransactionCode);
        Assert.Equal(rawPayload, payment.RawCallbackPayload);
        Assert.NotNull(payment.PaidAt);
        Assert.Equal("PAID", booking.BookingStatus);
    }

    [Fact]
    public async Task ConfirmPayment_TimelyProviderPaymentAfterCleanup_RestoresBookingAndCreatesTicket()
    {
        var fixture = Fixture.Create();
        await fixture.SeedPendingBookingWithSeatAsync();
        var created = await fixture.Service.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                BookingId = "BOOKING_TEST",
                PaymentProviderId = "PAYPROV_TEST_SEPAY"
            },
            "USER_TEST");

        var expiry = DateTime.UtcNow.AddMinutes(-1);
        var providerPaidAt = expiry.AddMinutes(-5);
        var booking = await fixture.DbContext.Bookings.SingleAsync();
        var payment = await fixture.DbContext.Payments.SingleAsync();
        var showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
        booking.BookingStatus = "CANCELLED";
        booking.ExpiredAt = expiry;
        payment.PaymentStatus = "EXPIRED";
        showtimeSeat.SeatStatus = "AVAILABLE";
        showtimeSeat.LockedUntil = null;
        showtimeSeat.LockedByUserId = null;
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Service.ConfirmPaymentAsync(
            $"Cinema {created.TransactionCode}",
            120000m,
            "SEP_TIMELY",
            """{"referenceCode":"SEP_TIMELY"}""",
            CancellationToken.None,
            providerPaidAt);

        payment = await fixture.DbContext.Payments.SingleAsync();
        booking = await fixture.DbContext.Bookings.SingleAsync();
        showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
        Assert.Equal("SUCCESS", payment.PaymentStatus);
        Assert.Equal(providerPaidAt, payment.PaidAt);
        Assert.Equal("PAID", booking.BookingStatus);
        Assert.Equal("BOOKED", showtimeSeat.SeatStatus);
        Assert.Single(await fixture.DbContext.Tickets.ToListAsync());
        Assert.Empty(await fixture.DbContext.Refunds.ToListAsync());
    }

    [Fact]
    public async Task ConfirmPayment_ProviderPaymentAfterExpiry_KeepsSeatAndCreatesRefund()
    {
        var fixture = Fixture.Create();
        await fixture.SeedPendingBookingWithSeatAsync();
        var created = await fixture.Service.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                BookingId = "BOOKING_TEST",
                PaymentProviderId = "PAYPROV_TEST_SEPAY"
            },
            "USER_TEST");

        var expiry = DateTime.UtcNow.AddMinutes(-5);
        var providerPaidAt = expiry.AddMinutes(1);
        var booking = await fixture.DbContext.Bookings.SingleAsync();
        var payment = await fixture.DbContext.Payments.SingleAsync();
        var showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
        booking.BookingStatus = "CANCELLED";
        booking.ExpiredAt = expiry;
        payment.PaymentStatus = "EXPIRED";
        showtimeSeat.SeatStatus = "AVAILABLE";
        showtimeSeat.LockedUntil = null;
        showtimeSeat.LockedByUserId = null;
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Service.ConfirmPaymentAsync(
            $"Cinema {created.TransactionCode}",
            120000m,
            "SEP_LATE",
            """{"referenceCode":"SEP_LATE"}""",
            CancellationToken.None,
            providerPaidAt);

        payment = await fixture.DbContext.Payments.SingleAsync();
        booking = await fixture.DbContext.Bookings.SingleAsync();
        showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
        Assert.Equal("SUCCESS", payment.PaymentStatus);
        Assert.Equal("REFUND_PENDING", booking.BookingStatus);
        Assert.Equal("AVAILABLE", showtimeSeat.SeatStatus);
        Assert.Empty(await fixture.DbContext.Tickets.ToListAsync());
        Assert.Single(await fixture.DbContext.Refunds.ToListAsync());
    }

    [Fact]
    public async Task ConfirmPayment_TimelyProviderPaymentButSeatRelocked_CreatesRefund()
    {
        var fixture = Fixture.Create();
        await fixture.SeedPendingBookingWithSeatAsync();
        var created = await fixture.Service.CreatePaymentAsync(
            new CreatePaymentRequest
            {
                BookingId = "BOOKING_TEST",
                PaymentProviderId = "PAYPROV_TEST_SEPAY"
            },
            "USER_TEST");

        var expiry = DateTime.UtcNow.AddMinutes(-1);
        var providerPaidAt = expiry.AddMinutes(-5);
        var booking = await fixture.DbContext.Bookings.SingleAsync();
        var payment = await fixture.DbContext.Payments.SingleAsync();
        var showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
        booking.BookingStatus = "CANCELLED";
        booking.ExpiredAt = expiry;
        payment.PaymentStatus = "EXPIRED";
        showtimeSeat.SeatStatus = "LOCKED";
        showtimeSeat.LockedUntil = DateTime.UtcNow.AddMinutes(5);
        showtimeSeat.LockedByUserId = "USER_OTHER";
        await fixture.DbContext.SaveChangesAsync();

        await fixture.Service.ConfirmPaymentAsync(
            $"Cinema {created.TransactionCode}",
            120000m,
            "SEP_RELOCKED",
            """{"referenceCode":"SEP_RELOCKED"}""",
            CancellationToken.None,
            providerPaidAt);

        booking = await fixture.DbContext.Bookings.SingleAsync();
        showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
        Assert.Equal("REFUND_PENDING", booking.BookingStatus);
        Assert.Equal("LOCKED", showtimeSeat.SeatStatus);
        Assert.Equal("USER_OTHER", showtimeSeat.LockedByUserId);
        Assert.Empty(await fixture.DbContext.Tickets.ToListAsync());
        Assert.Single(await fixture.DbContext.Refunds.ToListAsync());
    }

    private sealed class Fixture
    {
        private Fixture(CinemaDbContext dbContext, PaymentService service)
        {
            DbContext = dbContext;
            Service = service;
        }

        public CinemaDbContext DbContext { get; }

        public PaymentService Service { get; }

        public static Fixture Create(
            decimal? paymentAmountOverride = null,
            IEnumerable<IPaymentGateway>? paymentGateways = null)
        {
            var options = new DbContextOptionsBuilder<CinemaDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var dbContext = new CinemaDbContext(options);
            var refundSettings = Options.Create(new RefundSettings
            {
                FrontendBaseUrl = "https://frontend.test",
                ClaimTokenMinutes = 5
            });
            var service = new PaymentService(
                dbContext,
                Options.Create(new SepaySettings
                {
                    WebhookSecret = "test-secret",
                    BankName = "Test Bank",
                    BankAccount = "123456789",
                    DevelopmentPaymentAmountOverride = paymentAmountOverride
                }),
                Options.Create(new BookingSettings()),
                new RefundClaimIssuer(refundSettings),
                Mock.Of<IEmailSender>(),
                refundSettings,
                new CinemaSystem.Infrastructure.Time.SystemClock(),
                NullLogger<PaymentService>.Instance,
                new VoucherReservationService(dbContext),
                new CancellationCompensationService(
                    dbContext,
                    Options.Create(new CancellationCompensationSettings()),
                    new CinemaSystem.Infrastructure.Time.SystemClock()),
                new VoucherService(dbContext, new CinemaSystem.Infrastructure.Time.SystemClock()),
                paymentGateways ?? Array.Empty<IPaymentGateway>());

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
            DbContext.Roles.Add(new Role
            {
                RoleId = "ROLE_CUSTOMER",
                RoleName = "CUSTOMER",
                Description = "Customer"
            });
            DbContext.Users.Add(new User
            {
                UserId = "USER_TEST",
                RoleId = "ROLE_CUSTOMER",
                Email = "customer@test.com",
                PasswordHash = "HASH",
                FullName = "Customer Test",
                PhoneNumber = "0900000000",
                Status = "ACTIVE",
                EmailVerified = true,
                CreatedAt = DateTime.UtcNow
            });
            DbContext.CustomerProfiles.Add(new CustomerProfile
            {
                CustomerProfileId = "CUS_TEST",
                UserId = "USER_TEST",
                MemberLevel = "BRONZE",
                RewardPoints = 0
            });
            DbContext.Showtimes.Add(new Showtime
            {
                ShowtimeId = "SHOWTIME_TEST",
                MovieId = "MOV_TEST",
                RoomId = "ROOM_TEST",
                StartTime = DateTime.UtcNow.AddDays(1),
                EndTime = DateTime.UtcNow.AddDays(1).AddHours(2),
                BasePrice = 120000m,
                Status = "OPEN",
                CreatedAt = DateTime.UtcNow
            });
            DbContext.Bookings.Add(new Booking
            {
                BookingId = "BOOKING_TEST",
                CustomerProfileId = "CUS_TEST",
                ShowtimeId = "SHOWTIME_TEST",
                BookingStatus = "PENDING_PAYMENT",
                TotalAmount = 120000m,
                CreatedAt = DateTime.UtcNow,
                BookingChannel = "ONLINE"
            });

            await DbContext.SaveChangesAsync();
        }

        public async Task SeedPendingBookingWithSeatAsync()
        {
            await SeedPendingBookingAsync();
            DbContext.ShowtimeSeats.Add(new ShowtimeSeat
            {
                ShowtimeSeatId = "SHOWTIME_SEAT_A5",
                ShowtimeId = "SHOWTIME_TEST",
                SeatId = "SEAT_A5",
                SeatStatus = "AVAILABLE",
                RowVersion = Array.Empty<byte>()
            });
            DbContext.BookingSeats.Add(new BookingSeat
            {
                BookingSeatId = "BOOKING_SEAT_A5",
                BookingId = "BOOKING_TEST",
                ShowtimeSeatId = "SHOWTIME_SEAT_A5",
                SeatPrice = 120000m
            });
            await DbContext.SaveChangesAsync();
        }
    }
}
