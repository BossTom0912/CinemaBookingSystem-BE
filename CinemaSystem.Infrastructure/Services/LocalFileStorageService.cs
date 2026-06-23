using CinemaSystem.Application.Interfaces;

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Infrastructure.Services;

public class LocalFileStorageService : IFileStorageService
{
    public LocalFileStorageService()
    {
    }

    public async Task<string> SaveFileAsync(Stream fileStream, string fileName, string folderName, CancellationToken cancellationToken)
    {
        if (fileStream == null || fileStream.Length == 0)
            return string.Empty;

        var wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var uploadsFolder = Path.Combine(wwwRootPath, "uploads", folderName);
        
        if (!Directory.Exists(uploadsFolder))
        {
            Directory.CreateDirectory(uploadsFolder);
        }

        var uniqueFileName = Guid.NewGuid().ToString("N") + Path.GetExtension(fileName);
        var filePath = Path.Combine(uploadsFolder, uniqueFileName);

        using (var outputStream = new FileStream(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(outputStream, cancellationToken);
        }

        // Return relative URL
        return $"/uploads/{folderName}/{uniqueFileName}";
    }

    public Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(fileUrl))
            return Task.CompletedTask;

        var wwwRootPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        // Convert URL to physical path. Example URL: /uploads/posters/abc.jpg
        var filePath = Path.Combine(wwwRootPath, fileUrl.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
        
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }
}
