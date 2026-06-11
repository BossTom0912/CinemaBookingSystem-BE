using System.Net;
using System.Net.Http.Json;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Chatbot;
using CinemaSystem.Contracts.Common;
using CinemaSystem.Tests.Infrastructure;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Moq;

namespace CinemaSystem.Tests;

/// <summary>
/// Integration tests for the Chatbot API endpoint (/api/chatbot).
/// </summary>
public sealed class ChatbotApiIntegrationTests
{
    [Fact]
    public async Task AskChatbot_Success_ReturnsReply()
    {
        // Arrange
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var mockService = new Mock<IChatbotService>();
                mockService.Setup(s => s.AskAsync(It.IsAny<ChatbotRequest>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(ServiceResult<ChatbotResponse>.Ok(new ChatbotResponse { Reply = "Hello from mock AI!" }));
                services.RemoveAll<IChatbotService>();
                services.AddScoped<IChatbotService>(_ => mockService.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/chatbot", new ChatbotRequest { Message = "Hello" });

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<ChatbotResponse>>();
        Assert.NotNull(body);
        Assert.True(body.Success);
        Assert.Equal("Hello from mock AI!", body.Data!.Reply);
    }

    [Fact]
    public async Task AskChatbot_Failure_ReturnsBadRequest()
    {
        // Arrange
        await using var factory = new CinemaWebApplicationFactory();
        using var client = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureTestServices(services =>
            {
                var mockService = new Mock<IChatbotService>();
                mockService.Setup(s => s.AskAsync(It.IsAny<ChatbotRequest>(), It.IsAny<CancellationToken>()))
                           .ReturnsAsync(ServiceResult<ChatbotResponse>.Fail(400, "Gemini API key is not configured.", "MISSING_API_KEY"));
                services.RemoveAll<IChatbotService>();
                services.AddScoped<IChatbotService>(_ => mockService.Object);
            });
        }).CreateClient();

        // Act
        var response = await client.PostAsJsonAsync("/api/chatbot", new ChatbotRequest { Message = "Hello" });

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ApiResponse<object>>();
        Assert.NotNull(body);
        Assert.False(body.Success);
        Assert.Equal("MISSING_API_KEY", body.ErrorCode);
    }
}
