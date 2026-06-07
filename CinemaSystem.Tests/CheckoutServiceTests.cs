using System.ComponentModel.DataAnnotations;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Infrastructure.Bookings;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Persistence.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Tests;

public sealed class CheckoutServiceTests
{
    [Fact]
    public async Task Checkout_WithSeatsFoodAndVoucher_CreatesPendingBookingFromDatabasePrices()
    {
        var fixture = await CheckoutFixture.CreateAsync();
        var request = fixture.ValidRequest(
            foodItems: [new CheckoutFoodItemRequest { FbItemId = "FB_POPCORN", Quantity = 2 }],
            voucherCode: "SAVE10");

        var result = await fixture.Service.CheckoutAsync("USR_CUSTOMER", request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(201, result.StatusCode);
        Assert.NotNull(result.Data);
        Assert.Equal(120000m, result.Data.SeatSubtotal);
        Assert.Equal(80000m, result.Data.FoodSubtotal);
        Assert.Equal(200000m, result.Data.GrossAmount);
        Assert.Equal(20000m, result.Data.VoucherDiscount);
        Assert.Equal(180000m, result.Data.TotalAmount);
        Assert.Equal(0m, result.Data.RewardDiscount);

        var booking = await fixture.DbContext.Bookings
            .Include(item => item.BookingSeats)
            .Include(item => item.BookingFbItems)
            .Include(item => item.VoucherUsage)
            .SingleAsync();
        Assert.Equal(BookingConstants.BookingStatus.PendingPayment, booking.BookingStatus);
        Assert.Equal(BookingConstants.BookingChannel.Online, booking.BookingChannel);
        Assert.Equal(180000m, booking.TotalAmount);
        Assert.Equal(120000m, Assert.Single(booking.BookingSeats).SeatPrice);
        Assert.Equal(40000m, Assert.Single(booking.BookingFbItems).UnitPrice);
        Assert.Equal(80000m, Assert.Single(booking.BookingFbItems).Subtotal);
        Assert.Equal(BookingConstants.VoucherUsageStatus.Applied, booking.VoucherUsage!.UsageStatus);
        Assert.Equal(20000m, booking.VoucherUsage.DiscountAmount);

        var showtimeSeat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
        Assert.Equal(BookingConstants.ShowtimeSeatStatus.Locked, showtimeSeat.SeatStatus);
        Assert.Equal("USR_CUSTOMER", showtimeSeat.LockedByUserId);
        Assert.Equal(0, await fixture.DbContext.Payments.CountAsync());
        Assert.Equal(0, await fixture.DbContext.Tickets.CountAsync());
        Assert.Equal(0, (await fixture.DbContext.Vouchers.SingleAsync()).UsedCount);
        Assert.Equal(10, (await fixture.DbContext.CinemaFbInventories.SingleAsync()).Quantity);
    }

    [Fact]
    public async Task Checkout_SeatLockedByAnotherUser_ReturnsConflictWithoutCreatingBooking()
    {
        var fixture = await CheckoutFixture.CreateAsync();
        var seat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
        seat.LockedByUserId = "USR_OTHER";
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Service.CheckoutAsync(
            "USR_CUSTOMER",
            fixture.ValidRequest(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(BookingConstants.ErrorCodes.SeatNotLockedByUser, result.ErrorCode);
        Assert.Equal(0, await fixture.DbContext.Bookings.CountAsync());
    }

    [Fact]
    public async Task Checkout_ExpiredSeatLock_ReturnsConflictWithoutCreatingBooking()
    {
        var fixture = await CheckoutFixture.CreateAsync();
        var seat = await fixture.DbContext.ShowtimeSeats.SingleAsync();
        seat.LockedUntil = fixture.Clock.UtcNow.AddSeconds(-1);
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Service.CheckoutAsync(
            "USR_CUSTOMER",
            fixture.ValidRequest(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(BookingConstants.ErrorCodes.SeatLockExpired, result.ErrorCode);
        Assert.Equal(0, await fixture.DbContext.Bookings.CountAsync());
    }

    [Fact]
    public async Task Checkout_InsufficientFoodStock_ReturnsConflict()
    {
        var fixture = await CheckoutFixture.CreateAsync();
        var request = fixture.ValidRequest(
            foodItems: [new CheckoutFoodItemRequest { FbItemId = "FB_POPCORN", Quantity = 11 }]);

        var result = await fixture.Service.CheckoutAsync(
            "USR_CUSTOMER",
            request,
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(BookingConstants.ErrorCodes.InsufficientFoodStock, result.ErrorCode);
        Assert.Equal(0, await fixture.DbContext.Bookings.CountAsync());
    }

    [Fact]
    public async Task Checkout_VoucherBelowMinimum_ReturnsBadRequest()
    {
        var fixture = await CheckoutFixture.CreateAsync();
        var voucher = await fixture.DbContext.Vouchers.SingleAsync();
        voucher.MinOrderAmount = 200000m;
        await fixture.DbContext.SaveChangesAsync();

        var result = await fixture.Service.CheckoutAsync(
            "USR_CUSTOMER",
            fixture.ValidRequest(voucherCode: "SAVE10"),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(400, result.StatusCode);
        Assert.Equal(BookingConstants.ErrorCodes.VoucherMinOrderNotMet, result.ErrorCode);
        Assert.Equal(0, await fixture.DbContext.Bookings.CountAsync());
    }

    [Fact]
    public async Task Checkout_DatabaseConcurrencyFailure_ReturnsConflictAndDoesNotCreateBooking()
    {
        var fixture = await CheckoutFixture.CreateAsync(throwConcurrencyOnCheckout: true);

        var result = await fixture.Service.CheckoutAsync(
            "USR_CUSTOMER",
            fixture.ValidRequest(),
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal(BookingConstants.ErrorCodes.CheckoutConcurrencyConflict, result.ErrorCode);
        Assert.Equal(0, await fixture.DbContext.Bookings.CountAsync());
    }

    [Fact]
    public void CheckoutRequest_DuplicateSeatIds_FailsModelValidation()
    {
        var request = new CheckoutRequest
        {
            ShowtimeId = "SHO_001",
            ShowtimeSeatIds = ["STS_001", "sts_001"]
        };
        var validationResults = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(
            validationResults,
            item => item.MemberNames.Contains(nameof(CheckoutRequest.ShowtimeSeatIds)));
    }

    [Fact]
    public void CheckoutFoodItemRequest_ZeroQuantity_FailsModelValidation()
    {
        var request = new CheckoutFoodItemRequest
        {
            FbItemId = "FB_POPCORN",
            Quantity = 0
        };
        var validationResults = new List<ValidationResult>();

        var valid = Validator.TryValidateObject(
            request,
            new ValidationContext(request),
            validationResults,
            validateAllProperties: true);

        Assert.False(valid);
        Assert.Contains(
            validationResults,
            item => item.MemberNames.Contains(nameof(CheckoutFoodItemRequest.Quantity)));
    }

    private sealed class CheckoutFixture
    {
        private CheckoutFixture(
            TestCinemaDbContext dbContext,
            FakeClock clock,
            CheckoutService service)
        {
            DbContext = dbContext;
            Clock = clock;
            Service = service;
        }

        public TestCinemaDbContext DbContext { get; }

        public FakeClock Clock { get; }

        public CheckoutService Service { get; }

        public static async Task<CheckoutFixture> CreateAsync(bool throwConcurrencyOnCheckout = false)
        {
            var options = new DbContextOptionsBuilder<CinemaDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
                .ConfigureWarnings(builder => builder.Ignore(InMemoryEventId.TransactionIgnoredWarning))
                .Options;
            var dbContext = new TestCinemaDbContext(options);
            var clock = new FakeClock(new DateTime(2026, 6, 7, 8, 0, 0, DateTimeKind.Utc));
            var service = new CheckoutService(
                dbContext,
                clock,
                Options.Create(new BookingSettings
                {
                    OnlineSaleCutoffMinutes = 15,
                    MaxSeatsPerCheckout = 10
                }),
                NullLogger<CheckoutService>.Instance);

            await SeedAsync(dbContext, clock.UtcNow);
            dbContext.ThrowConcurrencyOnSave = throwConcurrencyOnCheckout;

            return new CheckoutFixture(dbContext, clock, service);
        }

        public CheckoutRequest ValidRequest(
            IReadOnlyList<CheckoutFoodItemRequest>? foodItems = null,
            string? voucherCode = null)
        {
            return new CheckoutRequest
            {
                ShowtimeId = "SHO_001",
                ShowtimeSeatIds = ["STS_001"],
                FoodItems = foodItems ?? [],
                VoucherCode = voucherCode
            };
        }

        private static async Task SeedAsync(TestCinemaDbContext dbContext, DateTime now)
        {
            var role = new Role
            {
                RoleId = AuthConstants.RoleIds.Customer,
                RoleName = AuthConstants.Roles.Customer,
                Description = "Customer"
            };
            var user = new User
            {
                UserId = "USR_CUSTOMER",
                RoleId = role.RoleId,
                Email = "customer@example.com",
                PasswordHash = "HASH",
                FullName = "Customer",
                Status = AuthConstants.UserStatus.Active,
                EmailVerified = true,
                CreatedAt = now
            };
            var customer = new CustomerProfile
            {
                CustomerProfileId = "CUS_001",
                UserId = user.UserId,
                MemberLevel = "STANDARD",
                RewardPoints = 1000
            };
            var cinema = new Cinema
            {
                CinemaId = "CIN_001",
                CinemaName = "Cinema 1",
                Address = "Address",
                City = "City",
                CinemaStatus = BookingConstants.ResourceStatus.Active
            };
            var room = new Room
            {
                RoomId = "ROM_001",
                CinemaId = cinema.CinemaId,
                RoomName = "Room 1",
                Capacity = 100,
                RoomStatus = BookingConstants.ResourceStatus.Active
            };
            var movie = new Movie
            {
                MovieId = "MOV_001",
                Title = "Movie",
                DurationMinutes = 120,
                AgeRating = "T13",
                MovieStatus = "NOW_SHOWING"
            };
            var showtime = new Showtime
            {
                ShowtimeId = "SHO_001",
                MovieId = movie.MovieId,
                RoomId = room.RoomId,
                StartTime = now.AddHours(2),
                EndTime = now.AddHours(4),
                BasePrice = 100000m,
                Status = BookingConstants.ShowtimeStatus.Open,
                CreatedAt = now
            };
            var seatType = new SeatType
            {
                SeatTypeId = "SET_VIP",
                TypeName = "VIP",
                ExtraFee = 20000m
            };
            var seat = new Seat
            {
                SeatId = "SEA_A1",
                RoomId = room.RoomId,
                SeatTypeId = seatType.SeatTypeId,
                SeatCode = "A1",
                RowLabel = "A",
                SeatNumber = 1,
                IsActive = true
            };
            var showtimeSeat = new ShowtimeSeat
            {
                ShowtimeSeatId = "STS_001",
                ShowtimeId = showtime.ShowtimeId,
                SeatId = seat.SeatId,
                SeatStatus = BookingConstants.ShowtimeSeatStatus.Locked,
                LockedByUserId = user.UserId,
                LockedUntil = now.AddMinutes(10),
                RowVersion = [1]
            };
            var foodItem = new FbItem
            {
                FbItemId = "FB_POPCORN",
                ItemName = "Popcorn",
                Price = 40000m,
                ItemStatus = BookingConstants.ResourceStatus.Available
            };
            var inventory = new CinemaFbInventory
            {
                CinemaInventoryId = "INV_001",
                CinemaId = cinema.CinemaId,
                FbItemId = foodItem.FbItemId,
                Quantity = 10
            };
            var voucher = new Voucher
            {
                VoucherId = "VOU_001",
                VoucherCode = "SAVE10",
                DiscountType = BookingConstants.DiscountType.Percent,
                DiscountValue = 10m,
                UsageLimit = 100,
                UsedCount = 0,
                StartDate = now.AddDays(-1),
                EndDate = now.AddDays(1),
                VoucherStatus = BookingConstants.VoucherStatus.Active,
                MinOrderAmount = 100000m,
                MaxDiscountAmount = 50000m,
                PerCustomerLimit = 1
            };

            dbContext.AddRange(
                role,
                user,
                customer,
                cinema,
                room,
                movie,
                showtime,
                seatType,
                seat,
                showtimeSeat,
                foodItem,
                inventory,
                voucher);
            await dbContext.SaveChangesAsync();
        }
    }

    private sealed class TestCinemaDbContext : CinemaDbContext
    {
        public TestCinemaDbContext(DbContextOptions<CinemaDbContext> options)
            : base(options)
        {
        }

        public bool ThrowConcurrencyOnSave { get; set; }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ThrowConcurrencyOnSave)
            {
                throw new DbUpdateConcurrencyException("Simulated concurrency conflict.");
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
    }
}
