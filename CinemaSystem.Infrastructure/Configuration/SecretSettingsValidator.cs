namespace CinemaSystem.Infrastructure.Configuration;

public static class SecretSettingsValidator
{
    private static readonly string[] PlaceholderPrefixes =
    [
        "CHANGE_ME",
        "REPLACE_",
        "YOUR_"
    ];

    public static bool IsConfigured(string? value, int minimumLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < minimumLength)
        {
            return false;
        }

        return !PlaceholderPrefixes.Any(prefix =>
            value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }
}
