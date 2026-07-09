using CinemaSystem.Application.Interfaces;
using CinemaSystem.Application.Settings;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace CinemaSystem.Infrastructure.Services;

public sealed class LocalFileStorageService : IFileStorageService
{
    private readonly string _webRootPath;
    private readonly FileStorageSettings _settings;

    public LocalFileStorageService(
        IHostEnvironment environment,
        IOptions<FileStorageSettings> options)
    {
        _settings = options.Value;
        _webRootPath = Path.GetFullPath(
            Path.Combine(environment.ContentRootPath, _settings.WebRootFolder));
    }

    public async Task<string> SaveFileAsync(
        Stream fileStream,
        string fileName,
        string folderName,
        CancellationToken cancellationToken)
    {
        if (fileStream is null || fileStream.Length == 0)
        {
            return string.Empty;
        }

        var normalizedFolder = NormalizeFolderName(folderName);
        var uploadsFolder = Path.Combine(
            _webRootPath,
            _settings.UploadRootFolder,
            normalizedFolder);
        Directory.CreateDirectory(uploadsFolder);

        var uniqueFileName = $"{Guid.NewGuid():N}{Path.GetExtension(fileName)}";
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);
        await using var outputStream = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await fileStream.CopyToAsync(outputStream, cancellationToken);

        return $"/{_settings.UploadRootFolder}/{normalizedFolder}/{uniqueFileName}";
    }

    public Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
        {
            return Task.CompletedTask;
        }

        var relativePath = fileUrl
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);
        var filePath = Path.GetFullPath(Path.Combine(_webRootPath, relativePath));
        var allowedRoot = _webRootPath.TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

        if (!filePath.StartsWith(allowedRoot, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The requested file path is outside the configured web root.");
        }

        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    private static string NormalizeFolderName(string folderName)
    {
        var normalized = folderName.Trim().Trim('/', '\\');
        if (string.IsNullOrWhiteSpace(normalized)
            || normalized.Contains("..", StringComparison.Ordinal)
            || normalized.IndexOfAny([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar]) >= 0)
        {
            throw new ArgumentException("Invalid storage folder.", nameof(folderName));
        }

        return normalized;
    }
}
