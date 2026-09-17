using back.DTOs;
using back.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace back.Services
{
    public interface IJuradoService
    {
        Task<Presentacion> CreatePresentacionAsync(CreatePresentacionDto createPresentacionDto);
        Task<IEnumerable<Presentacion>> GetPresentacionesByConvocatoriaAsync(int convocatoriaId);
        Task<Presentacion> GetPresentacionByIdAsync(int id);
        Task<PresentacionResultadoDto> CalificarPresentacionAsync(int presentacionId, int juradoId, decimal calificacion, string? comentarios);
        Task<IEnumerable<PresentacionResultadoDto>> GetResultadosPresentacionAsync(int presentacionId);
    }
}
