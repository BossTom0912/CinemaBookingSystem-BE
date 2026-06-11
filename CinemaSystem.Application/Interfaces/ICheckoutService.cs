using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Bookings;

namespace CinemaSystem.Application.Interfaces;

public interface ICheckoutService
{
    Task<ServiceResult<CheckoutResponse>> CheckoutAsync(
        string userId,
        CheckoutRequest request,
        CancellationToken cancellationToken);
}
