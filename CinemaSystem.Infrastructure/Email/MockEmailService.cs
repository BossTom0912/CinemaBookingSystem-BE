using CinemaSystem.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace CinemaSystem.Infrastructure.Email;

public sealed class MockEmailService : IEmailSender, IEmailService
{
    private readonly ILogger<MockEmailService> _logger;

    public MockEmailService(ILogger<MockEmailService> logger)
    {
        _logger = logger;
    }

    public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "[MockEmail] To={ToEmail} Subject={Subject}\n{Body}",
            toEmail,
            subject,
            body);
        Console.WriteLine($"[MockEmail] To: {toEmail}");
        Console.WriteLine($"[MockEmail] Subject: {subject}");
        Console.WriteLine($"[MockEmail] Body:\n{body}");
        return Task.CompletedTask;
    }

    public Task SendInvitationAsync(string toEmail, string invitationToken, CancellationToken cancellationToken)
    {
        var body = $"""
            Hello,

            You have been invited to join Cinema Booking as staff.

            Use this invitation code to set your password: {invitationToken}

            If you did not expect this invitation, please ignore this email.
            """;

        return SendEmailAsync(
            toEmail,
            "Cinema Booking - Staff Invitation",
            body,
            cancellationToken);
    }
}
