using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace CinemaSystem.Application.Interfaces;

public interface IFileStorageService
{
    Task<string> SaveFileAsync(Stream fileStream, string fileName, string folderName, CancellationToken cancellationToken);
    Task DeleteFileAsync(string fileUrl, CancellationToken cancellationToken);
}
