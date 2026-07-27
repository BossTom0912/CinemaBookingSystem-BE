using System.Net;
using System.Text.Json;
using CinemaSystem.Application.Email;
using CinemaSystem.Infrastructure.Configuration;
using CinemaSystem.Infrastructure.Email;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Tests;

public sealed class ResendEmailSenderTests
{
    [Fact]
    public async Task SendEmailAsync_PostsPlainTextEmailWithBearerAuthentication()
    {
        HttpRequestMessage? capturedRequest = null;
        string? capturedContent = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedRequest = request;
            capturedContent = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var sender = CreateSender(handler);

        await sender.SendEmailAsync(
            "customer@example.com",
            "Verify your email",
            "Your verification code is ready.",
            CancellationToken.None);

        Assert.NotNull(capturedRequest);
        Assert.Equal(HttpMethod.Post, capturedRequest.Method);
        Assert.Equal("https://api.resend.com/emails", capturedRequest.RequestUri!.AbsoluteUri);
        Assert.Equal("Bearer", capturedRequest.Headers.Authorization!.Scheme);
        Assert.Equal("test-api-key", capturedRequest.Headers.Authorization.Parameter);

        using var document = JsonDocument.Parse(capturedContent!);
        var root = document.RootElement;
        Assert.Equal(
            "Cinema Booking System <noreply@mail.cinema.beer>",
            root.GetProperty("from").GetString());
        Assert.Equal(
            "customer@example.com",
            root.GetProperty("to")[0].GetString());
        Assert.Equal("Verify your email", root.GetProperty("subject").GetString());
        Assert.Equal(
            "Your verification code is ready.",
            root.GetProperty("text").GetString());
        Assert.False(root.TryGetProperty("html", out _));
    }

    [Fact]
    public async Task SendEmailAsync_PostsHtmlWhenBodyContainsHtmlDocument()
    {
        string? capturedContent = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedContent = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var sender = CreateSender(handler);

        await sender.SendEmailAsync(
            "customer@example.com",
            "Verify your email",
            "<html><body>Verify</body></html>",
            CancellationToken.None);

        using var document = JsonDocument.Parse(capturedContent!);
        Assert.Equal(
            "<html><body>Verify</body></html>",
            document.RootElement.GetProperty("html").GetString());
        Assert.False(document.RootElement.TryGetProperty("text", out _));
    }

    [Fact]
    public async Task SendEmailAsync_PostsInlineRemoteAttachmentWithContentId()
    {
        string? capturedContent = null;
        var handler = new StubHttpMessageHandler(async request =>
        {
            capturedContent = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        var sender = CreateSender(handler);
        var proofUri = new Uri(
            "https://cdn.example.com/refunds/proof.png?key=one&signature=two");

        await sender.SendEmailAsync(
            new EmailMessage
            {
                ToEmail = "customer@example.com",
                Subject = "Refund completed",
                TextBody = $"Transfer proof: {proofUri}",
                HtmlBody =
                    "<html><body><img src=\"cid:refund-proof\"></body></html>",
                Attachments =
                [
                    new EmailAttachment
                    {
                        FileName = "proof.png",
                        Source = proofUri,
                        ContentId = "refund-proof"
                    }
                ]
            },
            CancellationToken.None);

        using var document = JsonDocument.Parse(capturedContent!);
        var root = document.RootElement;
        Assert.Equal(
            "<html><body><img src=\"cid:refund-proof\"></body></html>",
            root.GetProperty("html").GetString());
        Assert.Equal(
            $"Transfer proof: {proofUri}",
            root.GetProperty("text").GetString());

        var attachment = Assert.Single(root.GetProperty("attachments").EnumerateArray());
        Assert.Equal(proofUri.AbsoluteUri, attachment.GetProperty("path").GetString());
        Assert.Equal("proof.png", attachment.GetProperty("filename").GetString());
        Assert.Equal("refund-proof", attachment.GetProperty("content_id").GetString());
    }

    [Fact]
    public async Task SendEmailAsync_ThrowsWhenResendRejectsRequest()
    {
        var handler = new StubHttpMessageHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)));
        var sender = CreateSender(handler);

        var exception = await Assert.ThrowsAsync<HttpRequestException>(() =>
            sender.SendEmailAsync(
                "customer@example.com",
                "Verify your email",
                "Body",
                CancellationToken.None));

        Assert.Equal(HttpStatusCode.UnprocessableEntity, exception.StatusCode);
        Assert.DoesNotContain("Body", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("test-api-key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendEmailAsync_RequiresApiKey()
    {
        var handler = new StubHttpMessageHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)));
        var sender = CreateSender(handler, apiKey: string.Empty);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sender.SendEmailAsync(
                "customer@example.com",
                "Verify your email",
                "Body",
                CancellationToken.None));
    }

    private static ResendEmailSender CreateSender(
        HttpMessageHandler handler,
        string apiKey = "test-api-key")
    {
        var client = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.resend.com/")
        };
        var options = Options.Create(new EmailSettings
        {
            Provider = EmailSettings.ResendProvider,
            SenderEmail = "noreply@mail.cinema.beer",
            SenderName = "Cinema Booking System",
            ResendApiKey = apiKey,
            ResendApiBaseUrl = "https://api.resend.com/",
            SendTimeoutSeconds = 15
        });

        return new ResendEmailSender(client, options);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, Task<HttpResponseMessage>> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _handler(request);
        }
    }
}
