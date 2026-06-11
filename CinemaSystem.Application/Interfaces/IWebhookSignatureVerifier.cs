namespace CinemaSystem.Application.Interfaces;

public interface IWebhookSignatureVerifier
{
    bool Verify(string signature, string timestamp, string payload);
}
