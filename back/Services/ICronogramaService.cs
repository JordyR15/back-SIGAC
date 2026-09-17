using back.DTOs;
using back.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace back.Services
{
    public interface ICronogramaService
    {
        Task<IEnumerable<CronogramaActividadDto>> GetCronogramaByEstudianteAsync(int estudianteId);
        Task<IEnumerable<CronogramaActividadDto>> GetCronogramaByClaseAsync(int claseId);
        Task<CronogramaActividadDto> CreateCronogramaActividadAsync(CronogramaActividadDto cronogramaActividadDto);
        Task<CronogramaActividadDto> UpdateCronogramaActividadAsync(int id, CronogramaActividadDto cronogramaActividadDto);
        Task<bool> DeleteCronogramaActividadAsync(int id);
        Task<IEnumerable<HistorialCronograma>> GetHistorialCronogramaAsync(int cronogramaId);
    }
}
