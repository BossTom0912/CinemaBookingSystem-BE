namespace CinemaSystem.Application.Interfaces;

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken);
}
