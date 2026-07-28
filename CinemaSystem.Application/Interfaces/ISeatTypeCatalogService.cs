using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Seats;

namespace CinemaSystem.Application.Interfaces;

public interface ISeatTypeCatalogService
{
    Task<ServiceResult<IReadOnlyList<SeatTypeResponse>>> GetAllAsync(
        bool includeInactive,
        CancellationToken cancellationToken);

    Task<ServiceResult<SeatTypeResponse>> CreateAsync(
        UpsertSeatTypeRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<SeatTypeResponse>> UpdateAsync(
        string seatTypeId,
        UpsertSeatTypeRequest request,
        CancellationToken cancellationToken);
}
