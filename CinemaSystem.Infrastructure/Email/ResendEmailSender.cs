using System.Net.Http.Headers;
using System.Net.Http.Json;
using CinemaSystem.Application.Email;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Email;

public sealed class ResendEmailSender : IEmailSender
{
    private const string SendEmailPath = "emails";

    private readonly HttpClient _httpClient;
    private readonly EmailSettings _settings;

    public ResendEmailSender(HttpClient httpClient, IOptions<EmailSettings> options)
    {
        _httpClient = httpClient;
        _settings = options.Value;
    }

    public async Task SendEmailAsync(
        string toEmail,
        string subject,
        string body,
        CancellationToken cancellationToken)
    {
        await SendEmailAsync(
            new EmailMessage
            {
                ToEmail = toEmail,
                Subject = subject,
                TextBody = IsHtml(body) ? null : body,
                HtmlBody = IsHtml(body) ? body : null
            },
            cancellationToken);
    }

    public async Task SendEmailAsync(
        EmailMessage message,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.SenderEmail) ||
            string.IsNullOrWhiteSpace(_settings.ResendApiKey))
        {
            throw new InvalidOperationException(
                "EmailSettings:SenderEmail and EmailSettings:ResendApiKey must be configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, SendEmailPath);
        request.Headers.Authorization =
            new AuthenticationHeaderValue("Bearer", _settings.ResendApiKey);

        var payload = new Dictionary<string, object?>
        {
            ["from"] = FormatSender(),
            ["to"] = new[] { message.ToEmail },
            ["subject"] = message.Subject
        };

        if (!string.IsNullOrWhiteSpace(message.TextBody))
        {
            payload["text"] = message.TextBody;
        }

        if (!string.IsNullOrWhiteSpace(message.HtmlBody))
        {
            payload["html"] = message.HtmlBody;
        }

        if (message.Attachments.Count > 0)
        {
            payload["attachments"] = message.Attachments.Select(attachment =>
            {
                var value = new Dictionary<string, object?>
                {
                    ["path"] = attachment.Source.AbsoluteUri,
                    ["filename"] = attachment.FileName
                };
                if (!string.IsNullOrWhiteSpace(attachment.ContentId))
                {
                    value["content_id"] = attachment.ContentId;
                }

                return value;
            }).ToArray();
        }

        request.Content = JsonContent.Create(payload);

        using var timeoutCancellation =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCancellation.CancelAfter(TimeSpan.FromSeconds(_settings.SendTimeoutSeconds));

        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            timeoutCancellation.Token);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Resend rejected the email request with HTTP {(int)response.StatusCode}.",
                inner: null,
                response.StatusCode);
        }
    }

    private string FormatSender()
    {
        return string.IsNullOrWhiteSpace(_settings.SenderName)
            ? _settings.SenderEmail
            : $"{_settings.SenderName} <{_settings.SenderEmail}>";
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
