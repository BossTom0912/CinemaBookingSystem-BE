using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Vouchers;

namespace CinemaSystem.Application.Interfaces;

public interface IVoucherService
{
    Task<ServiceResult<VoucherResponse>> CreateVoucherAsync(
        CreateVoucherRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<VoucherResponse>>> GetAllVouchersAsync(
        string? searchCode,
        string? status,
        CancellationToken cancellationToken);

    Task<ServiceResult<VoucherResponse>> GetVoucherByIdAsync(
        string voucherId,
        CancellationToken cancellationToken);

    Task<ServiceResult<VoucherResponse>> UpdateVoucherAsync(
        string voucherId,
        UpdateVoucherRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> DeleteVoucherAsync(
        string voucherId,
        CancellationToken cancellationToken);

    Task<ServiceResult<ValidateVoucherResponse>> ValidateVoucherForCustomerAsync(
        string voucherCode,
        decimal bookingAmount,
        string userId,
        CancellationToken cancellationToken);

    Task<ServiceResult<VoucherValidationResult>> ValidateAndGetVoucherAsync(
        string voucherCode,
        decimal bookingAmount,
        string customerProfileId,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<VoucherResponse>>> GetActiveVouchersForCustomerAsync(
        CancellationToken cancellationToken);

    Task<ServiceResult<bool>> ClaimVoucherForCustomerAsync(
        string voucherId,
        string userId,
        CancellationToken cancellationToken);

    Task<ServiceResult<IReadOnlyList<VoucherResponse>>> GetMyVouchersAsync(
        string userId,
        CancellationToken cancellationToken);

    Task<ServiceResult<int>> IssueCompensationVoucherAsync(
        IssueCompensationRequest request,
        CancellationToken cancellationToken);
}
