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

    public VoucherService(CinemaDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
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
            VoucherStatus = DomainConstants.VoucherStatus.Active
        };

        _dbContext.Vouchers.Add(voucher);
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

        // Instead of hard deleting, we soft deactivate it if it has usages, or delete if not used
        var hasUsages = await _dbContext.VoucherUsages.AnyAsync(vu => vu.VoucherId == voucherId, cancellationToken);
        if (hasUsages)
        {
            voucher.VoucherStatus = DomainConstants.VoucherStatus.Inactive;
            await _dbContext.SaveChangesAsync(cancellationToken);
            return ServiceResult<bool>.Ok(true, "Voucher deactivated successfully (retained due to usage records).");
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

        // 5. Check Customer Limit (count active customer usages)
        if (!string.IsNullOrEmpty(customerProfileId))
        {
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
                && v.StartDate <= now
                && v.EndDate >= now
                && v.UsedCount < v.UsageLimit)
            .ToListAsync(cancellationToken);

        var responseList = activeVouchers.Select(MapToResponse).ToList();
        return ServiceResult<IReadOnlyList<VoucherResponse>>.Ok(responseList, "Active vouchers retrieved successfully.");
    }

    private static string NewId(string prefix) => $"{prefix}_{Guid.NewGuid():N}";

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
            VoucherStatus = voucher.VoucherStatus
        };
    }
}
