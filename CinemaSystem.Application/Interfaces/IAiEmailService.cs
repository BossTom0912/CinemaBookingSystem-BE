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
        string oldTime,
        string newTime,
        string cutoffTime,
        string bookingId,
        string token,
        CancellationToken cancellationToken,
        string? compensationVoucherCode = null,
        string? compensationNote = null,
        string? targetSeatType = null);

    Task SendAiRoomChangeEmailAsync(
        string toEmail,
        string subject,
        string movieTitle,
        string oldRoomName,
        string newRoomName,
        string timeStr,
        string bookingId,
        CancellationToken cancellationToken,
        string? compensationVoucherCode = null,
        string? compensationNote = null,
        string? targetSeatType = null);
}
