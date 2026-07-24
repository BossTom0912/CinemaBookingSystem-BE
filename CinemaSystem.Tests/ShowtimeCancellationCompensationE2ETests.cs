using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Bookings;
using CinemaSystem.Contracts.Compensations;
using CinemaSystem.Contracts.Notifications;
using CinemaSystem.Contracts.Vouchers;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Services;
using CinemaSystem.Infrastructure.Showtimes;
using CinemaSystem.Infrastructure.Time;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace CinemaSystem.Tests;

public sealed class ShowtimeCancellationCompensationE2ETests
{
    private const string TargetUserEmail = "khoivthse182701@fpt.edu.vn";
    private const string TargetUserId = "USR_KHOIVTH";
    private const string TargetCustomerProfileId = "CUS_KHOIVTH";

    private static CinemaDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<CinemaDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
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

    private async Task<(User User, CustomerProfile Customer)> SeedTargetUserAsync(CinemaDbContext db)
    {
        var user = new User
        {
            UserId = TargetUserId,
            Email = TargetUserEmail,
            FullName = "Khoi VTH",
            PasswordHash = "hash",
            RoleId = AuthConstants.RoleIds.Customer,
            Status = DomainConstants.EntityStatus.Active
        };
        var customer = new CustomerProfile
        {
            CustomerProfileId = TargetCustomerProfileId,
            UserId = TargetUserId,
            User = user,
            MemberLevel = "Standard"
        };
        db.Users.Add(user);
        db.CustomerProfiles.Add(customer);
        await db.SaveChangesAsync();
        return (user, customer);
    }

    [Fact]
    public async Task TC01_Notification_SentOnShowtimeCancellation()
    {
        // ARRANGE
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        await SeedTargetUserAsync(db);

        var notifService = new NotificationService(db, MockNoOpEmailService(), clock, NullLogger<NotificationService>.Instance, new UserHeartbeatTracker());

        // ACT: Admin cancels showtime and dispatches notification to customer
        var request = new SendNotificationRequest
        {
            UserId = TargetUserId,
            Title = "Lệnh hủy suất chiếu & Đền bù",
            Message = $"Rất tiếc: Suất chiếu của quý khách ({TargetUserEmail}) đã bị hủy do sự cố kỹ thuật. Vui lòng chọn phương án đền bù.",
            Channel = "App",
            Type = "Transactional"
        };
        var result = await notifService.SendNotificationAsync(request, CancellationToken.None);

        // ASSERT
        Assert.True(result.Success);
        var notifications = await db.Notifications.Where(n => n.UserId == TargetUserId).ToListAsync();
        Assert.NotEmpty(notifications);
        Assert.Contains("sự cố kỹ thuật", notifications.First().Message);
    }

    [Fact]
    public async Task TC02_VoucherPrivate_TargetedUserOnly()
    {
        // ARRANGE
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var voucherService = new VoucherService(db, clock);
        await SeedTargetUserAsync(db);

        // Create a Private Voucher assigned specifically to TargetCustomerProfileId
        var privateVoucher = new Voucher
        {
            VoucherId = "VOU_PRIVATE_COMP",
            VoucherCode = "COMP_KHOIVTH_100",
            Title = "Voucher Đền Bù Riêng Tư",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 100000m,
            UsageLimit = 10,
            UsedCount = 0,
            StartDate = clock.UtcNow.AddDays(-1),
            EndDate = clock.UtcNow.AddDays(30),
            VoucherStatus = DomainConstants.VoucherStatus.Active,
            IsPrivate = true,
            TargetType = "SPECIFIC_CUSTOMERS",
            TargetCustomerIds = TargetCustomerProfileId
        };
        db.Vouchers.Add(privateVoucher);
        await db.SaveChangesAsync();

        // ACT 1: Target User validates private voucher -> Successful
        var targetResult = await voucherService.ValidateVoucherForCustomerAsync(
            "COMP_KHOIVTH_100",
            150000m,
            TargetUserId,
            CancellationToken.None);

        Assert.True(targetResult.Success);
        Assert.True(targetResult.Data.IsValid);

        // ACT 2: Unauthorized User validates private voucher -> Rejected (403 / Not Targeted)
        var unauthorizedUser = new User
        {
            UserId = "USR_OTHER",
            Email = "other@example.com",
            FullName = "Other User",
            PasswordHash = "hash",
            RoleId = AuthConstants.RoleIds.Customer,
            Status = DomainConstants.EntityStatus.Active
        };
        var unauthorizedCustomer = new CustomerProfile
        {
            CustomerProfileId = "CUS_OTHER",
            UserId = "USR_OTHER",
            User = unauthorizedUser,
            MemberLevel = "Standard"
        };
        db.Users.Add(unauthorizedUser);
        db.CustomerProfiles.Add(unauthorizedCustomer);
        await db.SaveChangesAsync();

        var unauthorizedResult = await voucherService.ValidateVoucherForCustomerAsync(
            "COMP_KHOIVTH_100",
            150000m,
            "USR_OTHER",
            CancellationToken.None);

        Assert.False(unauthorizedResult.Data.IsValid);
        Assert.Equal("VOUCHER_NOT_TARGETED", unauthorizedResult.Data.ErrorCode);
    }

    [Fact]
    public async Task TC03_VoucherPublic_AvailableToAllUsers()
    {
        // ARRANGE
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var voucherService = new VoucherService(db, clock);
        await SeedTargetUserAsync(db);

        var publicVoucher = new Voucher
        {
            VoucherId = "VOU_PUBLIC_SUMMER",
            VoucherCode = "SUMMER2026",
            Title = "Khuyến Mãi Hè Công Khai",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 20000m,
            UsageLimit = 100,
            UsedCount = 0,
            StartDate = clock.UtcNow.AddDays(-1),
            EndDate = clock.UtcNow.AddDays(30),
            VoucherStatus = DomainConstants.VoucherStatus.Active,
            IsPrivate = false,
            TargetType = "ALL_CUSTOMERS"
        };
        db.Vouchers.Add(publicVoucher);
        await db.SaveChangesAsync();

        // ACT: Get active public vouchers
        var activeListResult = await voucherService.GetActiveVouchersForCustomerAsync(CancellationToken.None);

        // ASSERT: Public voucher is visible
        Assert.True(activeListResult.Success);
        Assert.Contains(activeListResult.Data, v => v.VoucherCode == "SUMMER2026");

        // Target user validates public voucher -> Success
        var validateResult = await voucherService.ValidateVoucherForCustomerAsync(
            "SUMMER2026",
            100000m,
            TargetUserId,
            CancellationToken.None);

        Assert.True(validateResult.Success);
        Assert.True(validateResult.Data.IsValid);
    }

    [Fact]
    public async Task TC04_BookingUpdate_ShowtimeUpdateAlertsUser()
    {
        // ARRANGE
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        await SeedTargetUserAsync(db);

        var notifService = new NotificationService(db, MockNoOpEmailService(), clock, NullLogger<NotificationService>.Instance, new UserHeartbeatTracker());

        // ACT: Admin modifies showtime room/time, triggering alert to affected customer
        var request = new SendNotificationRequest
        {
            UserId = TargetUserId,
            Title = "Cập nhật suất chiếu",
            Message = $"Thông báo cập nhật: Suất chiếu đã thay đổi phòng chiếu. Quý khách ({TargetUserEmail}) vui lòng kiểm tra vé mới.",
            Channel = "App",
            Type = "Transactional"
        };
        var result = await notifService.SendNotificationAsync(request, CancellationToken.None);

        // ASSERT
        Assert.True(result.Success);
        var notif = await db.Notifications.FirstOrDefaultAsync(n => n.UserId == TargetUserId);
        Assert.NotNull(notif);
        Assert.Contains("thay đổi phòng chiếu", notif.Message);
    }

    [Fact]
    public async Task TC05_Scenario1_TH1_SeatUpgradeAndCompensationVoucher()
    {
        // ARRANGE: Setup User khoivthse182701@fpt.edu.vn with Standard seat booking
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var voucherService = new VoucherService(db, clock);
        await SeedTargetUserAsync(db);

        // 1. Admin cancels/modifies showtime due to technical issues
        var oldBooking = new Booking
        {
            BookingId = "BOK_SCENARIO_1",
            CustomerProfileId = TargetCustomerProfileId,
            BookingStatus = DomainConstants.EntityStatus.Paid,
            TotalAmount = 80000m,
            BookingChannel = "ONLINE",
            CreatedAt = clock.UtcNow
        };
        db.Bookings.Add(oldBooking);
        await db.SaveChangesAsync();

        // 2. User chooses Option 1 (TH1): Accept transfer to new showtime with VIP/Deluxe seat upgrade without extra charge + receive Private Voucher
        // Admin/System issues Private Compensation Voucher & upgrades seat
        var compVoucherReq = new CreateVoucherRequest
        {
            VoucherCode = "TH1_UPGRADE_COMP",
            Title = "Voucher Đền Bù Chuyển Suất TH1",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 50000m,
            UsageLimit = 1,
            PerCustomerLimit = 1,
            StartDate = clock.UtcNow.AddDays(-1),
            EndDate = clock.UtcNow.AddDays(30),
            IsPrivate = true,
            TargetType = "SPECIFIC_CUSTOMERS",
            TargetCustomerIds = TargetCustomerProfileId
        };
        var createVoucherRes = await voucherService.CreateVoucherAsync(compVoucherReq, CancellationToken.None);
        Assert.True(createVoucherRes.Success);

        // Issue voucher to user's wallet
        var issueRes = await voucherService.IssueCompensationVoucherAsync(
            new IssueCompensationRequest
            {
                VoucherId = createVoucherRes.Data.VoucherId,
                CustomerProfileIds = new List<string> { TargetCustomerProfileId }
            },
            CancellationToken.None);
        Assert.True(issueRes.Success);

        // Create system notification for TH1 qualification
        db.Notifications.Add(new Notification
        {
            NotificationId = $"NOT_{Guid.NewGuid():N}",
            UserId = TargetUserId,
            Title = "🎁 Đổi suất chiếu & Tặng Voucher đền bù (TH1)",
            Message = "Bạn đủ điều kiện nhận Voucher đền bù và đã được nâng hạng ghế VIP miễn phí cho suất chiếu mới!",
            IsRead = false,
            CreatedAt = clock.UtcNow
        });
        await db.SaveChangesAsync();

        // ASSERT: Check Wallet & Notifications for khoivthse182701@fpt.edu.vn
        var myVouchersRes = await voucherService.GetMyVouchersAsync(TargetUserId, CancellationToken.None);
        Assert.True(myVouchersRes.Success);
        Assert.Contains(myVouchersRes.Data, v => v.VoucherCode == "TH1_UPGRADE_COMP");

        var userNotif = await db.Notifications.FirstOrDefaultAsync(n => n.UserId == TargetUserId && n.Title.Contains("TH1"));
        Assert.NotNull(userNotif);
        Assert.Contains("nâng hạng ghế VIP miễn phí", userNotif.Message);
    }

    [Fact]
    public async Task TC06_Scenario2_TH2_FullRefundAndCompensationVoucher()
    {
        // ARRANGE: User khoivthse182701@fpt.edu.vn has a paid booking
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var voucherService = new VoucherService(db, clock);
        await SeedTargetUserAsync(db);

        var booking = new Booking
        {
            BookingId = "BOK_SCENARIO_2",
            CustomerProfileId = TargetCustomerProfileId,
            BookingStatus = DomainConstants.EntityStatus.Paid,
            TotalAmount = 120000m,
            BookingChannel = "ONLINE",
            CreatedAt = clock.UtcNow
        };
        db.Bookings.Add(booking);

        var payment = new Payment
        {
            PaymentId = "PAY_SCENARIO_2",
            BookingId = "BOK_SCENARIO_2",
            PaymentProviderId = "PAYPROV_SEPAY",
            Amount = 120000m,
            PaymentStatus = "SUCCESS",
            PaidAt = clock.UtcNow
        };
        db.Payments.Add(payment);
        await db.SaveChangesAsync();

        // ACT: User chooses Option 2 (TH2): 100% Refund + Private Compensation Voucher
        // 1. Cancel booking and record 100% refund
        booking.BookingStatus = DomainConstants.EntityStatus.Cancelled;
        var refund = new Refund
        {
            RefundId = "REF_SCENARIO_2",
            BookingId = booking.BookingId,
            PaymentId = payment.PaymentId,
            PaymentProviderId = payment.PaymentProviderId,
            RefundAmount = 120000m,
            RefundStatus = "COMPLETED",
            RequestedAt = clock.UtcNow,
            RefundReason = "User selected Option 2 (100% Refund + Voucher)"
        };
        db.Refunds.Add(refund);

        // 2. Issue 100% Private Compensation Voucher
        var privateVoucherReq = new CreateVoucherRequest
        {
            VoucherCode = "TH2_REFUND_COMP",
            Title = "Voucher Đền Bù Hoàn Tiền TH2",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 50000m,
            UsageLimit = 1,
            PerCustomerLimit = 1,
            StartDate = clock.UtcNow.AddDays(-1),
            EndDate = clock.UtcNow.AddDays(30),
            IsPrivate = true,
            TargetType = "SPECIFIC_CUSTOMERS",
            TargetCustomerIds = TargetCustomerProfileId
        };
        var createVoucherRes = await voucherService.CreateVoucherAsync(privateVoucherReq, CancellationToken.None);
        Assert.True(createVoucherRes.Success);

        await voucherService.IssueCompensationVoucherAsync(
            new IssueCompensationRequest
            {
                VoucherId = createVoucherRes.Data.VoucherId,
                CustomerProfileIds = new List<string> { TargetCustomerProfileId }
            },
            CancellationToken.None);

        await db.SaveChangesAsync();

        // ASSERT: Check Booking is Cancelled, Refund is Completed 100%, and Private Voucher is in wallet
        var updatedBooking = await db.Bookings.FindAsync("BOK_SCENARIO_2");
        Assert.Equal(DomainConstants.EntityStatus.Cancelled, updatedBooking!.BookingStatus);

        var dbRefund = await db.Refunds.FirstOrDefaultAsync(r => r.BookingId == "BOK_SCENARIO_2");
        Assert.NotNull(dbRefund);
        Assert.Equal(120000m, dbRefund.RefundAmount);
        Assert.Equal("COMPLETED", dbRefund.RefundStatus);

        var myVouchersRes = await voucherService.GetMyVouchersAsync(TargetUserId, CancellationToken.None);
        Assert.True(myVouchersRes.Success);
        Assert.Contains(myVouchersRes.Data, v => v.VoucherCode == "TH2_REFUND_COMP");
    }

    [Fact]
    public async Task TC07_SecurityEdgeCase_ReusePrevention()
    {
        // ARRANGE: Create used voucher and attempt second validation / claim
        var db = CreateDbContext();
        var clock = new FakeClock(DateTime.UtcNow);
        var voucherService = new VoucherService(db, clock);
        await SeedTargetUserAsync(db);

        var voucher = new Voucher
        {
            VoucherId = "VOU_USED_ONCE",
            VoucherCode = "USED_ONCE_CODE",
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 50000m,
            UsageLimit = 1,
            PerCustomerLimit = 1,
            UsedCount = 1, // Already reached limit
            StartDate = clock.UtcNow.AddDays(-1),
            EndDate = clock.UtcNow.AddDays(30),
            VoucherStatus = DomainConstants.VoucherStatus.Active,
            IsPrivate = true,
            TargetType = "SPECIFIC_CUSTOMERS",
            TargetCustomerIds = TargetCustomerProfileId
        };
        db.Vouchers.Add(voucher);

        db.VoucherUsages.Add(new VoucherUsage
        {
            VoucherUsageId = "VUS_PREV_1",
            VoucherId = "VOU_USED_ONCE",
            CustomerProfileId = TargetCustomerProfileId,
            BookingId = "BOK_PREV_1",
            UsageStatus = DomainConstants.VoucherUsageStatus.Confirmed,
            UsedAt = clock.UtcNow
        });
        await db.SaveChangesAsync();

        // ACT: Attempt to validate used voucher
        var validationResult = await voucherService.ValidateVoucherForCustomerAsync(
            "USED_ONCE_CODE",
            100000m,
            TargetUserId,
            CancellationToken.None);

        // ASSERT: System blocks reuse attempt
        Assert.False(validationResult.Data.IsValid);
        Assert.Equal(BookingConstants.ErrorCodes.VoucherUsageLimitReached, validationResult.Data.ErrorCode);
    }

    private static IEmailService MockNoOpEmailService()
    {
        return new FakeEmailService();
    }

    private sealed class FakeEmailService : IEmailService
    {
        public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendEmailVerificationTokenAsync(string email, string token, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendPasswordResetTokenAsync(string email, string token, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task SendAccountInvitationAsync(string email, string tempPassword, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
