using back.Data;
using back.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace back.Controllers
{
    [Authorize] // Idealmente, con un rol de "Coordinador"
    [ApiController]
    [Route("api/[controller]")]
    public class CoordinadorController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CoordinadorController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("ayudantias/solicitudes")]
        public async Task<IActionResult> ObtenerSolicitudesAyudantia()
        {
            var solicitudes = await _context.Ayudantias
                .Where(a => a.Estado == "Pendiente")
                .Include(a => a.Estudiante)
                .Include(a => a.Catedra)
                .Select(a => new SolicitudAyudantiaDto
                {
                    AyudantiaId = a.Id,
                    EstudianteId = a.EstudianteId,
                    NombreEstudiante = a.Estudiante.Username,
                    CatedraId = a.CatedraId,
                    NombreCatedra = a.Catedra.Nombre,
                    Estado = a.Estado
                })
                .ToListAsync();

            return Ok(solicitudes);
        }

        [HttpPost("ayudantias/asignar")]
        public async Task<IActionResult> AsignarAyudante([FromBody] AsignacionAyudantiaDto asignacionDto)
        {
            var ayudantia = await _context.Ayudantias
                .Include(a => a.Catedra)
                .FirstOrDefaultAsync(a => a.Id == asignacionDto.AyudantiaId);
            if (ayudantia == null) return NotFound("Solicitud de ayudantía no encontrada.");

            // Validar nota mínima si está establecida en la cátedra
            var catedra = ayudantia.Catedra;
            var inscripcion = await _context.Inscripciones
                .FirstOrDefaultAsync(i => i.EstudianteId == ayudantia.EstudianteId && i.CatedraId == ayudantia.CatedraId);

            if (catedra != null && catedra.MinimoNota.HasValue)
            {
                if (inscripcion == null)
                {
                    return BadRequest(new { message = "No se puede asignar: el estudiante no está inscrito en la cátedra." });
                }

                if (inscripcion.PromedioActual < catedra.MinimoNota.Value)
                {
                    return BadRequest(new { message = "No se puede asignar: el promedio del estudiante es inferior a la nota mínima establecida." });
                }
            }

            ayudantia.Estado = "Activa";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Ayudante asignado exitosamente." });
        }

        [HttpPut("catedras/{catedraId}/minimo-nota")]
        public async Task<IActionResult> SetMinimoNota(int catedraId, [FromBody] SetMinimoNotaDto dto)
        {
            var catedra = await _context.Catedras.FindAsync(catedraId);
            if (catedra == null) return NotFound("Cátedra no encontrada.");

            catedra.MinimoNota = dto.MinimoNota;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Nota mínima para la cátedra {catedraId} actualizada a {dto.MinimoNota}." });
        }

        [HttpGet("ayudantias/seguimiento")]
        public async Task<IActionResult> SeguimientoInstitucionalAyudantias()
        {
            var ayudantiasActivas = await _context.Ayudantias
                .Include(a => a.Estudiante)
                .Include(a => a.Catedra)
                .Select(a => new SolicitudAyudantiaDto // Reutilizamos el DTO
                {
                    AyudantiaId = a.Id,
                    EstudianteId = a.EstudianteId,
                    NombreEstudiante = a.Estudiante.Username,
                    CatedraId = a.CatedraId,
                    NombreCatedra = a.Catedra.Nombre,
                    Estado = a.Estado
                })
                .ToListAsync();

            return Ok(ayudantiasActivas);
        }

        [HttpPut("ayudantias/{ayudantiaId}/estado")]
        public async Task<IActionResult> GestionarEstadoAyudantia(int ayudantiaId, [FromBody] GestionEstadoAyudantiaDto estadoDto)
        {
            var ayudantia = await _context.Ayudantias.FindAsync(ayudantiaId);
            if (ayudantia == null) return NotFound("Ayudantía no encontrada.");

            ayudantia.Estado = estadoDto.NuevoEstado;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Estado de la ayudantía actualizado a {estadoDto.NuevoEstado}." });
        }

        [HttpGet("ayudantias/reportes-administrativos")]
        public async Task<IActionResult> GenerarReportesAdministrativos()
        {
            var reporte = await _context.Ayudantias
                .GroupBy(a => a.Estado)
                .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
                .ToListAsync();

            // Este es un ejemplo simple. Se podrían generar reportes más complejos.
            return Ok(reporte);
        }
    }
}
