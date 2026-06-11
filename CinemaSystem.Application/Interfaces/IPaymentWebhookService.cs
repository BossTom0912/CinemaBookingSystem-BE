using CinemaSystem.Application.Common;

namespace CinemaSystem.Application.Interfaces;

public interface IPaymentWebhookService
{
    Task<ServiceResult<object>> HandleSepayWebhookAsync(
        string payload,
        string? signatureHeader,
        string? timestampHeader,
        CancellationToken cancellationToken);
}
