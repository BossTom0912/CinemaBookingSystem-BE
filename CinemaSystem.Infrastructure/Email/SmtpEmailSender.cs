using System.Net;
using System.Net.Mail;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailSettings _settings;

    public SmtpEmailSender(IOptions<EmailSettings> options)
    {
        _settings = options.Value;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.SenderEmail) || string.IsNullOrWhiteSpace(_settings.Password))
        {
            throw new InvalidOperationException("EmailSettings:SenderEmail and EmailSettings:Password must be configured.");
        }

        using var message = new MailMessage
        {
            From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
            Subject = subject,
            SubjectEncoding = System.Text.Encoding.UTF8,
            Body = body,
            BodyEncoding = System.Text.Encoding.UTF8,
            IsBodyHtml = IsHtml(body)
        };
        message.To.Add(toEmail);

        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_settings.SenderEmail, _settings.Password)
        };

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromSeconds(_settings.SendTimeoutSeconds));

        await client.SendMailAsync(message, timeoutCancellation.Token);
    }

    private static bool IsHtml(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return false;
        var trimmed = body.TrimStart();
        return trimmed.StartsWith("<", StringComparison.Ordinal) ||
               body.Contains("<html", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("<body", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("<div", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("<p", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("<table", StringComparison.OrdinalIgnoreCase) ||
               body.Contains("<!DOCTYPE", StringComparison.OrdinalIgnoreCase);
    }
}
