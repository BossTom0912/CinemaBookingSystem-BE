using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.FoodAndBeverage;

namespace CinemaSystem.Application.Interfaces;

public interface IFbItemService
{
    Task<ServiceResult<FbItemResponse>> CreateAsync(CreateFbItemRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<FbItemResponse>>> GetAllActiveAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<FbItemResponse>>> GetAllForAdminAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<FbItemResponse>> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<ServiceResult<FbItemResponse>> UpdateAsync(string id, UpdateFbItemRequest request, CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> DeactivateAsync(string id, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<CinemaFbInventoryResponse>>> GetCinemaInventoryAsync(string cinemaId, CancellationToken cancellationToken = default);

    Task<ServiceResult<CinemaFbInventoryResponse>> UpdateCinemaInventoryAsync(UpdateCinemaFbInventoryRequest request, string? currentStaffCinemaId, bool isSystemAdmin, CancellationToken cancellationToken = default);

    Task<ServiceResult<FbFulfillmentResponse>> CreateCounterOrderAsync(CreateCounterFbOrderRequest request, string staffProfileId, string currentStaffCinemaId, CancellationToken cancellationToken = default);

    Task<ServiceResult<FbFulfillmentResponse>> FulfillOrderAsync(string bookingId, string staffProfileId, string currentStaffCinemaId, CancellationToken cancellationToken = default);

    Task<ServiceResult<bool>> RestockCanceledOrderAsync(string bookingId, CancellationToken cancellationToken = default);
}
