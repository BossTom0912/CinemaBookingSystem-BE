using CinemaSystem.Application.Interfaces;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Services;

public sealed class VoucherReservationService : IVoucherReservationService
{
    private readonly CinemaDbContext _dbContext;

    public VoucherReservationService(CinemaDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<CustomerVoucher?> FindAvailableClaimAsync(
        string voucherId,
        string customerProfileId,
        CancellationToken cancellationToken)
    {
        return _dbContext.CustomerVouchers
            .Where(claim => claim.VoucherId == voucherId
                && claim.CustomerProfileId == customerProfileId
                && !claim.IsUsed
                && !claim.VoucherUsages.Any(usage =>
                    usage.UsageStatus != DomainConstants.VoucherUsageStatus.Cancelled))
            .OrderBy(claim => claim.ClaimedAt)
            .ThenBy(claim => claim.CustomerVoucherId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> ConfirmAsync(
        VoucherUsage voucherUsage,
        DateTime confirmedAt,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                voucherUsage.UsageStatus,
                DomainConstants.VoucherUsageStatus.Applied,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var claim = await GetExactClaimAsync(voucherUsage, cancellationToken);
        if (claim is not null)
        {
            claim.IsUsed = true;
            claim.UsedAt = confirmedAt;
        }

        voucherUsage.UsageStatus = DomainConstants.VoucherUsageStatus.Confirmed;
        voucherUsage.UsedAt = confirmedAt;
        return true;
    }

    public async Task<bool> CancelAsync(
        VoucherUsage voucherUsage,
        CancellationToken cancellationToken)
    {
        if (string.Equals(
                voucherUsage.UsageStatus,
                DomainConstants.VoucherUsageStatus.Cancelled,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var wasConfirmed = string.Equals(
            voucherUsage.UsageStatus,
            DomainConstants.VoucherUsageStatus.Confirmed,
            StringComparison.OrdinalIgnoreCase);

        var claim = await GetExactClaimAsync(voucherUsage, cancellationToken);
        if (claim is not null && claim.IsUsed)
        {
            claim.IsUsed = false;
            claim.UsedAt = null;
        }

        voucherUsage.UsageStatus = DomainConstants.VoucherUsageStatus.Cancelled;
        return wasConfirmed;
    }

    private async Task<CustomerVoucher?> GetExactClaimAsync(
        VoucherUsage voucherUsage,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(voucherUsage.CustomerVoucherId))
        {
            return null;
        }

        var claim = voucherUsage.CustomerVoucher
            ?? await _dbContext.CustomerVouchers
                .SingleOrDefaultAsync(
                    item => item.CustomerVoucherId == voucherUsage.CustomerVoucherId,
                    cancellationToken);

        if (claim is null
            || !string.Equals(claim.VoucherId, voucherUsage.VoucherId, StringComparison.Ordinal)
            || !string.Equals(
                claim.CustomerProfileId,
                voucherUsage.CustomerProfileId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Voucher usage {voucherUsage.VoucherUsageId} has an invalid customer voucher link.");
        }

        return claim;
    }
}
