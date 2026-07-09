namespace CinemaSystem.Application.Settings;

public sealed class FileStorageSettings
{
    public const string SectionName = "FileStorageSettings";

    public string WebRootFolder { get; set; } = "wwwroot";

    public string UploadRootFolder { get; set; } = "uploads";

    public string PosterFolder { get; set; } = "posters";

    public string GeneralImageFolder { get; set; } = "images";

    public string[] AllowedImageExtensions { get; set; } =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".gif",
        ".webp"
    ];
}
