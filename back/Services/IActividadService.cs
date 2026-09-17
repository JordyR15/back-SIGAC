using back.DTOs;
using back.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace back.Services
{
    public interface IActividadService
    {
        Task<IEnumerable<ActividadDto>> GetActividadesByClaseAsync(int claseId);
        Task<ActividadDto> GetActividadByIdAsync(int id);
        Task<ActividadDto> CreateActividadAsync(CreateActividadDto createActividadDto);
        Task<ActividadDto> UpdateActividadAsync(int id, ActividadDto actividadDto);
        Task<bool> DeleteActividadAsync(int id);
        Task<bool> EntregarActividadAsync(EntregarActividadDto entregarActividadDto);
        Task<bool> CalificarEntregaAsync(CalificarEntregaDto calificarEntregaDto);
        Task<IEnumerable<EstudianteActividadRealizada>> GetEntregasByActividadAsync(int actividadId);
    }
}
