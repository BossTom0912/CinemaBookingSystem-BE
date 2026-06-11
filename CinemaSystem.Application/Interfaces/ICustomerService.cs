using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Customers;

namespace CinemaSystem.Application.Interfaces;

public interface ICustomerService
{
    Task<ServiceResult<CustomerProfileResponse>> GetProfileAsync(string userId, CancellationToken cancellationToken);

    Task<ServiceResult<CustomerProfileResponse>> UpdateProfileAsync(string userId, UpdateProfileRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<object>> ChangePasswordAsync(string userId, ChangePasswordRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<object>> RequestEmailUpdateAsync(string userId, UpdateEmailRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<object>> VerifyEmailUpdateAsync(string userId, VerifyEmailUpdateRequest request, CancellationToken cancellationToken);

    Task<ServiceResult<List<BookingHistoryResponse>>> GetBookingHistoryAsync(string userId, CancellationToken cancellationToken);
}
