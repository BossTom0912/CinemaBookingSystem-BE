using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Vouchers;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Services;

namespace CinemaSystem.Tests;

public sealed class VoucherServiceTests
{
    private static CinemaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new CinemaDbContext(options);
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
    }

    [Fact]
    public async Task CreateVoucherAsync_Success_ReturnsVoucherResponse()
    {
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var service = new VoucherService(db, clock);

        var request = new CreateVoucherRequest
        {
            VoucherCode = "SAVE50",
            Title = "Save 50k",
            Description = "Save 50k off tickets",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 50000m,
            MinOrderAmount = 100000m,
            UsageLimit = 100,
            PerCustomerLimit = 2,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(5)
        };

        var result = await service.CreateVoucherAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("SAVE50", result.Data.VoucherCode);
        Assert.Equal(50000m, result.Data.DiscountValue);

        var saved = await db.Vouchers.FirstOrDefaultAsync(v => v.VoucherCode == "SAVE50");
        Assert.NotNull(saved);
        Assert.Equal("SAVE50", saved.VoucherCode);
    }

    [Fact]
    public async Task CreateVoucherAsync_DuplicateCode_ReturnsConflict()
    {
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var service = new VoucherService(db, clock);

        db.Vouchers.Add(new Voucher
        {
            VoucherId = "VOU_1",
            VoucherCode = "SAVE50",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 50000m,
            UsageLimit = 100,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(5),
            VoucherStatus = DomainConstants.VoucherStatus.Active
        });
        await db.SaveChangesAsync();

        var request = new CreateVoucherRequest
        {
            VoucherCode = "save50", // test case insensitivity
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 50000m,
            UsageLimit = 100,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(5)
        };

        var result = await service.CreateVoucherAsync(request, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal(409, result.StatusCode);
        Assert.Equal("VOUCHER_CODE_EXISTS", result.ErrorCode);
    }

    [Fact]
    public async Task ValidateVoucherForCustomerAsync_Success_ReturnsValid()
    {
        var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var clock = new FakeClock(now);
        var service = new VoucherService(db, clock);

        // Seed Customer
        db.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerProfileId = "CUS_1",
            UserId = "USR_1",
            MemberLevel = "Standard"
        });

        // Seed Voucher
        db.Vouchers.Add(new Voucher
        {
            VoucherId = "VOU_1",
            VoucherCode = "SAVE10",
            DiscountType = DomainConstants.DiscountType.Percent,
            DiscountValue = 10m,
            UsageLimit = 10,
            PerCustomerLimit = 1,
            MinOrderAmount = 100000m,
            MaxDiscountAmount = 20000m,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            VoucherStatus = DomainConstants.VoucherStatus.Active
        });
        await db.SaveChangesAsync();

        var result = await service.ValidateVoucherForCustomerAsync("SAVE10", 150000m, "USR_1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.True(result.Data.IsValid);
        Assert.Equal(15000m, result.Data.DiscountAmount); // 10% of 150,000 is 15,000
    }

    [Fact]
    public async Task ValidateVoucherForCustomerAsync_MinOrderNotMet_ReturnsInvalid()
    {
        var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var clock = new FakeClock(now);
        var service = new VoucherService(db, clock);

        db.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerProfileId = "CUS_1",
            UserId = "USR_1",
            MemberLevel = "Standard"
        });

        db.Vouchers.Add(new Voucher
        {
            VoucherId = "VOU_1",
            VoucherCode = "SAVE50",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 50000m,
            MinOrderAmount = 100000m,
            UsageLimit = 10,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            VoucherStatus = DomainConstants.VoucherStatus.Active
        });
        await db.SaveChangesAsync();

        var result = await service.ValidateVoucherForCustomerAsync("SAVE50", 90000m, "USR_1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.IsValid);
        Assert.Equal("VOUCHER_MIN_ORDER_NOT_MET", result.Data.ErrorCode);
    }

    [Fact]
    public async Task ValidateVoucherForCustomerAsync_GlobalLimitReached_ReturnsInvalid()
    {
        var db = CreateDbContext();
        var now = DateTime.UtcNow;
        var clock = new FakeClock(now);
        var service = new VoucherService(db, clock);

        db.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerProfileId = "CUS_1",
            UserId = "USR_1",
            MemberLevel = "Standard"
        });

        db.Vouchers.Add(new Voucher
        {
            VoucherId = "VOU_1",
            VoucherCode = "SAVE50",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 50000m,
            UsageLimit = 2,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            VoucherStatus = DomainConstants.VoucherStatus.Active
        });

        // Seed 2 usages (APPLIED and CONFIRMED)
        db.VoucherUsages.AddRange(
            new VoucherUsage
            {
                VoucherUsageId = "VUS_1",
                VoucherId = "VOU_1",
                CustomerProfileId = "CUS_1",
                BookingId = "BOK_1",
                UsageStatus = DomainConstants.VoucherUsageStatus.Confirmed,
                DiscountAmount = 50000m
            },
            new VoucherUsage
            {
                VoucherUsageId = "VUS_2",
                VoucherId = "VOU_1",
                CustomerProfileId = "CUS_2",
                BookingId = "BOK_2",
                UsageStatus = DomainConstants.VoucherUsageStatus.Applied,
                DiscountAmount = 50000m
            }
        );
        await db.SaveChangesAsync();

        var result = await service.ValidateVoucherForCustomerAsync("SAVE50", 150000m, "USR_1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.IsValid);
        Assert.Equal("VOUCHER_USAGE_LIMIT_REACHED", result.Data.ErrorCode);
    }

    [Fact]
    public async Task UpdateVoucherAsync_Success_UpdatesVoucherDetails()
    {
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var service = new VoucherService(db, clock);

        var voucher = new Voucher
        {
            VoucherId = "VOU_1",
            VoucherCode = "SAVE50",
            Title = "Old Title",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 50000m,
            UsageLimit = 10,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(2),
            VoucherStatus = DomainConstants.VoucherStatus.Active
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();

        var updateRequest = new UpdateVoucherRequest
        {
            Title = "New Title",
            Description = "Updated Desc",
            ImageUrl = "new_img.png",
            VoucherStatus = DomainConstants.VoucherStatus.Inactive,
            MinOrderAmount = 200000m,
            UsageLimit = 20,
            StartDate = DateTime.UtcNow.AddDays(-2),
            EndDate = DateTime.UtcNow.AddDays(10)
        };

        var result = await service.UpdateVoucherAsync("VOU_1", updateRequest, CancellationToken.None);

        Assert.True(result.Success);
        Assert.NotNull(result.Data);
        Assert.Equal("New Title", result.Data.Title);
        Assert.Equal("INACTIVE", result.Data.VoucherStatus);

        var updated = await db.Vouchers.FindAsync("VOU_1");
        Assert.NotNull(updated);
        Assert.Equal("New Title", updated.Title);
        Assert.Equal(20, updated.UsageLimit);
    }

    [Fact]
    public async Task DeleteVoucherAsync_HasUsages_DeactivatesVoucher()
    {
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var service = new VoucherService(db, clock);

        var voucher = new Voucher
        {
            VoucherId = "VOU_1",
            VoucherCode = "SAVE50",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 50000m,
            UsageLimit = 10,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(2),
            VoucherStatus = DomainConstants.VoucherStatus.Active
        };
        db.Vouchers.Add(voucher);

        db.VoucherUsages.Add(new VoucherUsage
        {
            VoucherUsageId = "VUS_1",
            VoucherId = "VOU_1",
            CustomerProfileId = "CUS_1",
            BookingId = "BOK_1",
            UsageStatus = DomainConstants.VoucherUsageStatus.Confirmed
        });
        await db.SaveChangesAsync();

        var result = await service.DeleteVoucherAsync("VOU_1", CancellationToken.None);

        Assert.True(result.Success);
        
        var saved = await db.Vouchers.FindAsync("VOU_1");
        Assert.Null(saved); // Hard deleted
    }

    [Fact]
    public async Task GetActiveVouchersForCustomerAsync_ExcludesPrivateVouchers()
    {
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var service = new VoucherService(db, clock);

        var now = DateTime.UtcNow;
        db.Vouchers.AddRange(
            new Voucher
            {
                VoucherId = "VOU_PUBLIC",
                VoucherCode = "PUBLIC50",
                DiscountType = DomainConstants.DiscountType.Amount,
                DiscountValue = 50000m,
                UsageLimit = 10,
                UsedCount = 0,
                StartDate = now.AddDays(-1),
                EndDate = now.AddDays(2),
                VoucherStatus = DomainConstants.VoucherStatus.Active,
                IsPrivate = false
            },
            new Voucher
            {
                VoucherId = "VOU_PRIVATE",
                VoucherCode = "PRIVATE50",
                DiscountType = DomainConstants.DiscountType.Amount,
                DiscountValue = 50000m,
                UsageLimit = 10,
                UsedCount = 0,
                StartDate = now.AddDays(-1),
                EndDate = now.AddDays(2),
                VoucherStatus = DomainConstants.VoucherStatus.Active,
                IsPrivate = true
            }
        );
        await db.SaveChangesAsync();

        var result = await service.GetActiveVouchersForCustomerAsync(CancellationToken.None);

        Assert.True(result.Success);
        Assert.Single(result.Data);
        Assert.Equal("PUBLIC50", result.Data[0].VoucherCode);
    }

    [Fact]
    public async Task CheckAndAwardTicketMilestoneVouchersAsync_AwardsVoucherAndNotificationWhenConditionMet()
    {
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var service = new VoucherService(db, clock);

        var user = new User { UserId = "USR_1", Email = "test@example.com", FullName = "Test User", PasswordHash = "hash", RoleId = AuthConstants.RoleIds.Customer, Status = DomainConstants.EntityStatus.Active };
        var customer = new CustomerProfile { CustomerProfileId = "CUS_1", UserId = "USR_1", User = user, MemberLevel = "Standard" };
        db.Users.Add(user);
        db.CustomerProfiles.Add(customer);

        var booking = new Booking { BookingId = "BOK_1", CustomerProfileId = "CUS_1", BookingStatus = DomainConstants.EntityStatus.Paid, BookingChannel = "ONLINE" };
        db.Bookings.Add(booking);

        var seat = new BookingSeat { BookingSeatId = "BS_1", BookingId = "BOK_1", ShowtimeSeatId = "STS_1" };
        db.BookingSeats.Add(seat);

        db.Tickets.Add(new Ticket
        {
            TicketId = "TCK_1",
            BookingSeatId = "BS_1",
            BookingSeat = seat,
            QrCode = "QR_1",
            TicketStatus = DomainConstants.TicketStatus.Unused
        });

        var now = DateTime.UtcNow;
        var milestoneVoucher = new Voucher
        {
            VoucherId = "VOU_MILESTONE",
            VoucherCode = "TICKET1",
            Title = "Thưởng 1 Vé",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 30000m,
            UsageLimit = 100,
            UsedCount = 0,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(2),
            VoucherStatus = DomainConstants.VoucherStatus.Active,
            IsPrivate = true,
            RequiredTicketCount = 1
        };
        db.Vouchers.Add(milestoneVoucher);
        await db.SaveChangesAsync();

        var result = await service.CheckAndAwardTicketMilestoneVouchersAsync("CUS_1", CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data);

        var claimed = await db.CustomerVouchers.FirstOrDefaultAsync(cv => cv.CustomerProfileId == "CUS_1" && cv.VoucherId == "VOU_MILESTONE");
        Assert.NotNull(claimed);

        var notif = await db.Notifications.FirstOrDefaultAsync(n => n.UserId == "USR_1");
        Assert.NotNull(notif);
        Assert.Contains("Tặng Voucher", notif.Title);
    }

    [Fact]
    public async Task CreateVoucherAsync_SpecificCustomers_CreatesCustomerVoucherAndNotification()
    {
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var service = new VoucherService(db, clock);

        var user = new User
        {
            UserId = "USR_10",
            Email = "vip@example.com",
            FullName = "VIP Customer",
            PasswordHash = "hash",
            RoleId = AuthConstants.RoleIds.Customer,
            Status = DomainConstants.EntityStatus.Active
        };
        var customer = new CustomerProfile
        {
            CustomerProfileId = "CUS_10",
            UserId = "USR_10",
            User = user,
            MemberLevel = "VIP"
        };
        db.Users.Add(user);
        db.CustomerProfiles.Add(customer);
        await db.SaveChangesAsync();

        var request = new CreateVoucherRequest
        {
            VoucherCode = "VIPEXCLUSIVE",
            Title = "Voucher VIP 100k",
            Description = "Dành riêng cho khách hàng VIP",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 100000m,
            UsageLimit = 10,
            TargetType = "SPECIFIC_CUSTOMERS",
            TargetCustomerIds = "CUS_10",
            IsPrivate = true,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(10)
        };

        var result = await service.CreateVoucherAsync(request, CancellationToken.None);

        Assert.True(result.Success);

        var walletVoucher = await db.CustomerVouchers
            .FirstOrDefaultAsync(cv => cv.CustomerProfileId == "CUS_10" && cv.Voucher.VoucherCode == "VIPEXCLUSIVE");
        Assert.NotNull(walletVoucher);

        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.UserId == "USR_10");
        Assert.NotNull(notification);
        Assert.Contains("Tặng Voucher riêng", notification.Title);
        Assert.Contains("VIPEXCLUSIVE", notification.Message);
        Assert.Contains("100,000", notification.Message);
    }

    [Fact]
    public async Task IssueCompensationVoucherAsync_SendsNotificationToTargetedCustomers()
    {
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var service = new VoucherService(db, clock);

        var user = new User
        {
            UserId = "USR_11",
            Email = "comp@example.com",
            FullName = "Compensation Customer",
            PasswordHash = "hash",
            RoleId = AuthConstants.RoleIds.Customer,
            Status = DomainConstants.EntityStatus.Active
        };
        var customer = new CustomerProfile
        {
            CustomerProfileId = "CUS_11",
            UserId = "USR_11",
            User = user,
            MemberLevel = "Standard"
        };
        db.Users.Add(user);
        db.CustomerProfiles.Add(customer);

        var voucher = new Voucher
        {
            VoucherId = "VOU_COMP",
            VoucherCode = "COMP50K",
            Title = "Đền Bù Sự Cố",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 50000m,
            UsageLimit = 100,
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(5),
            VoucherStatus = DomainConstants.VoucherStatus.Active
        };
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();

        var request = new IssueCompensationRequest
        {
            VoucherId = "VOU_COMP",
            CustomerProfileIds = new List<string> { "CUS_11" }
        };

        var result = await service.IssueCompensationVoucherAsync(request, CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal(1, result.Data);

        var notification = await db.Notifications
            .FirstOrDefaultAsync(n => n.UserId == "USR_11");
        Assert.NotNull(notification);
        Assert.Contains("Voucher Đền Bù", notification.Title);
        Assert.Contains("COMP50K", notification.Message);
    }

    [Fact]
    public async Task CreateVoucherAsync_EventVoucher_NotifiesAllCustomers()
    {
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var service = new VoucherService(db, clock);

        var user1 = new User { UserId = "USR_20", Email = "cus1@example.com", FullName = "Customer 1", PasswordHash = "hash", RoleId = AuthConstants.RoleIds.Customer, Status = DomainConstants.EntityStatus.Active };
        var user2 = new User { UserId = "USR_21", Email = "cus2@example.com", FullName = "Customer 2", PasswordHash = "hash", RoleId = AuthConstants.RoleIds.Customer, Status = DomainConstants.EntityStatus.Active };
        db.Users.AddRange(user1, user2);
        await db.SaveChangesAsync();

        var request = new CreateVoucherRequest
        {
            VoucherCode = "SUMMER2026",
            Title = "Siêu Ưu Đãi Mùa Hè",
            Description = "Giảm 20% toàn bộ đơn hàng",
            DiscountType = DomainConstants.DiscountType.Percent,
            DiscountValue = 20m,
            UsageLimit = 500,
            Category = "EVENT",
            TargetType = "ALL_CUSTOMERS",
            StartDate = DateTime.UtcNow.AddDays(-1),
            EndDate = DateTime.UtcNow.AddDays(15)
        };

        var result = await service.CreateVoucherAsync(request, CancellationToken.None);

        Assert.True(result.Success);

        var notif1 = await db.Notifications.FirstOrDefaultAsync(n => n.UserId == "USR_20");
        var notif2 = await db.Notifications.FirstOrDefaultAsync(n => n.UserId == "USR_21");

        Assert.NotNull(notif1);
        Assert.NotNull(notif2);
        Assert.Contains("Sự Kiện Hot", notif1.Title);
        Assert.Contains("SUMMER2026", notif1.Message);
        Assert.Contains("20%", notif1.Message);
    }
}
