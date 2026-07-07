using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Application.Interfaces;

public interface IAiEmailService
{
    Task SendAiApologyEmailAsync(
        string toEmail, 
        string subject, 
        string reason, 
        string details, 
        CancellationToken cancellationToken);
}
