using CinemaSystem.Application.Interfaces;

namespace CinemaSystem.Services;

public sealed class SmtpEmailServiceAdapter : IEmailService
{
    private readonly IEmailSender _emailSender;

    public SmtpEmailServiceAdapter(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public Task SendEmailAsync(string toEmail, string subject, string body, CancellationToken cancellationToken)
    {
        return _emailSender.SendEmailAsync(toEmail, subject, body, cancellationToken);
    }

    public Task SendInvitationAsync(string toEmail, string invitationToken, CancellationToken cancellationToken)
    {
        var body = $"""
            Hello,

            You have been invited to join Cinema Booking as staff.

            Use this invitation code to set your password: {invitationToken}

            If you did not expect this invitation, please ignore this email.
            """;

        return _emailSender.SendEmailAsync(
            toEmail,
            "Cinema Booking - Staff Invitation",
            body,
            cancellationToken);
    }
}
