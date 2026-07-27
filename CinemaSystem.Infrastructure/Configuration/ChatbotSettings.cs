namespace CinemaSystem.Infrastructure.Configuration;

public sealed class ChatbotSettings
{
    public const string SectionName = "Chatbot";

    // Fail closed: public voucher codes are not sent to an external model
    // until the deployment explicitly enables the reviewed disclosure path.
    public bool ExposePublicVouchers { get; set; }
}
