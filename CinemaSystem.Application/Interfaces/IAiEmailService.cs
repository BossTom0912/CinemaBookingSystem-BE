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

    Task SendAiTimeChangeEmailAsync(
        string toEmail,
        string subject,
        string movieTitle,
        string newTime,
        string bookingId,
        string token,
        CancellationToken cancellationToken);
}
