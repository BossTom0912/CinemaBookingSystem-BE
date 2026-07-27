namespace CinemaSystem.Application.Email;

public sealed record EmailMessage
{
    public required string ToEmail { get; init; }

    public required string Subject { get; init; }

    public string? TextBody { get; init; }

    public string? HtmlBody { get; init; }

    public IReadOnlyList<EmailAttachment> Attachments { get; init; } = [];
}

public sealed record EmailAttachment
{
    public required string FileName { get; init; }

    public required Uri Source { get; init; }

    public string? ContentId { get; init; }
}
