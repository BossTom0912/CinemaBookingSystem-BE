using CinemaSystem.Contracts.Payments;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Tests;

public sealed class PaymentServiceTests
{
    [Fact]
    public async Task CreatePayment_CreatesPendingPaymentWithBankInfo()
    {
        var fixture = Fixture.Create();
        await fixture.SeedPendingBookingAsync();

        var result = await fixture.Service.CreatePaymentAsync(new CreatePaymentRequest
        {
            BookingId = "BOOKING_TEST",
            PaymentProviderId = "PAYPROV_TEST_SEPAY"
        });

        Assert.Equal(120000m, result.Amount);
        Assert.Equal("Test Bank", result.BankName);
        Assert.Equal("123456789", result.BankAccount);
        Assert.Matches("^T[A-Z0-9]{10}$", result.TransactionCode);

        var payment = await fixture.DbContext.Payments.SingleAsync();
        Assert.Equal("PENDING", payment.PaymentStatus);
        Assert.Equal("SEPAY", payment.PaymentMethod);
    }

    [Fact]
    public async Task CreatePayment_ExistingPendingPayment_ReturnsExistingPayment()
    {
        var fixture = Fixture.Create();
        await fixture.SeedPendingBookingAsync();

        var first = await fixture.Service.CreatePaymentAsync(new CreatePaymentRequest
        {
            BookingId = "BOOKING_TEST",
            PaymentProviderId = "PAYPROV_TEST_SEPAY"
        });
        var second = await fixture.Service.CreatePaymentAsync(new CreatePaymentRequest
        {
            BookingId = "BOOKING_TEST",
            PaymentProviderId = "PAYPROV_TEST_SEPAY"
        });

        Assert.Equal(first.PaymentId, second.PaymentId);
        Assert.Equal(first.TransactionCode, second.TransactionCode);
        Assert.Single(await fixture.DbContext.Payments.ToListAsync());
    }

    [Fact]
    public async Task ConfirmPayment_ValidWebhookContent_MarksPaymentAndBookingPaid()
    {
        var fixture = Fixture.Create();
        await fixture.SeedPendingBookingAsync();
        var created = await fixture.Service.CreatePaymentAsync(new CreatePaymentRequest
        {
            BookingId = "BOOKING_TEST",
            PaymentProviderId = "PAYPROV_TEST_SEPAY"
        });
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
                .ConfigureWarnings(warnings => warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var dbContext = new CinemaDbContext(options);
            var service = new PaymentService(
                dbContext,
                Options.Create(new SepaySettings
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
