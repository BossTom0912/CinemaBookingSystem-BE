using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Tickets;

namespace CinemaSystem.Application.Interfaces;

public interface ITicketScanService
{
    Task<ServiceResult<ScanTicketResponse>> PreviewAsync(
        string userId,
        string actorRole,
        ScanTicketRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ScanTicketResponse>> ConfirmAsync(
        string userId,
        string actorRole,
        ConfirmTicketScanRequest request,
        CancellationToken cancellationToken);

    Task<ServiceResult<ScanTicketResponse>> ScanAsync(
        string userId,
        string actorRole,
        ScanTicketRequest request,
        CancellationToken cancellationToken);
}
