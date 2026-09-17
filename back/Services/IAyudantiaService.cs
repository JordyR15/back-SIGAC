using back.DTOs;
using back.Entities;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace back.Services
{
    public interface IAyudantiaService
    {
        Task<Convocatoria> CreateConvocatoriaAsync(CreateConvocatoriaDto createConvocatoriaDto);
        Task<IEnumerable<Convocatoria>> GetAllConvocatoriasAsync();
        Task<Convocatoria> GetConvocatoriaByIdAsync(int id);
        Task<bool> PostularAyudantiaAsync(PostulacionAyudantiaDto postulacionAyudantiaDto);
        Task<bool> SolicitudAyudantiaAsync(SolicitudAyudantiaDto solicitudAyudantiaDto);
        Task<bool> AsignarAyudantiaAsync(AsignacionAyudantiaDto asignacionAyudantiaDto);
        Task<bool> GestionarEstadoAyudantiaAsync(GestionEstadoAyudantiaDto gestionEstadoAyudantiaDto);
        Task<IEnumerable<HistorialAyudantiaDto>> GetHistorialAyudantiaAsync(int ayudantiaId);
        Task<MonitoreoAyudantiaDto> GetMonitoreoAyudantiaAsync(int ayudantiaId);
        Task<IEnumerable<ActividadAyudantiaDto>> GetActividadesAyudantiaAsync(int ayudantiaId);
        Task<bool> RegistrarBitacoraAsync(RegistroBitacoraDto registroBitacoraDto);
        Task<bool> RegistrarBitacoraMultipartAsync(RegistrarBitacoraMultipartDto registrarBitacoraMultipartDto);
        Task<string> GenerarInformeMensualAsync(InformeMensualRequestDto informeMensualRequestDto);
    }
}
