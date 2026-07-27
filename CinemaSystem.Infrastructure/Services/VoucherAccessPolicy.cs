using System.Linq.Expressions;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Domain.Constants;
using CinemaSystem.Domain.Entities;
using CinemaSystem.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CinemaSystem.Infrastructure.Services;

public sealed class VoucherAccessPolicy : IVoucherAccessPolicy
{
    private static readonly char[] TargetSeparators = [',', ';', ' ', '\n', '\r', '\t'];

    private readonly CinemaDbContext _dbContext;
    private readonly IClock _clock;

    public VoucherAccessPolicy(CinemaDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public Expression<Func<Voucher, bool>> GetPublicDisclosurePredicate(DateTime utcNow)
    {
        return voucher =>
            voucher.VoucherStatus == DomainConstants.VoucherStatus.Active
            && !voucher.IsPrivate
            && voucher.TargetType == DomainConstants.VoucherTargetType.AllCustomers
            && (voucher.TargetCustomerIds == null || voucher.TargetCustomerIds == string.Empty)
            && voucher.StartDate <= utcNow
            && voucher.EndDate >= utcNow
            && voucher.UsedCount < voucher.UsageLimit;
    }

    public bool CanDisclosePublicly(Voucher voucher)
    {
        var now = _clock.UtcNow;
        return string.Equals(
                voucher.VoucherStatus,
                DomainConstants.VoucherStatus.Active,
                StringComparison.OrdinalIgnoreCase)
            && !voucher.IsPrivate
            && string.Equals(
                voucher.TargetType,
                DomainConstants.VoucherTargetType.AllCustomers,
                StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(voucher.TargetCustomerIds)
            && voucher.StartDate <= now
            && voucher.EndDate >= now
            && voucher.UsedCount < voucher.UsageLimit;
    }

    public Task<bool> CanCustomerUseAsync(
        Voucher voucher,
        string customerProfileId,
        CancellationToken cancellationToken)
    {
        return HasCustomerAccessAsync(voucher, customerProfileId, cancellationToken);
    }

    public Task<bool> CanCustomerClaimAsync(
        Voucher voucher,
        string customerProfileId,
        CancellationToken cancellationToken)
    {
        return HasCustomerAccessAsync(voucher, customerProfileId, cancellationToken);
    }

    public VoucherPolicyValidationResult ValidateConfiguration(Voucher voucher)
    {
        var isAllCustomers = string.Equals(
            voucher.TargetType,
            DomainConstants.VoucherTargetType.AllCustomers,
            StringComparison.OrdinalIgnoreCase);
        var isSpecificCustomers = string.Equals(
            voucher.TargetType,
            DomainConstants.VoucherTargetType.SpecificCustomers,
            StringComparison.OrdinalIgnoreCase);
        var hasTargets = !string.IsNullOrWhiteSpace(voucher.TargetCustomerIds);

        if (!isAllCustomers && !isSpecificCustomers)
        {
            return VoucherPolicyValidationResult.Invalid(
                "INVALID_VOUCHER_TARGET_TYPE",
                $"Target type must be {DomainConstants.VoucherTargetType.AllCustomers} "
                + $"or {DomainConstants.VoucherTargetType.SpecificCustomers}.");
        }

        if (voucher.IsPrivate && !isSpecificCustomers)
        {
            return VoucherPolicyValidationResult.Invalid(
                "PRIVATE_VOUCHER_REQUIRES_TARGET",
                "Private vouchers must target specific customers.");
        }

        if (isSpecificCustomers && !voucher.IsPrivate)
        {
            return VoucherPolicyValidationResult.Invalid(
                "TARGETED_VOUCHER_MUST_BE_PRIVATE",
                "Vouchers for specific customers must be private.");
        }

        if (isSpecificCustomers && !hasTargets)
        {
            return VoucherPolicyValidationResult.Invalid(
                "VOUCHER_TARGET_REQUIRED",
                "A specific-customer voucher must have at least one valid customer.");
        }

        if (isAllCustomers && hasTargets)
        {
            return VoucherPolicyValidationResult.Invalid(
                "PUBLIC_VOUCHER_CANNOT_HAVE_TARGETS",
                "A public voucher cannot contain customer targets.");
        }

        if (string.Equals(
                voucher.Category,
                DomainConstants.VoucherCategory.Compensation,
                StringComparison.OrdinalIgnoreCase)
            && (!voucher.IsPrivate || !isSpecificCustomers || !hasTargets))
        {
            return VoucherPolicyValidationResult.Invalid(
                "COMPENSATION_VOUCHER_REQUIRES_CUSTOMER",
                "Compensation vouchers must be private and assigned to specific customers.");
        }

        return VoucherPolicyValidationResult.Valid();
    }

    private async Task<bool> HasCustomerAccessAsync(
        Voucher voucher,
        string customerProfileId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerProfileId))
        {
            return false;
        }

        if (!ValidateConfiguration(voucher).IsValid)
        {
            return false;
        }

        var isExplicitPublicTarget = string.Equals(
            voucher.TargetType,
            DomainConstants.VoucherTargetType.AllCustomers,
            StringComparison.OrdinalIgnoreCase);
        if (!voucher.IsPrivate
            && isExplicitPublicTarget
            && string.IsNullOrWhiteSpace(voucher.TargetCustomerIds))
        {
            return true;
        }

        var isAssigned = await _dbContext.CustomerVouchers
            .AsNoTracking()
            .AnyAsync(
                customerVoucher =>
                    customerVoucher.VoucherId == voucher.VoucherId
                    && customerVoucher.CustomerProfileId == customerProfileId,
                cancellationToken);
        if (isAssigned)
        {
            return true;
        }

        var targetIdentifiers = ParseTargetIdentifiers(voucher.TargetCustomerIds);
        if (targetIdentifiers.Count == 0)
        {
            return false;
        }

        var customer = await _dbContext.CustomerProfiles
            .AsNoTracking()
            .Where(profile => profile.CustomerProfileId == customerProfileId)
            .Select(profile => new
            {
                profile.CustomerProfileId,
                profile.UserId,
                Email = profile.User != null ? profile.User.Email : null
            })
            .FirstOrDefaultAsync(cancellationToken);
        if (customer == null)
        {
            return false;
        }

        return targetIdentifiers.Contains(customer.CustomerProfileId)
            || targetIdentifiers.Contains(customer.UserId)
            || (!string.IsNullOrWhiteSpace(customer.Email)
                && targetIdentifiers.Contains(customer.Email));
    }

    private static HashSet<string> ParseTargetIdentifiers(string? targetCustomerIds)
    {
        if (string.IsNullOrWhiteSpace(targetCustomerIds))
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        return targetCustomerIds
            .Split(TargetSeparators, StringSplitOptions.RemoveEmptyEntries)
            .Select(identifier => identifier.Trim())
            .Where(identifier => identifier.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
