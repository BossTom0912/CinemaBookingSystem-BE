namespace CinemaSystem.Infrastructure.Configuration;

public sealed class GeminiSettings
{
    public const string SectionName = "GeminiSettings";
    public const string ApiKeyHeaderName = "x-goog-api-key";

    public string ApiKey { get; set; } = string.Empty;

    public string ApiBaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models";

    public string Model { get; set; } = "gemini-3.1-flash-lite";

    public int ContextMovieLimit { get; set; } = 100;
}
