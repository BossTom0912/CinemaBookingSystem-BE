using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Email;

/// <summary>
/// Development email sink. It deliberately never logs message bodies because
/// they may contain OTPs, invitation codes, or confirmation tokens.
/// </summary>
public sealed class MockEmailService : IEmailSender, IEmailService
{
    private readonly ILogger<MockEmailService> _logger;
    private readonly EmailTemplatesSettings _templates;

    public MockEmailService(
        ILogger<MockEmailService> logger,
        IOptions<EmailTemplatesSettings> templateOptions)
    {
        _logger = logger;
        _templates = templateOptions.Value;
    }

    public Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[MockEmail] To={ToEmail} Subject={Subject}",
            toEmail,
            subject);
        return Task.CompletedTask;
    }

    public Task SendInvitationAsync(
        string toEmail,
        string invitationToken,
        CancellationToken cancellationToken)
    {
        return SendEmailAsync(
            toEmail,
            _templates.StaffInvitationSubject,
            string.Format(_templates.StaffInvitationBody, invitationToken),
            cancellationToken);
    }
}
