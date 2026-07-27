using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text.Encodings.Web;
using CinemaSystem.Application.Email;
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

    public Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        var isHtml = IsHtml(body);
        return SendEmailAsync(
            new EmailMessage
            {
                ToEmail = toEmail,
                Subject = subject,
                TextBody = isHtml ? null : body,
                HtmlBody = isHtml ? body : null
            },
            cancellationToken);
    }

    public async Task SendEmailAsync(
        EmailMessage email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.SenderEmail) || string.IsNullOrWhiteSpace(_settings.Password))
        {
            throw new InvalidOperationException("EmailSettings:SenderEmail and EmailSettings:Password must be configured.");
        }

        using var timeoutCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromSeconds(_settings.SendTimeoutSeconds));

        var htmlBody = ResolveRemoteInlineImages(email);
        using var message = new MailMessage
        {
            From = new MailAddress(_settings.SenderEmail, _settings.SenderName),
            Subject = email.Subject,
            SubjectEncoding = System.Text.Encoding.UTF8,
            Body = htmlBody ?? email.TextBody ?? string.Empty,
            BodyEncoding = System.Text.Encoding.UTF8,
            IsBodyHtml = !string.IsNullOrWhiteSpace(htmlBody)
        };
        message.To.Add(email.ToEmail);

        if (!string.IsNullOrWhiteSpace(email.TextBody))
        {
            message.AlternateViews.Add(
                AlternateView.CreateAlternateViewFromString(
                    email.TextBody,
                    contentEncoding: null,
                    MediaTypeNames.Text.Plain));
        }

        if (!string.IsNullOrWhiteSpace(htmlBody))
        {
            var htmlView = AlternateView.CreateAlternateViewFromString(
                htmlBody,
                contentEncoding: null,
                MediaTypeNames.Text.Html);
            message.AlternateViews.Add(htmlView);
        }

        if (email.Attachments.Any(attachment =>
                string.IsNullOrWhiteSpace(attachment.ContentId)))
        {
            throw new NotSupportedException(
                "SMTP remote file attachments are not supported.");
        }

        using var client = new SmtpClient(_settings.SmtpHost, _settings.SmtpPort)
        {
            EnableSsl = true,
            Credentials = new NetworkCredential(_settings.SenderEmail, _settings.Password)
        };

        await client.SendMailAsync(message, timeoutCancellation.Token);
    }

    private static string? ResolveRemoteInlineImages(EmailMessage email)
    {
        var htmlBody = email.HtmlBody;
        if (string.IsNullOrWhiteSpace(htmlBody))
        {
            return htmlBody;
        }

        foreach (var attachment in email.Attachments.Where(attachment =>
                     !string.IsNullOrWhiteSpace(attachment.ContentId)))
        {
            htmlBody = htmlBody.Replace(
                $"cid:{attachment.ContentId}",
                HtmlEncoder.Default.Encode(attachment.Source.AbsoluteUri),
                StringComparison.Ordinal);
        }

        return htmlBody;
    }

    private static bool IsHtml(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

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
