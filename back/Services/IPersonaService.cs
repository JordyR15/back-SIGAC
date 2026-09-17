using back.DTOs;
using back.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace back.Services
{
    public interface IPersonaService
    {
        Task<PersonaDto> GetPersonaByIdAsync(int id);
        Task<PersonaDto> GetPersonaByCedulaAsync(string cedula);
        Task<IEnumerable<PersonaDto>> GetAllPersonasAsync();
        Task<PersonaDto> CreatePersonaAsync(PersonaDto personaDto);
        Task<PersonaDto> UpdatePersonaAsync(int id, PersonaDto personaDto);
        Task<bool> DeletePersonaAsync(int id);
        Task<ExpedienteDto> GetExpedienteAsync(int personaId);
    }
}
