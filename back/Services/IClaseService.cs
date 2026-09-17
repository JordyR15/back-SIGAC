using back.DTOs;
using back.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace back.Services
{
    public interface IClaseService
    {
        Task<IEnumerable<ClaseDto>> GetAllClasesAsync();
        Task<ClaseDto> GetClaseByIdAsync(int id);
        Task<ClaseDto> CreateClaseAsync(CreateClaseDto createClaseDto);
        Task<ClaseDto> UpdateClaseAsync(int id, ClaseDto claseDto);
        Task<bool> DeleteClaseAsync(int id);
        Task<IEnumerable<ClaseSesionDto>> GetSesionesByClaseAsync(int claseId);
        Task<ClaseSesionDto> CreateClaseSesionAsync(CreateClaseSesionDto createClaseSesionDto);
        Task<ClaseSesionDto> UpdateClaseSesionAsync(int id, ClaseSesionDto claseSesionDto);
        Task<bool> DeleteClaseSesionAsync(int id);
        Task<bool> InscribirEstudianteClaseAsync(InscribirEstudianteClaseDto inscribirEstudianteClaseDto);
        Task<bool> InscribirEstudiantesMasivosAsync(IEnumerable<InscribirEstudianteClaseDto> inscripciones);
    }
}
