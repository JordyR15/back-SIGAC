using back.DTOs;
using back.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace back.Services
{
    public interface IAsistenciaService
    {
        Task<IEnumerable<AsistenciaDto>> GetAsistenciasBySesionAsync(int sesionId);
        Task<AsistenciaDto> GetAsistenciaByEstudianteSesionAsync(int estudianteId, int sesionId);
        Task<AsistenciaDto> CreateAsistenciaAsync(CreateAsistenciaDto createAsistenciaDto);
        Task<AsistenciaDto> UpdateAsistenciaAsync(int id, AsistenciaDto asistenciaDto);
        Task<bool> DeleteAsistenciaAsync(int id);
        Task<bool> RegistrarAsistenciaLoteAsync(IEnumerable<CreateAsistenciaDto> asistencias);
        Task<double> CalcularPorcentajeAsistenciaAsync(int estudianteId, int claseId);
    }
}
