using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Vouchers;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;

namespace CinemaSystem.Infrastructure.Services;

public sealed class VoucherService : IVoucherService
{
    private readonly CinemaDbContext _dbContext;
    private readonly IClock _clock;
    private readonly IEmailService? _emailService;

    public VoucherService(CinemaDbContext dbContext, IClock clock, IEmailService? emailService = null)
    {
        _dbContext = dbContext;
        _clock = clock;
        _emailService = emailService;
    }

    public async Task<ServiceResult<VoucherResponse>> CreateVoucherAsync(
        CreateVoucherRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EndDate <= request.StartDate)
        {
            return ServiceResult<VoucherResponse>.Fail(400, "End date must be after start date.", "INVALID_DATE_RANGE");
        }

        var normalizedCode = request.VoucherCode.Trim().ToUpperInvariant();
        var exists = await _dbContext.Vouchers.AnyAsync(v => v.VoucherCode == normalizedCode, cancellationToken);
        if (exists)
        {
            return ServiceResult<VoucherResponse>.Fail(409, "Voucher code already exists.", "VOUCHER_CODE_EXISTS");
        }

        var voucherId = NewId(DomainConstants.EntityIdPrefix.Voucher);
        var voucher = new Voucher
        {
            VoucherId = voucherId,
            VoucherCode = normalizedCode,
            Title = request.Title,
            Description = request.Description,
            ImageUrl = request.ImageUrl,
            DiscountType = request.DiscountType,
            DiscountValue = request.DiscountValue,
            MinOrderAmount = request.MinOrderAmount,
            MaxDiscountAmount = request.MaxDiscountAmount,
            UsageLimit = request.UsageLimit,
            PerCustomerLimit = request.PerCustomerLimit,
            UsedCount = 0,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            VoucherStatus = DomainConstants.VoucherStatus.Active,
            Category = string.IsNullOrWhiteSpace(request.Category) ? "EVENT" : request.Category.Trim().ToUpperInvariant(),
            ApplicableScope = string.IsNullOrWhiteSpace(request.ApplicableScope) ? "TOTAL_ORDER" : request.ApplicableScope.Trim().ToUpperInvariant(),
            TargetType = string.IsNullOrWhiteSpace(request.TargetType) ? "ALL_CUSTOMERS" : request.TargetType.Trim().ToUpperInvariant(),
            TargetCustomerIds = request.TargetCustomerIds,
            SpecificFbItemIds = request.SpecificFbItemIds,
            IsPrivate = request.IsPrivate,
            RequiredTicketCount = request.RequiredTicketCount
        };

        _dbContext.Vouchers.Add(voucher);

        if (string.Equals(voucher.TargetType, "SPECIFIC_CUSTOMERS", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(voucher.TargetCustomerIds))
        {
            await AssignAndNotifyTargetCustomersAsync(voucher, voucher.TargetCustomerIds, cancellationToken);
        }
        else
        {
            await NotifyAllCustomersAboutEventVoucherAsync(voucher, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<VoucherResponse>.Ok(MapToResponse(voucher), "Voucher created successfully.");
    }

    public async Task<ServiceResult<IReadOnlyList<VoucherResponse>>> GetAllVouchersAsync(
        string? searchCode,
        string? status,
        CancellationToken cancellationToken)
    {
        var query = _dbContext.Vouchers.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(searchCode))
        {
            var cleanSearch = searchCode.Trim().ToUpperInvariant();
            query = query.Where(v => v.VoucherCode.Contains(cleanSearch));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var cleanStatus = status.Trim().ToUpperInvariant();
            query = query.Where(v => v.VoucherStatus == cleanStatus);
        }

        var vouchers = await query.ToListAsync(cancellationToken);
        var responseList = vouchers.Select(MapToResponse).ToList();

        return ServiceResult<IReadOnlyList<VoucherResponse>>.Ok(responseList, "Vouchers retrieved successfully.");
    }

    public async Task<ServiceResult<VoucherResponse>> GetVoucherByIdAsync(
        string voucherId,
        CancellationToken cancellationToken)
    {
        var voucher = await _dbContext.Vouchers.AsNoTracking()
            .FirstOrDefaultAsync(v => v.VoucherId == voucherId, cancellationToken);

        if (voucher == null)
        {
            return ServiceResult<VoucherResponse>.Fail(404, "Voucher not found.", "VOUCHER_NOT_FOUND");
        }

        return ServiceResult<VoucherResponse>.Ok(MapToResponse(voucher), "Voucher retrieved successfully.");
    }

    public async Task<ServiceResult<VoucherResponse>> UpdateVoucherAsync(
        string voucherId,
        UpdateVoucherRequest request,
        CancellationToken cancellationToken)
    {
        if (request.EndDate <= request.StartDate)
        {
            return ServiceResult<VoucherResponse>.Fail(400, "End date must be after start date.", "INVALID_DATE_RANGE");
        }

        var voucher = await _dbContext.Vouchers
            .FirstOrDefaultAsync(v => v.VoucherId == voucherId, cancellationToken);

        if (voucher == null)
        {
            return ServiceResult<VoucherResponse>.Fail(404, "Voucher not found.", "VOUCHER_NOT_FOUND");
        }

        if (request.UsageLimit < voucher.UsedCount)
        {
            return ServiceResult<VoucherResponse>.Fail(400, $"Usage limit cannot be less than current used count ({voucher.UsedCount}).", "INVALID_USAGE_LIMIT");
        }

        voucher.Title = request.Title;
        voucher.Description = request.Description;
        voucher.ImageUrl = request.ImageUrl;
        voucher.VoucherStatus = request.VoucherStatus;
        voucher.MinOrderAmount = request.MinOrderAmount;
        voucher.MaxDiscountAmount = request.MaxDiscountAmount;
        voucher.UsageLimit = request.UsageLimit;
        voucher.PerCustomerLimit = request.PerCustomerLimit;
        voucher.StartDate = request.StartDate;
        voucher.EndDate = request.EndDate;
        if (!string.IsNullOrWhiteSpace(request.Category)) voucher.Category = request.Category.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(request.ApplicableScope)) voucher.ApplicableScope = request.ApplicableScope.Trim().ToUpperInvariant();
        if (!string.IsNullOrWhiteSpace(request.TargetType)) voucher.TargetType = request.TargetType.Trim().ToUpperInvariant();
        if (request.TargetCustomerIds != null) voucher.TargetCustomerIds = request.TargetCustomerIds;
        if (request.SpecificFbItemIds != null) voucher.SpecificFbItemIds = request.SpecificFbItemIds;
        voucher.IsPrivate = request.IsPrivate;
        voucher.RequiredTicketCount = request.RequiredTicketCount;

        if (string.Equals(voucher.TargetType, "SPECIFIC_CUSTOMERS", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(voucher.TargetCustomerIds))
        {
            await AssignAndNotifyTargetCustomersAsync(voucher, voucher.TargetCustomerIds, cancellationToken);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<VoucherResponse>.Ok(MapToResponse(voucher), "Voucher updated successfully.");
    }

    public async Task<ServiceResult<bool>> DeleteVoucherAsync(
        string voucherId,
        CancellationToken cancellationToken)
    {
        var voucher = await _dbContext.Vouchers
            .FirstOrDefaultAsync(v => v.VoucherId == voucherId, cancellationToken);

        if (voucher == null)
        {
            return ServiceResult<bool>.Fail(404, "Voucher not found.", "VOUCHER_NOT_FOUND");
        }

        // Hard delete: Remove linked CustomerVoucher and VoucherUsage records first
        var customerVouchers = await _dbContext.CustomerVouchers
            .Where(cv => cv.VoucherId == voucherId)
            .ToListAsync(cancellationToken);
        if (customerVouchers.Count > 0)
        {
            _dbContext.CustomerVouchers.RemoveRange(customerVouchers);
        }

        var usages = await _dbContext.VoucherUsages
            .Where(vu => vu.VoucherId == voucherId)
            .ToListAsync(cancellationToken);
        if (usages.Count > 0)
        {
            _dbContext.VoucherUsages.RemoveRange(usages);
        }

        _dbContext.Vouchers.Remove(voucher);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, "Voucher deleted successfully.");
    }

    public async Task<ServiceResult<ValidateVoucherResponse>> ValidateVoucherForCustomerAsync(
        string voucherCode,
        decimal bookingAmount,
        string userId,
        CancellationToken cancellationToken)
    {
        var customerProfile = await _dbContext.CustomerProfiles
            .FirstOrDefaultAsync(cp => cp.UserId == userId, cancellationToken);

        if (customerProfile == null)
        {
            return ServiceResult<ValidateVoucherResponse>.Fail(403, "Only customers can validate vouchers.", "CUSTOMER_PROFILE_NOT_FOUND");
        }

        var validationResult = await ValidateAndGetVoucherAsync(
            voucherCode,
            bookingAmount,
            customerProfile.CustomerProfileId,
            cancellationToken);

        if (!validationResult.Success)
        {
            return ServiceResult<ValidateVoucherResponse>.Ok(new ValidateVoucherResponse
            {
                IsValid = false,
                DiscountAmount = 0,
                Message = validationResult.Message,
                ErrorCode = validationResult.ErrorCode
            }, "Validation completed.");
        }

        return ServiceResult<ValidateVoucherResponse>.Ok(new ValidateVoucherResponse
        {
            IsValid = true,
            DiscountAmount = validationResult.Data.DiscountAmount,
            Message = "Voucher is valid."
        }, "Voucher validated successfully.");
    }

    public async Task<ServiceResult<VoucherValidationResult>> ValidateAndGetVoucherAsync(
        string voucherCode,
        decimal bookingAmount,
        string customerProfileId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(voucherCode))
        {
            return ServiceResult<VoucherValidationResult>.Fail(400, "Voucher code cannot be empty.", BookingConstants.ErrorCodes.VoucherNotFound);
        }

        var normalizedCode = voucherCode.Trim().ToUpperInvariant();
        var voucher = await _dbContext.Vouchers
            .FirstOrDefaultAsync(v => v.VoucherCode == normalizedCode, cancellationToken);

        if (voucher == null)
        {
            return ServiceResult<VoucherValidationResult>.Fail(400, "Voucher code not found.", BookingConstants.ErrorCodes.VoucherNotFound);
        }

        // 1. Check Status
        if (!string.Equals(voucher.VoucherStatus, DomainConstants.VoucherStatus.Active, StringComparison.OrdinalIgnoreCase))
        {
            return ServiceResult<VoucherValidationResult>.Fail(400, "Voucher is not active.", BookingConstants.ErrorCodes.VoucherExpired);
        }

        var now = _clock.UtcNow;

        // 2. Check Dates
        if (now < voucher.StartDate || now > voucher.EndDate)
        {
            return ServiceResult<VoucherValidationResult>.Fail(400, "Voucher is out of active date range.", BookingConstants.ErrorCodes.VoucherExpired);
        }

        // 3. Check Minimum Order Amount
        if (voucher.MinOrderAmount.HasValue && bookingAmount < voucher.MinOrderAmount.Value)
        {
            return ServiceResult<VoucherValidationResult>.Fail(400, $"Minimum order amount of {voucher.MinOrderAmount.Value} VND is not met.", BookingConstants.ErrorCodes.VoucherMinOrderNotMet);
        }

        // 4. Check Global Usage Limit (count active usages: APPLIED & CONFIRMED)
        var activeUsagesCount = await _dbContext.VoucherUsages
            .CountAsync(vu => vu.VoucherId == voucher.VoucherId 
                && vu.UsageStatus != DomainConstants.VoucherUsageStatus.Cancelled, cancellationToken);

        if (activeUsagesCount >= voucher.UsageLimit)
        {
            return ServiceResult<VoucherValidationResult>.Fail(400, "Voucher global usage limit has been reached.", BookingConstants.ErrorCodes.VoucherUsageLimitReached);
        }

        // 5. Check Customer Limit & Target Type
        if (!string.IsNullOrEmpty(customerProfileId))
        {
            if (string.Equals(voucher.TargetType, "SPECIFIC_CUSTOMERS", StringComparison.OrdinalIgnoreCase))
            {
                var isClaimed = await _dbContext.CustomerVouchers
                    .AnyAsync(cv => cv.VoucherId == voucher.VoucherId && cv.CustomerProfileId == customerProfileId, cancellationToken);
                var isExplicitlyTargeted = !string.IsNullOrEmpty(voucher.TargetCustomerIds) && voucher.TargetCustomerIds.Contains(customerProfileId, StringComparison.OrdinalIgnoreCase);

                if (!isClaimed && !isExplicitlyTargeted)
                {
                    return ServiceResult<VoucherValidationResult>.Fail(403, "Voucher này không dành cho tài khoản của bạn.", "VOUCHER_NOT_TARGETED");
                }
            }

            var customerUsagesCount = await _dbContext.VoucherUsages
                .CountAsync(vu => vu.VoucherId == voucher.VoucherId 
                    && vu.CustomerProfileId == customerProfileId 
                    && vu.UsageStatus != DomainConstants.VoucherUsageStatus.Cancelled, cancellationToken);

            if (voucher.PerCustomerLimit.HasValue && customerUsagesCount >= voucher.PerCustomerLimit.Value)
            {
                return ServiceResult<VoucherValidationResult>.Fail(400, "You have reached your limit for this voucher.", BookingConstants.ErrorCodes.VoucherCustomerLimitReached);
            }
        }

        // Calculate discount
        decimal discount = 0;
        if (string.Equals(voucher.DiscountType, DomainConstants.DiscountType.Amount, StringComparison.OrdinalIgnoreCase))
        {
            discount = voucher.DiscountValue;
        }
        else if (string.Equals(voucher.DiscountType, DomainConstants.DiscountType.Percent, StringComparison.OrdinalIgnoreCase))
        {
            discount = bookingAmount * (voucher.DiscountValue / 100m);
            if (voucher.MaxDiscountAmount.HasValue && discount > voucher.MaxDiscountAmount.Value)
            {
                discount = voucher.MaxDiscountAmount.Value;
            }
        }

        discount = Math.Min(discount, bookingAmount);

        return ServiceResult<VoucherValidationResult>.Ok(new VoucherValidationResult(voucher, discount), "Voucher is valid.");
    }

    public async Task<ServiceResult<IReadOnlyList<VoucherResponse>>> GetActiveVouchersForCustomerAsync(
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var activeVouchers = await _dbContext.Vouchers.AsNoTracking()
            .Where(v => v.VoucherStatus == DomainConstants.VoucherStatus.Active
                && !v.IsPrivate
                && v.StartDate <= now
                && v.EndDate >= now
                && v.UsedCount < v.UsageLimit)
            .ToListAsync(cancellationToken);

        var responseList = activeVouchers.Select(MapToResponse).ToList();
        return ServiceResult<IReadOnlyList<VoucherResponse>>.Ok(responseList, "Active vouchers retrieved successfully.");
    }

    private static string NewId(string prefix) => CinemaSystem.Domain.Utilities.IdGenerator.NewId(prefix);

    public async Task<ServiceResult<bool>> ClaimVoucherForCustomerAsync(
        string voucherId,
        string userId,
        CancellationToken cancellationToken)
    {
        var customerProfile = await _dbContext.CustomerProfiles
            .FirstOrDefaultAsync(cp => cp.UserId == userId, cancellationToken);

        if (customerProfile == null)
        {
            return ServiceResult<bool>.Fail(403, "Only customers can claim vouchers.", "CUSTOMER_PROFILE_NOT_FOUND");
        }

        var voucher = await _dbContext.Vouchers
            .FirstOrDefaultAsync(v => v.VoucherId == voucherId, cancellationToken);

        if (voucher == null)
        {
            return ServiceResult<bool>.Fail(404, "Voucher not found.", "VOUCHER_NOT_FOUND");
        }

        var now = _clock.UtcNow;

        if (!string.Equals(voucher.VoucherStatus, DomainConstants.VoucherStatus.Active, StringComparison.OrdinalIgnoreCase)
            || now < voucher.StartDate || now > voucher.EndDate)
        {
            return ServiceResult<bool>.Fail(400, "Voucher is not active or has expired.", "VOUCHER_UNAVAILABLE");
        }

        if (voucher.UsedCount >= voucher.UsageLimit)
        {
            return ServiceResult<bool>.Fail(400, "Voucher global usage limit has been reached.", "VOUCHER_USAGE_LIMIT_REACHED");
        }

        var claimCount = await _dbContext.CustomerVouchers
            .CountAsync(cv => cv.VoucherId == voucherId && cv.CustomerProfileId == customerProfile.CustomerProfileId, cancellationToken);

        if (voucher.PerCustomerLimit.HasValue && claimCount >= voucher.PerCustomerLimit.Value)
        {
            return ServiceResult<bool>.Fail(400, "You have reached your claim limit for this voucher.", "VOUCHER_CUSTOMER_LIMIT_REACHED");
        }

        var customerVoucher = new CustomerVoucher
        {
            CustomerVoucherId = $"CV_{Guid.NewGuid():N}",
            CustomerProfileId = customerProfile.CustomerProfileId,
            VoucherId = voucher.VoucherId,
            ClaimedAt = now,
            IsUsed = false,
            UsedAt = null
        };

        _dbContext.CustomerVouchers.Add(customerVoucher);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true, "Voucher claimed successfully.");
    }

    public async Task<ServiceResult<IReadOnlyList<VoucherResponse>>> GetMyVouchersAsync(
        string userId,
        CancellationToken cancellationToken)
    {
        var customerProfile = await _dbContext.CustomerProfiles
            .FirstOrDefaultAsync(cp => cp.UserId == userId, cancellationToken);

        if (customerProfile == null)
        {
            return ServiceResult<IReadOnlyList<VoucherResponse>>.Fail(403, "Only customers have voucher wallets.", "CUSTOMER_PROFILE_NOT_FOUND");
        }

        // Auto check & award any eligible ticket milestone vouchers
        await CheckAndAwardTicketMilestoneVouchersAsync(customerProfile.CustomerProfileId, cancellationToken);

        var claimedVouchers = await _dbContext.CustomerVouchers
            .Include(cv => cv.Voucher)
            .Where(cv => cv.CustomerProfileId == customerProfile.CustomerProfileId
                && !cv.IsUsed
                && !cv.VoucherUsages.Any(usage =>
                    usage.UsageStatus != DomainConstants.VoucherUsageStatus.Cancelled))
            .Select(cv => cv.Voucher)
            .ToListAsync(cancellationToken);

        var responseList = claimedVouchers.Select(MapToResponse).ToList();
        return ServiceResult<IReadOnlyList<VoucherResponse>>.Ok(responseList, "Claimed vouchers retrieved successfully.");
    }

    public async Task<ServiceResult<int>> CheckAndAwardTicketMilestoneVouchersAsync(
        string customerProfileId,
        CancellationToken cancellationToken)
    {
        var customerProfile = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(cp => cp.CustomerProfileId == customerProfileId, cancellationToken);

        if (customerProfile == null)
        {
            return ServiceResult<int>.Ok(0, "Customer profile not found.");
        }

        // Total tickets booked by this customer across valid bookings
        var totalTicketsCount = await _dbContext.Tickets
            .AsNoTracking()
            .CountAsync(t => t.BookingSeat.Booking.CustomerProfileId == customerProfileId
                && (t.BookingSeat.Booking.BookingStatus == DomainConstants.EntityStatus.Paid
                    || t.BookingSeat.Booking.BookingStatus == DomainConstants.EntityStatus.Completed)
                && (t.TicketStatus == DomainConstants.TicketStatus.Unused
                    || t.TicketStatus == DomainConstants.TicketStatus.CheckedIn
                    || t.TicketStatus == DomainConstants.TicketStatus.Generated), cancellationToken);

        var now = _clock.UtcNow;

        // Find active milestone vouchers requiring ticket count <= user's total tickets
        var milestoneVouchers = await _dbContext.Vouchers
            .Where(v => v.VoucherStatus == DomainConstants.VoucherStatus.Active
                && v.RequiredTicketCount.HasValue
                && v.RequiredTicketCount.Value > 0
                && v.RequiredTicketCount.Value <= totalTicketsCount
                && v.StartDate <= now
                && v.EndDate >= now
                && v.UsedCount < v.UsageLimit)
            .ToListAsync(cancellationToken);

        int awardedCount = 0;
        foreach (var voucher in milestoneVouchers)
        {
            var alreadyClaimed = await _dbContext.CustomerVouchers
                .AnyAsync(cv => cv.VoucherId == voucher.VoucherId && cv.CustomerProfileId == customerProfileId, cancellationToken);

            if (!alreadyClaimed)
            {
                _dbContext.CustomerVouchers.Add(new CustomerVoucher
                {
                    CustomerVoucherId = $"CV_{Guid.NewGuid():N}",
                    CustomerProfileId = customerProfileId,
                    VoucherId = voucher.VoucherId,
                    ClaimedAt = now,
                    IsUsed = false
                });

                var notifId = CinemaSystem.Domain.Utilities.IdGenerator.NewId(DomainConstants.EntityIdPrefix.Notification);
                _dbContext.Notifications.Add(new Notification
                {
                    NotificationId = notifId,
                    UserId = customerProfile.UserId,
                    Title = "🎁 Tặng Voucher tích lũy vé!",
                    Message = $"Chúc mừng! Bạn đã đạt mốc đặt {voucher.RequiredTicketCount} vé và được tặng voucher: {voucher.Title ?? voucher.VoucherCode}!",
                    IsRead = false,
                    CreatedAt = now
                });

                awardedCount++;
            }
        }

        if (awardedCount > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<int>.Ok(awardedCount, $"Awarded {awardedCount} milestone vouchers.");
    }

    public async Task<ServiceResult<int>> IssueCompensationVoucherAsync(
        IssueCompensationRequest request,
        CancellationToken cancellationToken)
    {
        var voucher = await _dbContext.Vouchers.FirstOrDefaultAsync(v => v.VoucherId == request.VoucherId, cancellationToken);
        if (voucher == null)
        {
            return ServiceResult<int>.Fail(404, "Voucher not found.", "VOUCHER_NOT_FOUND");
        }

        var now = _clock.UtcNow;

        string discountText;
        if (string.Equals(voucher.DiscountType, DomainConstants.DiscountType.Percent, StringComparison.OrdinalIgnoreCase))
        {
            discountText = $"Giảm {voucher.DiscountValue}%";
            if (voucher.MaxDiscountAmount.HasValue && voucher.MaxDiscountAmount.Value > 0)
            {
                discountText += $" (tối đa {voucher.MaxDiscountAmount.Value:#,##0} VNĐ)";
            }
        }
        else
        {
            discountText = $"Giảm {voucher.DiscountValue:#,##0} VNĐ";
        }

        var dateRangeText = $"từ {voucher.StartDate:dd/MM/yyyy} đến {voucher.EndDate:dd/MM/yyyy}";
        var voucherTitle = !string.IsNullOrWhiteSpace(voucher.Title) ? voucher.Title : voucher.VoucherCode;
        var notifTitle = "🎁 [Voucher Đền Bù] Bạn vừa nhận được voucher đền bù từ G2Cinema!";
        var notifMessage = $"Bạn vừa được nhận voucher đền bù '{voucherTitle}': Mã [{voucher.VoucherCode}] - {discountText}. Hạn dùng {dateRangeText}. {(string.IsNullOrWhiteSpace(voucher.Description) ? "" : voucher.Description)}";

        int count = 0;
        foreach (var customerId in request.CustomerProfileIds.Distinct())
        {
            var profile = await _dbContext.CustomerProfiles
                .Include(cp => cp.User)
                .FirstOrDefaultAsync(cp => cp.CustomerProfileId == customerId || cp.UserId == customerId, cancellationToken);

            if (profile == null) continue;

            var exists = await _dbContext.CustomerVouchers
                .AnyAsync(cv => cv.VoucherId == voucher.VoucherId && cv.CustomerProfileId == profile.CustomerProfileId, cancellationToken);
            if (!exists)
            {
                _dbContext.CustomerVouchers.Add(new CustomerVoucher
                {
                    CustomerVoucherId = $"CV_{Guid.NewGuid():N}",
                    CustomerProfileId = profile.CustomerProfileId,
                    VoucherId = voucher.VoucherId,
                    ClaimedAt = now,
                    IsUsed = false
                });
                count++;
            }

            var notifId = CinemaSystem.Domain.Utilities.IdGenerator.NewId(DomainConstants.EntityIdPrefix.Notification);
            _dbContext.Notifications.Add(new Notification
            {
                NotificationId = notifId,
                UserId = profile.UserId,
                Title = notifTitle,
                Message = notifMessage,
                IsRead = false,
                CreatedAt = now
            });

            if (_emailService != null && profile.User != null && !string.IsNullOrWhiteSpace(profile.User.Email))
            {
                try
                {
                    await _emailService.SendEmailAsync(
                        profile.User.Email,
                        notifTitle,
                        notifMessage,
                        cancellationToken);
                }
                catch
                {
                    // Ignore email sending error
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ServiceResult<int>.Ok(count, $"Successfully issued compensation voucher to {count} customers.");
    }

    private async Task AssignAndNotifyTargetCustomersAsync(
        Voucher voucher,
        string? targetCustomerIds,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetCustomerIds))
        {
            return;
        }

        var rawIds = targetCustomerIds
            .Split(new[] { ',', ';', ' ', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(id => id.Trim())
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (rawIds.Count == 0)
        {
            return;
        }

        var customerProfiles = await _dbContext.CustomerProfiles
            .Include(cp => cp.User)
            .Where(cp => rawIds.Contains(cp.CustomerProfileId)
                || rawIds.Contains(cp.UserId)
                || (cp.User != null && rawIds.Contains(cp.User.Email)))
            .ToListAsync(cancellationToken);

        if (customerProfiles.Count == 0)
        {
            return;
        }

        var now = _clock.UtcNow;

        string discountText;
        if (string.Equals(voucher.DiscountType, DomainConstants.DiscountType.Percent, StringComparison.OrdinalIgnoreCase))
        {
            discountText = $"Giảm {voucher.DiscountValue}%";
            if (voucher.MaxDiscountAmount.HasValue && voucher.MaxDiscountAmount.Value > 0)
            {
                discountText += $" (tối đa {voucher.MaxDiscountAmount.Value:#,##0} VNĐ)";
            }
        }
        else
        {
            discountText = $"Giảm {voucher.DiscountValue:#,##0} VNĐ";
        }

        if (voucher.MinOrderAmount.HasValue && voucher.MinOrderAmount.Value > 0)
        {
            discountText += $" cho đơn từ {voucher.MinOrderAmount.Value:#,##0} VNĐ";
        }

        var dateRangeText = $"từ {voucher.StartDate:dd/MM/yyyy} đến {voucher.EndDate:dd/MM/yyyy}";
        var voucherTitle = !string.IsNullOrWhiteSpace(voucher.Title) ? voucher.Title : voucher.VoucherCode;
        var notifTitle = "🎁 Tặng Voucher riêng từ G2Cinema!";
        var notifMessage = $"Chúc mừng! Bạn vừa được tặng voucher riêng '{voucherTitle}': Mã [{voucher.VoucherCode}] - {discountText}. Thời hạn áp dụng {dateRangeText}. {(string.IsNullOrWhiteSpace(voucher.Description) ? "" : voucher.Description)}";

        foreach (var profile in customerProfiles)
        {
            var hasCustomerVoucher = await _dbContext.CustomerVouchers
                .AnyAsync(cv => cv.VoucherId == voucher.VoucherId && cv.CustomerProfileId == profile.CustomerProfileId, cancellationToken);

            if (!hasCustomerVoucher)
            {
                _dbContext.CustomerVouchers.Add(new CustomerVoucher
                {
                    CustomerVoucherId = $"CV_{Guid.NewGuid():N}",
                    CustomerProfileId = profile.CustomerProfileId,
                    VoucherId = voucher.VoucherId,
                    ClaimedAt = now,
                    IsUsed = false
                });
            }

            var notifId = CinemaSystem.Domain.Utilities.IdGenerator.NewId(DomainConstants.EntityIdPrefix.Notification);
            _dbContext.Notifications.Add(new Notification
            {
                NotificationId = notifId,
                UserId = profile.UserId,
                Title = notifTitle,
                Message = notifMessage,
                IsRead = false,
                CreatedAt = now
            });

            if (_emailService != null && profile.User != null && !string.IsNullOrWhiteSpace(profile.User.Email))
            {
                try
                {
                    await _emailService.SendEmailAsync(
                        profile.User.Email,
                        notifTitle,
                        notifMessage,
                        cancellationToken);
                }
                catch
                {
                    // Ignore email failure
                }
            }
        }
    }

    private async Task NotifyAllCustomersAboutEventVoucherAsync(
        Voucher voucher,
        CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;

        var customerUserIds = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.RoleId == AuthConstants.RoleIds.Customer
                && u.Status == DomainConstants.EntityStatus.Active)
            .Select(u => u.UserId)
            .ToListAsync(cancellationToken);

        if (customerUserIds.Count == 0)
        {
            return;
        }

        string discountText;
        if (string.Equals(voucher.DiscountType, DomainConstants.DiscountType.Percent, StringComparison.OrdinalIgnoreCase))
        {
            discountText = $"Giảm {voucher.DiscountValue}%";
            if (voucher.MaxDiscountAmount.HasValue && voucher.MaxDiscountAmount.Value > 0)
            {
                discountText += $" (tối đa {voucher.MaxDiscountAmount.Value:#,##0} VNĐ)";
            }
        }
        else
        {
            discountText = $"Giảm {voucher.DiscountValue:#,##0} VNĐ";
        }

        if (voucher.MinOrderAmount.HasValue && voucher.MinOrderAmount.Value > 0)
        {
            discountText += $" cho đơn từ {voucher.MinOrderAmount.Value:#,##0} VNĐ";
        }

        var dateRangeText = $"từ {voucher.StartDate:dd/MM/yyyy} đến {voucher.EndDate:dd/MM/yyyy}";
        var voucherTitle = !string.IsNullOrWhiteSpace(voucher.Title) ? voucher.Title : voucher.VoucherCode;
        var notifTitle = "🎉 [Sự Kiện Hot] Voucher Ưu Đãi Mới!";
        var notifMessage = $"G2Cinema vừa phát hành voucher sự kiện '{voucherTitle}': Nhập mã [{voucher.VoucherCode}] - {discountText}. Áp dụng {dateRangeText}. {(string.IsNullOrWhiteSpace(voucher.Description) ? "" : voucher.Description)}";

        var notifications = customerUserIds.Select(userId => new Notification
        {
            NotificationId = CinemaSystem.Domain.Utilities.IdGenerator.NewId(DomainConstants.EntityIdPrefix.Notification),
            UserId = userId,
            Title = notifTitle,
            Message = notifMessage,
            IsRead = false,
            CreatedAt = now
        }).ToList();

        _dbContext.Notifications.AddRange(notifications);
    }

    public async Task<ServiceResult<IReadOnlyList<string>>> GetCustomerIdsByShowtimeOrRoomAsync(
        string? showtimeId,
        string? roomId,
        CancellationToken cancellationToken)
    {
        showtimeId = showtimeId?.Trim();
        roomId = roomId?.Trim();

        if (string.IsNullOrWhiteSpace(showtimeId) && string.IsNullOrWhiteSpace(roomId))
        {
            return ServiceResult<IReadOnlyList<string>>.Fail(
                400,
                "Vui lòng nhập Mã suất chiếu (Showtime ID) hoặc Mã phòng chiếu (Room ID).",
                "INVALID_INPUT");
        }

        var query = _dbContext.Bookings.AsNoTracking().AsQueryable();

        // Lọc các đơn đặt vé hợp lệ (chưa hủy/hoàn)
        query = query.Where(b => b.BookingStatus != "Cancelled" && b.BookingStatus != "REFUNDED" && b.BookingStatus != "FAILED");

        if (!string.IsNullOrWhiteSpace(showtimeId))
        {
            query = query.Where(b => b.ShowtimeId == showtimeId || b.BookingSeats.Any(bs => bs.ShowtimeSeat.ShowtimeId == showtimeId));
        }

        if (!string.IsNullOrWhiteSpace(roomId))
        {
            query = query.Where(b => b.Showtime != null && b.Showtime.RoomId == roomId);
        }

        var customerIds = await query
            .Select(b => b.CustomerProfileId)
            .Where(id => !string.IsNullOrEmpty(id))
            .Distinct()
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<string>>.Ok(
            customerIds!,
            $"Đã tìm thấy {customerIds.Count} khách hàng phù hợp.");
    }

    private static VoucherResponse MapToResponse(Voucher voucher)
    {
        return new VoucherResponse
        {
            VoucherId = voucher.VoucherId,
            VoucherCode = voucher.VoucherCode,
            Title = voucher.Title,
            Description = voucher.Description,
            ImageUrl = voucher.ImageUrl,
            DiscountType = voucher.DiscountType,
            DiscountValue = voucher.DiscountValue,
            MinOrderAmount = voucher.MinOrderAmount,
            MaxDiscountAmount = voucher.MaxDiscountAmount,
            UsageLimit = voucher.UsageLimit,
            PerCustomerLimit = voucher.PerCustomerLimit,
            UsedCount = voucher.UsedCount,
            StartDate = voucher.StartDate,
            EndDate = voucher.EndDate,
            VoucherStatus = voucher.VoucherStatus,
            Category = voucher.Category ?? "EVENT",
            ApplicableScope = voucher.ApplicableScope ?? "TOTAL_ORDER",
            TargetType = voucher.TargetType ?? "ALL_CUSTOMERS",
            TargetCustomerIds = voucher.TargetCustomerIds,
            SpecificFbItemIds = voucher.SpecificFbItemIds,
            IsPrivate = voucher.IsPrivate,
            RequiredTicketCount = voucher.RequiredTicketCount
        };
    }
}
