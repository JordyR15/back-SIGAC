using back.DTOs;
using back.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace back.Services
{
    public interface IMateriaService
    {
        Task<IEnumerable<MateriaDto>> GetAllMateriasAsync();
        Task<MateriaDto> GetMateriaByIdAsync(int id);
        Task<MateriaDto> CreateMateriaAsync(MateriaDto materiaDto);
        Task<MateriaDto> UpdateMateriaAsync(int id, MateriaDto materiaDto);
        Task<bool> DeleteMateriaAsync(int id);
        Task<IEnumerable<CatedraResumenDto>> GetCatedrasByMateriaAsync(int materiaId);
        Task<IEnumerable<TemaDto>> GetTemasByCatedraAsync(int catedraId);
        Task<IEnumerable<RecursoDto>> GetRecursosByTemaAsync(int temaId);
        Task<RecursoDto> CreateRecursoAsync(CreateRecursoDto createRecursoDto);
        Task<bool> MarkRecursoAsSeenAsync(MarkRecursoAsSeenDto markRecursoAsSeenDto);
        Task<IEnumerable<RecursoConEstadoDto>> GetRecursosConEstadoAsync(int estudianteId, int temaId);
    }
}
