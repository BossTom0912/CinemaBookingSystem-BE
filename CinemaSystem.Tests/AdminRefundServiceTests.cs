using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using CinemaSystem.Contracts.Refunds;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Services;
using CinemaSystem.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace CinemaSystem.Tests;

/// <summary>
/// Unit tests for AdminRefundService verifying admin manual refund processing,
/// showtime cancellations with automatic refunds, and refund queries.
/// </summary>
public sealed class AdminRefundServiceTests
{
    private const string AdminUserId = "USR_ADMIN_REFUND_01";

    [Fact]
    public async Task GetRefundsAsync_ReturnsPagedRefundList()
    {
        var fixture = await TestFixture.CreateAsync();
        await fixture.SeedRefundAsync("REF_001", DomainConstants.RefundStatus.Pending, 150000);

        var result = await fixture.Service.GetRefundsAsync(
            status: DomainConstants.RefundStatus.Pending,
            pageIndex: 1,
            pageSize: 10,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Single(result.Data.Items);
        Assert.Equal("REF_001", result.Data.Items.First().RefundId);
    }

    [Fact]
    public async Task ConfirmRefundAsync_PendingRefund_ConfirmsRefundSuccessfully()
    {
        var fixture = await TestFixture.CreateAsync();
        await fixture.SeedRefundAsync("REF_002", DomainConstants.RefundStatus.Pending, 200000);

        var result = await fixture.Service.ConfirmRefundAsync(
            "BKG_REF_002",
            AdminUserId,
            CancellationToken.None);

        Assert.True(result.Success);
        var refund = await fixture.DbContext.Refunds.FirstOrDefaultAsync(r => r.RefundId == "REF_002");
        Assert.Equal(DomainConstants.RefundStatus.Success, refund!.RefundStatus);
    }

    [Fact]
    public async Task CancelShowtimesAndRefundAsync_ValidShowtimes_CancelsAndCreatesRefunds()
    {
        var fixture = await TestFixture.CreateAsync();
        await fixture.SeedRefundAsync("REF_003", DomainConstants.RefundStatus.Pending, 100000);

        var result = await fixture.Service.CancelShowtimesAndRefundAsync(
            new[] { "SHW_REF_003" },
            reason: "Technical issues in room",
            forceCancel: true,
            actionUserId: AdminUserId,
            cancellationToken: CancellationToken.None);

        Assert.True(result.Success);
    }

    private sealed class TestFixture
    {
        public CinemaDbContext DbContext { get; }
        public AdminRefundService Service { get; }

        private TestFixture(CinemaDbContext dbContext, AdminRefundService service)
        {
            DbContext = dbContext;
            Service = service;
        }

        public static async Task<TestFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<CinemaDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
                .Options;

            var dbContext = new CinemaDbContext(options);

            var bgJobClient = new Mock<Hangfire.IBackgroundJobClient>().Object;
            var service = new AdminRefundService(
                dbContext,
                new InMemorySeatLockStore(),
                Options.Create(new CinemaProcessingSettings()),
                bgJobClient,
                Options.Create(new EmailTemplatesSettings()),
                new Mock<IAiEmailService>().Object);

            return new TestFixture(dbContext, service);
        }

        public async Task SeedRefundAsync(string refundId, string status, decimal amount)
        {
            var cinemaId = "CIN_REFUND";
            DbContext.Cinemas.Add(new Cinema { CinemaId = cinemaId, CinemaName = "Refund Cinema", Address = "1 St", City = "HCM", CinemaStatus = DomainConstants.EntityStatus.Active });

            var roomId = $"ROOM_{refundId}";
            DbContext.Rooms.Add(new Room { RoomId = roomId, CinemaId = cinemaId, RoomName = "Room R", RoomStatus = DomainConstants.EntityStatus.Active });

            var movieId = $"MOV_{refundId}";
            DbContext.Movies.Add(new Movie { MovieId = movieId, Title = "Refund Movie", DurationMinutes = 100, MovieStatus = DomainConstants.EntityStatus.Active });

            var showtimeId = $"SHW_{refundId}";
            DbContext.Showtimes.Add(new Showtime { ShowtimeId = showtimeId, RoomId = roomId, MovieId = movieId, StartTime = DateTime.UtcNow, Status = DomainConstants.EntityStatus.Open });

            var bookingId = $"BKG_{refundId}";
            DbContext.Bookings.Add(new Booking { BookingId = bookingId, ShowtimeId = showtimeId, BookingStatus = DomainConstants.EntityStatus.PendingRefund, TotalAmount = amount, BookingChannel = DomainConstants.BookingChannel.Online });

            var providerId = "PROV_SEPAY";
            if (!DbContext.PaymentProviders.Any(p => p.PaymentProviderId == providerId))
            {
                DbContext.PaymentProviders.Add(new PaymentProvider { PaymentProviderId = providerId, ProviderName = "SePay", ProviderStatus = DomainConstants.EntityStatus.Active });
            }

            var paymentId = $"PAY_{refundId}";
            DbContext.Payments.Add(new Payment { PaymentId = paymentId, BookingId = bookingId, PaymentProviderId = providerId, Amount = amount, PaymentStatus = DomainConstants.PaymentStatus.Success, CreatedAt = DateTime.UtcNow });

            DbContext.Refunds.Add(new Refund
            {
                RefundId = refundId,
                BookingId = bookingId,
                PaymentId = paymentId,
                PaymentProviderId = providerId,
                RefundAmount = amount,
                RefundStatus = status,
                RequestedAt = DateTime.UtcNow
            });

            await DbContext.SaveChangesAsync();
        }
    }
}
