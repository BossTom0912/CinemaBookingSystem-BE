using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Email;

public sealed class SmtpEmailServiceAdapter : IEmailService
{
    private readonly IEmailSender _emailSender;
    private readonly EmailTemplatesSettings _templates;

    public SmtpEmailServiceAdapter(
        IEmailSender emailSender,
        IOptions<EmailTemplatesSettings> templateOptions)
    {
        _emailSender = emailSender;
        _templates = templateOptions.Value;
    }

    public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        return _emailSender.SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public Task SendAccountInvitationAsync(string toEmail, string invitationToken, CancellationToken cancellationToken)
    {
        var body = string.Format(_templates.AccountInvitationBody, invitationToken);

        return _emailSender.SendEmailAsync(
            toEmail,
            _templates.AccountInvitationSubject,
            body,
            cancellationToken);
    }
}
