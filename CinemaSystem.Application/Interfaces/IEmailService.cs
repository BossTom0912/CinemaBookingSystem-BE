namespace CinemaSystem.Application.Interfaces;

public interface IEmailService
{
    Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken);

    Task SendAccountInvitationAsync(string toEmail, string invitationToken, CancellationToken cancellationToken);
}
