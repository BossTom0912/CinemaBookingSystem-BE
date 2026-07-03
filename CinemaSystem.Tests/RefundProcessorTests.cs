using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Refunds;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace CinemaSystem.Tests;

public sealed class RefundProcessorTests
{
    [Fact]
    public async Task Process_Success_RefundsBookingTicketsAndRevertsEarnedPoints()
    {
        var fixture = await Fixture.CreateAsync(
            PaymentRefundGatewayResult.Success("PROVIDER_REFUND_001"));

        var result = await fixture.Processor.ProcessAsync(
            "REF_PROCESS",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(BookingConstants.RefundStatus.Success, result.Data!.RefundStatus);
        Assert.Equal(BookingConstants.BookingStatus.Refunded, result.Data.BookingStatus);
        Assert.Equal(50, result.Data.RewardPointsReverted);

        var refund = await fixture.DbContext.Refunds.SingleAsync();
        var booking = await fixture.DbContext.Bookings.SingleAsync();
        var ticket = await fixture.DbContext.Tickets.SingleAsync();
        var customer = await fixture.DbContext.CustomerProfiles.SingleAsync();

        Assert.Equal("PROVIDER_REFUND_001", refund.ProviderRefundCode);
        Assert.NotNull(refund.RefundedAt);
        Assert.Equal(BookingConstants.BookingStatus.Refunded, booking.BookingStatus);
        Assert.Equal(BookingConstants.TicketStatus.Refunded, ticket.TicketStatus);
        Assert.Equal(50, customer.RewardPoints);
        Assert.Contains(
            await fixture.DbContext.RewardPointTransactions.ToListAsync(),
            item =>
                item.TransactionType == BookingConstants.RewardPointTransactionType.Revert
                && item.Points == -50);
        Assert.Contains(
            await fixture.DbContext.AuditLogs.ToListAsync(),
            item => item.Action == "PROCESS_REFUND");
        Assert.Single(fixture.EmailCapture!.Emails);

        var repeated = await fixture.Processor.ProcessAsync(
            "REF_PROCESS",
            CancellationToken.None);

        Assert.True(repeated.Data!.AlreadyProcessed);
        Assert.Equal(1, fixture.Gateway.CallCount);
        Assert.Single(await fixture.DbContext.RewardPointTransactions
            .Where(item =>
                item.TransactionType == BookingConstants.RewardPointTransactionType.Revert)
            .ToListAsync());
    }

    [Fact]
    public async Task Process_UnsupportedProvider_MarksManualRequiredWithoutRefundingTicket()
    {
        var fixture = await Fixture.CreateAsync(
            PaymentRefundGatewayResult.Unsupported(
                "Automatic refunds are not configured."));

        var result = await fixture.Processor.ProcessAsync(
            "REF_PROCESS",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(
            BookingConstants.RefundStatus.ManualRequired,
            result.Data!.RefundStatus);

        var refund = await fixture.DbContext.Refunds.SingleAsync();
        var booking = await fixture.DbContext.Bookings.SingleAsync();
        var ticket = await fixture.DbContext.Tickets.SingleAsync();

        Assert.Equal(
            BookingConstants.RefundStatus.ManualRequired,
            refund.RefundStatus);
        Assert.Equal(
            BookingConstants.BookingStatus.RefundPending,
            booking.BookingStatus);
        Assert.Equal(BookingConstants.TicketStatus.Cancelled, ticket.TicketStatus);
        Assert.Equal("Automatic refunds are not configured.", refund.FailureReason);
        Assert.DoesNotContain(
            await fixture.DbContext.RewardPointTransactions.ToListAsync(),
            item =>
                item.TransactionType == BookingConstants.RewardPointTransactionType.Revert);
    }

    [Fact]
    public async Task Process_EmailFailure_DoesNotRollbackSuccessfulRefund()
    {
        var fixture = await Fixture.CreateAsync(
            PaymentRefundGatewayResult.Success("PROVIDER_REFUND_002"),
            new ThrowingEmailSender());

        var result = await fixture.Processor.ProcessAsync(
            "REF_PROCESS",
            CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(BookingConstants.RefundStatus.Success, result.Data!.RefundStatus);
        Assert.Equal(
            BookingConstants.RefundStatus.Success,
            (await fixture.DbContext.Refunds.SingleAsync()).RefundStatus);
    }

    private sealed class Fixture
    {
        private Fixture(
            CinemaDbContext dbContext,
            RefundProcessor processor,
            FakeRefundGateway gateway,
            FakeEmailCapture? emailCapture)
        {
            DbContext = dbContext;
            Processor = processor;
            Gateway = gateway;
            EmailCapture = emailCapture;
        }

        public CinemaDbContext DbContext { get; }

        public RefundProcessor Processor { get; }

        public FakeRefundGateway Gateway { get; }

        public FakeEmailCapture? EmailCapture { get; }

        public static async Task<Fixture> CreateAsync(
            PaymentRefundGatewayResult gatewayResult,
            IEmailSender? emailSender = null)
        {
            var options = new DbContextOptionsBuilder<CinemaDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(warnings =>
                    warnings.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var dbContext = new CinemaDbContext(options);
            var gateway = new FakeRefundGateway(gatewayResult);
            var emailCapture = emailSender is null ? new FakeEmailCapture() : null;
            var effectiveEmailSender = emailSender ?? emailCapture!;
            var clock = new FixedClock(
                new DateTime(2026, 6, 25, 10, 0, 0, DateTimeKind.Utc));
            var processor = new RefundProcessor(
                dbContext,
                gateway,
                effectiveEmailSender,
                clock,
                NullLogger<RefundProcessor>.Instance);

            await SeedAsync(dbContext, clock.UtcNow);
            return new Fixture(dbContext, processor, gateway, emailCapture);
        }

        private static async Task SeedAsync(CinemaDbContext db, DateTime now)
        {
            db.Roles.AddRange(
                new Role
                {
                    RoleId = AuthConstants.RoleIds.Customer,
                    RoleName = AuthConstants.Roles.Customer
                },
                new Role
                {
                    RoleId = AuthConstants.RoleIds.Manager,
                    RoleName = AuthConstants.Roles.Manager
                });
            db.Users.AddRange(
                new User
                {
                    UserId = "USR_REFUND_CUSTOMER",
                    RoleId = AuthConstants.RoleIds.Customer,
                    Email = "refund-customer@test.com",
                    PasswordHash = "HASH",
                    FullName = "Refund Customer",
                    Status = AuthConstants.UserStatus.Active,
                    EmailVerified = true,
                    CreatedAt = now
                },
                new User
                {
                    UserId = "USR_REFUND_MANAGER",
                    RoleId = AuthConstants.RoleIds.Manager,
                    Email = "refund-manager@test.com",
                    PasswordHash = "HASH",
                    FullName = "Refund Manager",
                    Status = AuthConstants.UserStatus.Active,
                    EmailVerified = true,
                    CreatedAt = now
                });
            db.CustomerProfiles.Add(new CustomerProfile
            {
                CustomerProfileId = "CUS_REFUND",
                UserId = "USR_REFUND_CUSTOMER",
                MemberLevel = "STANDARD",
                RewardPoints = 100
            });
            db.Cinemas.Add(new Cinema
            {
                CinemaId = "CIN_REFUND",
                CinemaName = "Refund Cinema",
                Address = "A",
                City = "HCM",
                CinemaStatus = "ACTIVE"
            });
            db.Rooms.Add(new Room
            {
                RoomId = "ROOM_REFUND",
                CinemaId = "CIN_REFUND",
                RoomName = "Refund Room",
                Capacity = 1,
                RoomStatus = "ACTIVE"
            });
            db.SeatTypes.Add(new SeatType
            {
                SeatTypeId = "SEAT_TYPE_REFUND",
                TypeName = "STANDARD",
                ExtraFee = 0
            });
            db.Seats.Add(new Seat
            {
                SeatId = "SEAT_REFUND",
                RoomId = "ROOM_REFUND",
                SeatTypeId = "SEAT_TYPE_REFUND",
                SeatCode = "A1",
                RowLabel = "A",
                SeatNumber = 1,
                IsActive = true
            });
            db.Movies.Add(new Movie
            {
                MovieId = "MOV_REFUND",
                Title = "Refund Movie",
                DurationMinutes = 120,
                MovieStatus = "NOW_SHOWING"
            });
            db.Showtimes.Add(new Showtime
            {
                ShowtimeId = "SHW_REFUND",
                MovieId = "MOV_REFUND",
                RoomId = "ROOM_REFUND",
                StartTime = now.AddDays(1),
                EndTime = now.AddDays(1).AddHours(2),
                BasePrice = 100000m,
                Status = BookingConstants.ShowtimeStatus.Cancelled,
                CreatedAt = now
            });
            db.ShowtimeSeats.Add(new ShowtimeSeat
            {
                ShowtimeSeatId = "STS_REFUND",
                ShowtimeId = "SHW_REFUND",
                SeatId = "SEAT_REFUND",
                SeatStatus = BookingConstants.ShowtimeSeatStatus.Unavailable,
                RowVersion = []
            });
            db.Bookings.Add(new Booking
            {
                BookingId = "BKG_REFUND",
                CustomerProfileId = "CUS_REFUND",
                ShowtimeId = "SHW_REFUND",
                BookingStatus = BookingConstants.BookingStatus.RefundPending,
                BookingChannel = BookingConstants.BookingChannel.Online,
                TotalAmount = 100000m,
                CreatedAt = now
            });
            db.BookingSeats.Add(new BookingSeat
            {
                BookingSeatId = "BKS_REFUND",
                BookingId = "BKG_REFUND",
                ShowtimeSeatId = "STS_REFUND",
                SeatPrice = 100000m
            });
            db.Tickets.Add(new Ticket
            {
                TicketId = "TCK_REFUND",
                BookingSeatId = "BKS_REFUND",
                QrCode = "QR_REFUND",
                TicketStatus = BookingConstants.TicketStatus.Cancelled,
                GeneratedAt = now
            });
            db.PaymentProviders.Add(new PaymentProvider
            {
                PaymentProviderId = "PAYPROV_REFUND",
                ProviderName = "TEST_PROVIDER",
                ProviderStatus = "ACTIVE"
            });
            db.Payments.Add(new Payment
            {
                PaymentId = "PAY_REFUND",
                BookingId = "BKG_REFUND",
                PaymentProviderId = "PAYPROV_REFUND",
                Amount = 100000m,
                PaymentStatus = BookingConstants.PaymentStatus.Success,
                ProviderTransactionCode = "PAYMENT_PROVIDER_CODE",
                CreatedAt = now,
                PaidAt = now
            });
            db.ShowtimeCancellations.Add(new ShowtimeCancellation
            {
                ShowtimeCancellationId = "SHC_REFUND",
                ShowtimeId = "SHW_REFUND",
                CancelledByUserId = "USR_REFUND_MANAGER",
                CancelReason = "Technical failure",
                CancelledAt = now
            });
            db.Refunds.Add(new Refund
            {
                RefundId = "REF_PROCESS",
                BookingId = "BKG_REFUND",
                PaymentId = "PAY_REFUND",
                PaymentProviderId = "PAYPROV_REFUND",
                ShowtimeCancellationId = "SHC_REFUND",
                RefundAmount = 100000m,
                RefundStatus = BookingConstants.RefundStatus.Pending,
                RefundReason = "Technical failure",
                RequestedAt = now
            });
            db.RewardPointTransactions.Add(new RewardPointTransaction
            {
                RewardTransactionId = "RPT_EARN",
                CustomerProfileId = "CUS_REFUND",
                BookingId = "BKG_REFUND",
                TransactionType = BookingConstants.RewardPointTransactionType.Earn,
                Points = 50,
                CreatedAt = now
            });

            await db.SaveChangesAsync();
        }
    }

    private sealed class FakeRefundGateway : IPaymentRefundGateway
    {
        private readonly PaymentRefundGatewayResult _result;

        public FakeRefundGateway(PaymentRefundGatewayResult result)
        {
            _result = result;
        }

        public int CallCount { get; private set; }

        public Task<PaymentRefundGatewayResult> RefundAsync(
            PaymentRefundGatewayRequest request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(_result);
        }
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }
    }

    private sealed class ThrowingEmailSender : IEmailSender
    {
        public Task SendEmailAsync(
            string toEmail,
            string subject,
            string body,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("Simulated SMTP failure.");
        }
    }
}
