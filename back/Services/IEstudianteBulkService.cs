using back.DTOs;
using back.Entities;
using System.Threading.Tasks;

namespace back.Services
{
    public interface IEstudianteBulkService
    {
        Task<BulkUploadResultDto> ProcessImportJobAsync(int importJobId);
        Task<ImportJob> CreateImportJobAsync(string filePath, string uploadedBy);
        Task<ImportJob> GetImportJobByIdAsync(int id);
        Task<IEnumerable<ImportJob>> GetAllImportJobsAsync();
        Task<BulkUploadResultDto> ValidateAndImportStudentsAsync(string filePath);
    }
}
