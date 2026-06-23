using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Tickets;

namespace CinemaSystem.Application.Interfaces;

public interface ITicketScanService
{
    Task<ServiceResult<ScanTicketResponse>> ScanAsync(
        string userId,
        string? cinemaScopeId,
        ScanTicketRequest request,
        CancellationToken cancellationToken);
}
