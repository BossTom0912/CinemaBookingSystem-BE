using CinemaSystem.Application.Email;

namespace CinemaSystem.Application.Interfaces;

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken);

    Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken);
}
