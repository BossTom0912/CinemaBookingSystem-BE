using CinemaSystem.Application.Common;
using CinemaSystem.Contracts.Chatbot;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Application.Interfaces;

public interface IChatbotService
{
    Task<ServiceResult<ChatbotResponse>> AskAsync(ChatbotRequest request, CancellationToken cancellationToken);
}
