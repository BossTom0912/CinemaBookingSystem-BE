using CinemaSystem.Domain.Entities;

namespace CinemaSystem.Application.Interfaces;

public interface IVoucherReservationService
{
    Task<CustomerVoucher?> FindAvailableClaimAsync(
        string voucherId,
        string customerProfileId,
        CancellationToken cancellationToken);

    Task<bool> ConfirmAsync(
        VoucherUsage voucherUsage,
        DateTime confirmedAt,
        CancellationToken cancellationToken);

    Task<bool> CancelAsync(
        VoucherUsage voucherUsage,
        CancellationToken cancellationToken);
}
