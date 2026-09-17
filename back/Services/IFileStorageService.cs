using System.IO;
using System.Threading.Tasks;

namespace back.Services
{
    public interface IFileStorageService
    {
        Task<string> SaveFileAsync(Stream fileStream, string fileName, string contentType, string folderPath);
        Task<Stream> GetFileAsync(string filePath);
        Task<bool> DeleteFileAsync(string filePath);
        Task<bool> FileExistsAsync(string filePath);
        Task<string> GenerateUniqueFileNameAsync(string originalFileName);
    }
}
