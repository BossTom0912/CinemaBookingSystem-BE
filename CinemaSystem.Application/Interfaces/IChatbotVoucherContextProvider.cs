using CinemaSystem.Contracts.Chatbot;

namespace CinemaSystem.Application.Interfaces;

public interface IChatbotVoucherContextProvider
{
    Task<IReadOnlyList<PublicVoucherChatContext>> GetPublicVouchersAsync(
        CancellationToken cancellationToken);
}
