using System.Linq.Expressions;
using CinemaSystem.Application.Common;
using CinemaSystem.Domain.Entities;

namespace CinemaSystem.Application.Interfaces;

public interface IVoucherAccessPolicy
{
    Expression<Func<Voucher, bool>> GetPublicDisclosurePredicate(DateTime utcNow);

    bool CanDisclosePublicly(Voucher voucher);

    Task<bool> CanCustomerUseAsync(
        Voucher voucher,
        string customerProfileId,
        CancellationToken cancellationToken);

    Task<bool> CanCustomerClaimAsync(
        Voucher voucher,
        string customerProfileId,
        CancellationToken cancellationToken);

    VoucherPolicyValidationResult ValidateConfiguration(Voucher voucher);
}
