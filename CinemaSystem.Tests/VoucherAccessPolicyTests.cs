using CinemaSystem.Contracts.Vouchers;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using CinemaSystem.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Tests;

public sealed class VoucherAccessPolicyTests
{
    [Fact]
    public async Task ValidateAndClaim_PrivateAllCustomersEvenWhenAssigned_AreRejected()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext();
        db.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerProfileId = "CUS_1",
            UserId = "USR_1",
            MemberLevel = "Standard"
        });
        var voucher = CreateVoucher(
            "VOU_PRIVATE_INVALID",
            "PRIVATE_INVALID",
            now,
            isPrivate: true,
            DomainConstants.VoucherTargetType.AllCustomers);
        db.Vouchers.Add(voucher);
        db.CustomerVouchers.Add(new CustomerVoucher
        {
            CustomerVoucherId = "CV_PRIVATE_INVALID",
            CustomerProfileId = "CUS_1",
            VoucherId = voucher.VoucherId,
            ClaimedAt = now,
            IsUsed = false
        });
        await db.SaveChangesAsync();

        var service = new VoucherService(db, new FakeClock(now));

        var validation = await service.ValidateVoucherForCustomerAsync(
            "PRIVATE_INVALID",
            100000,
            "USR_1",
            CancellationToken.None);
        var claim = await service.ClaimVoucherForCustomerAsync(
            "VOU_PRIVATE_INVALID",
            "USR_1",
            CancellationToken.None);

        Assert.True(validation.Success);
        Assert.False(validation.Data!.IsValid);
        Assert.Equal("VOUCHER_NOT_TARGETED", validation.Data.ErrorCode);
        Assert.False(claim.Success);
        Assert.Equal(403, claim.StatusCode);
        Assert.Equal("VOUCHER_NOT_TARGETED", claim.ErrorCode);
    }

    [Fact]
    public async Task ValidateAndClaim_TargetIdentifierPrefix_DoesNotGrantAccess()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext();
        db.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerProfileId = "CUS_1",
            UserId = "USR_1",
            MemberLevel = "Standard"
        });
        db.Vouchers.Add(CreateVoucher(
            "VOU_TARGETED",
            "TARGETED_ONLY",
            now,
            isPrivate: true,
            DomainConstants.VoucherTargetType.SpecificCustomers,
            "CUS_10"));
        await db.SaveChangesAsync();

        var service = new VoucherService(db, new FakeClock(now));

        var validation = await service.ValidateVoucherForCustomerAsync(
            "TARGETED_ONLY",
            100000,
            "USR_1",
            CancellationToken.None);
        var claim = await service.ClaimVoucherForCustomerAsync(
            "VOU_TARGETED",
            "USR_1",
            CancellationToken.None);

        Assert.False(validation.Data!.IsValid);
        Assert.Equal("VOUCHER_NOT_TARGETED", validation.Data.ErrorCode);
        Assert.False(claim.Success);
        Assert.Equal("VOUCHER_NOT_TARGETED", claim.ErrorCode);
    }

    [Fact]
    public async Task CreateVoucher_PrivateAllCustomers_IsRejected()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext();
        var service = new VoucherService(db, new FakeClock(now));

        var result = await service.CreateVoucherAsync(
            new CreateVoucherRequest
            {
                VoucherCode = "INVALID_PRIVATE",
                DiscountType = DomainConstants.DiscountType.Amount,
                DiscountValue = 50000,
                UsageLimit = 1,
                StartDate = now.AddDays(-1),
                EndDate = now.AddDays(1),
                IsPrivate = true,
                TargetType = DomainConstants.VoucherTargetType.AllCustomers
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("PRIVATE_VOUCHER_REQUIRES_TARGET", result.ErrorCode);
        Assert.Empty(db.Vouchers);
    }

    [Fact]
    public async Task CreateVoucher_SpecificCustomerWithoutValidCustomer_IsRejected()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext();
        var service = new VoucherService(db, new FakeClock(now));

        var result = await service.CreateVoucherAsync(
            new CreateVoucherRequest
            {
                VoucherCode = "MISSING_TARGET",
                DiscountType = DomainConstants.DiscountType.Amount,
                DiscountValue = 50000,
                UsageLimit = 1,
                StartDate = now.AddDays(-1),
                EndDate = now.AddDays(1),
                IsPrivate = true,
                TargetType = DomainConstants.VoucherTargetType.SpecificCustomers,
                TargetCustomerIds = "CUS_DOES_NOT_EXIST"
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("VOUCHER_TARGET_REQUIRED", result.ErrorCode);
        Assert.Empty(db.Vouchers);
    }

    [Fact]
    public async Task CreateVoucher_PublicCompensationVoucher_IsRejected()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext();
        var service = new VoucherService(db, new FakeClock(now));

        var result = await service.CreateVoucherAsync(
            new CreateVoucherRequest
            {
                VoucherCode = "PUBLIC_COMPENSATION",
                DiscountType = DomainConstants.DiscountType.Amount,
                DiscountValue = 50000,
                UsageLimit = 1,
                StartDate = now.AddDays(-1),
                EndDate = now.AddDays(1),
                Category = DomainConstants.VoucherCategory.Compensation,
                IsPrivate = false,
                TargetType = DomainConstants.VoucherTargetType.AllCustomers
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("COMPENSATION_VOUCHER_REQUIRES_CUSTOMER", result.ErrorCode);
        Assert.Empty(db.Vouchers);
    }

    [Fact]
    public async Task UpdateVoucher_PrivateAllCustomers_IsRejectedAndOriginalIsPreserved()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext();
        var voucher = CreateVoucher(
            "VOU_PUBLIC",
            "PUBLIC_ORIGINAL",
            now,
            isPrivate: false,
            DomainConstants.VoucherTargetType.AllCustomers);
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();
        var service = new VoucherService(db, new FakeClock(now));

        var result = await service.UpdateVoucherAsync(
            voucher.VoucherId,
            new UpdateVoucherRequest
            {
                Title = "Invalid update",
                VoucherStatus = DomainConstants.VoucherStatus.Active,
                UsageLimit = voucher.UsageLimit,
                StartDate = voucher.StartDate,
                EndDate = voucher.EndDate,
                IsPrivate = true,
                TargetType = DomainConstants.VoucherTargetType.AllCustomers
            },
            CancellationToken.None);

        Assert.False(result.Success);
        Assert.Equal("PRIVATE_VOUCHER_REQUIRES_TARGET", result.ErrorCode);
        Assert.False(voucher.IsPrivate);
        Assert.Null(voucher.Title);
    }

    [Fact]
    public async Task AssignedPrivateVoucher_CanBeUsedOnlyByAssignedCustomer()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext();
        db.CustomerProfiles.AddRange(
            new CustomerProfile
            {
                CustomerProfileId = "CUS_OWNER",
                UserId = "USR_OWNER",
                MemberLevel = "Standard"
            },
            new CustomerProfile
            {
                CustomerProfileId = "CUS_OTHER",
                UserId = "USR_OTHER",
                MemberLevel = "Standard"
            });
        var voucher = CreateVoucher(
            "VOU_ASSIGNED",
            "ASSIGNED_PRIVATE",
            now,
            isPrivate: true,
            DomainConstants.VoucherTargetType.SpecificCustomers,
            "CUS_OWNER");
        db.Vouchers.Add(voucher);
        db.CustomerVouchers.Add(new CustomerVoucher
        {
            CustomerVoucherId = "CV_OWNER",
            CustomerProfileId = "CUS_OWNER",
            VoucherId = voucher.VoucherId,
            ClaimedAt = now,
            IsUsed = false
        });
        await db.SaveChangesAsync();

        var service = new VoucherService(db, new FakeClock(now));
        var owner = await service.ValidateVoucherForCustomerAsync(
            voucher.VoucherCode,
            100000,
            "USR_OWNER",
            CancellationToken.None);
        var other = await service.ValidateVoucherForCustomerAsync(
            voucher.VoucherCode,
            100000,
            "USR_OTHER",
            CancellationToken.None);

        Assert.True(owner.Data!.IsValid);
        Assert.False(other.Data!.IsValid);
        Assert.Equal("VOUCHER_NOT_TARGETED", other.Data.ErrorCode);
    }

    [Fact]
    public async Task LegacyVoucherWithoutExplicitAudience_FailsClosed()
    {
        var now = new DateTime(2026, 7, 28, 12, 0, 0, DateTimeKind.Utc);
        await using var db = CreateDbContext();
        db.CustomerProfiles.Add(new CustomerProfile
        {
            CustomerProfileId = "CUS_1",
            UserId = "USR_1",
            MemberLevel = "Standard"
        });
        var voucher = CreateVoucher(
            "VOU_LEGACY",
            "LEGACY_UNCLASSIFIED",
            now,
            isPrivate: false,
            DomainConstants.VoucherTargetType.AllCustomers);
        voucher.TargetType = null;
        db.Vouchers.Add(voucher);
        await db.SaveChangesAsync();

        var service = new VoucherService(db, new FakeClock(now));

        var validation = await service.ValidateVoucherForCustomerAsync(
            voucher.VoucherCode,
            100000,
            "USR_1",
            CancellationToken.None);
        var claim = await service.ClaimVoucherForCustomerAsync(
            voucher.VoucherId,
            "USR_1",
            CancellationToken.None);

        Assert.False(validation.Data!.IsValid);
        Assert.Equal("VOUCHER_NOT_TARGETED", validation.Data.ErrorCode);
        Assert.False(claim.Success);
        Assert.Equal("VOUCHER_NOT_TARGETED", claim.ErrorCode);
    }

    private static Voucher CreateVoucher(
        string id,
        string code,
        DateTime now,
        bool isPrivate,
        string targetType,
        string? targetCustomerIds = null)
    {
        return new Voucher
        {
            VoucherId = id,
            VoucherCode = code,
            DiscountType = DomainConstants.DiscountType.Amount,
            DiscountValue = 50000,
            UsageLimit = 10,
            UsedCount = 0,
            StartDate = now.AddDays(-1),
            EndDate = now.AddDays(1),
            VoucherStatus = DomainConstants.VoucherStatus.Active,
            TargetType = targetType,
            TargetCustomerIds = targetCustomerIds,
            IsPrivate = isPrivate
        };
    }

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

        public DateTime UtcNow { get; }
    }
}
