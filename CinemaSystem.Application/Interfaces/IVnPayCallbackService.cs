using CinemaSystem.Contracts.Payments;

namespace CinemaSystem.Application.Interfaces;

public interface IVnPayCallbackService
{
    Task<VnPayIpnResponse> HandleIpnAsync(
        IReadOnlyDictionary<string, string> parameters,
        CancellationToken cancellationToken = default);

    bool HasValidSignature(IReadOnlyDictionary<string, string> parameters);
}
