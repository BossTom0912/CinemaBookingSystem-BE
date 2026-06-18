using System.Net.Http.Json;
using System.Text.Json;
using System.Text;
using CinemaSystem.Application.Common;
using CinemaSystem.Application.Interfaces;
using CinemaSystem.Contracts.Chatbot;
using CinemaSystem.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Services;

public class GeminiChatbotService : IChatbotService
{
    private static readonly HttpClient _httpClient = new HttpClient();
    private readonly IMovieService _movieService;
    private readonly IShowtimeService _showtimeService;
    private readonly GeminiSettings _settings;

    public GeminiChatbotService(
        IMovieService movieService,
        IShowtimeService showtimeService,
        IOptions<GeminiSettings> settings)
    {
        _movieService = movieService;
        _showtimeService = showtimeService;
        _settings = settings.Value;
    }

    public async Task<ServiceResult<ChatbotResponse>> AskAsync(ChatbotRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            return ServiceResult<ChatbotResponse>.Fail(500, "Gemini API key is not configured.", "MISSING_API_KEY");
        }

        // Get context from DB
        var moviesResult = await _movieService.GetMoviesAsync(null, cancellationToken);
        var showtimesResult = await _showtimeService.GetShowtimesAsync(cancellationToken);

        var contextBuilder = new StringBuilder("Movie Theater Context:\n");
        if (moviesResult.Success && moviesResult.Data != null)
        {
            contextBuilder.AppendLine("Available Movies:");
            foreach (var m in moviesResult.Data)
            {
                contextBuilder.AppendLine($"- {m.MovieNameVn} (Genre: {m.Genre}, Duration: {m.Duration}m, Age Rating: {m.AgeRating}, Highlight: {m.Highlight})");
            }
        }
        
        if (showtimesResult.Success && showtimesResult.Data != null)
        {
            contextBuilder.AppendLine("Available Showtimes:");
            foreach (var s in showtimesResult.Data)
            {
                contextBuilder.AppendLine($"- Movie: {s.MovieTitle}, Time: {s.StartTime:yyyy-MM-dd HH:mm} to {s.EndTime:HH:mm}, Cinema: {s.CinemaName}, Room: {s.RoomName}, Price: {s.BasePrice}, Status: {s.Status}");
            }
        }

        string systemPrompt = $@"You are a helpful customer support AI for our movie theater system.
Use the following context to answer the user's questions about movies and showtimes.
If you don't know the answer or the information is not in the context, politely say you don't have that information.
Keep your answers concise, friendly, and formatted nicely.

Context:
{contextBuilder.ToString()}";

        var payload = new
        {
            system_instruction = new { parts = new[] { new { text = systemPrompt } } },
            contents = new[]
            {
                new { role = "user", parts = new[] { new { text = request.Message } } }
            }
        };

        var url = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-3.1-flash-lite:generateContent?key={_settings.ApiKey}";

        var response = await _httpClient.PostAsJsonAsync(url, payload, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            var errorDetails = await response.Content.ReadAsStringAsync(cancellationToken);
            return ServiceResult<ChatbotResponse>.Fail(500, $"AI API error: {response.StatusCode} - {errorDetails}", "AI_API_ERROR");
        }

        var jsonDoc = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
        var replyText = "I'm sorry, I couldn't generate a response.";
        try
        {
            replyText = jsonDoc.GetProperty("candidates")[0]
                               .GetProperty("content")
                               .GetProperty("parts")[0]
                               .GetProperty("text").GetString();
        }
        catch { }

        return ServiceResult<ChatbotResponse>.Ok(new ChatbotResponse { Reply = replyText ?? string.Empty });
    }
}
