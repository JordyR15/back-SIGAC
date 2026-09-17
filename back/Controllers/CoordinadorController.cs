using back.Data;
using back.DTOs;
using back.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
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

        [HttpPut("convocatorias/{id}/publicar")]
        public async Task<IActionResult> PublicarConvocatoria(int id)
        {
            var conv = await _context.Convocatorias.FindAsync(id);
            if (conv == null) return NotFound("Convocatoria no encontrada.");

            conv.Estado = "Publicada";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Convocatoria publicada." });
        }

        [HttpGet("ayudantias/solicitudes")]
        public async Task<IActionResult> ObtenerSolicitudesAyudantia()
        {
            var ayudantiasList = await _context.Ayudantias
                .Include(a => a.Estudiante)
                    .ThenInclude(e => e.Persona)
                .Include(a => a.Catedra)
                .Include(a => a.Presentaciones)
                    .ThenInclude(p => p.Jurados)
                        .ThenInclude(j => j.Persona)
                .ToListAsync();

            var solicitudes = ayudantiasList.Select(a => MapToSolicitudDto(a)).ToList();
            return Ok(solicitudes);
        }

        [HttpPost("ayudantias/convocar-tribunal")]
        [HttpPost("ayudantias/{ayudantiaId}/convocar-tribunal")]
        public async Task<IActionResult> ConvocarTribunal(int? ayudantiaId, [FromBody] CreatePresentacionDto dto)
        {
            if (dto == null) dto = new CreatePresentacionDto();

            int targetAyudantiaId = ayudantiaId ?? dto.AyudantiaId;
            int targetEstudianteId = dto.PostulanteId > 0 ? dto.PostulanteId : (dto.EstudianteId ?? 0);

            Ayudantia? ayudantia = null;
            if (targetAyudantiaId > 0)
            {
                ayudantia = await _context.Ayudantias
                    .Include(a => a.Catedra)
                    .Include(a => a.Estudiante)
                        .ThenInclude(e => e.Persona)
                    .Include(a => a.Presentaciones)
                    .FirstOrDefaultAsync(a => a.Id == targetAyudantiaId);
            }

            if (ayudantia == null && targetEstudianteId > 0)
            {
                ayudantia = await _context.Ayudantias
                    .Include(a => a.Catedra)
                    .Include(a => a.Estudiante)
                        .ThenInclude(e => e.Persona)
                    .Include(a => a.Presentaciones)
                    .FirstOrDefaultAsync(a => a.EstudianteId == targetEstudianteId && (dto.CatedraId <= 0 || a.CatedraId == dto.CatedraId));
            }

            if (ayudantia == null)
            {
                if (targetEstudianteId <= 0)
                {
                    return BadRequest(new { message = "Se requiere una postulación o estudiante válido para convocar tribunal." });
                }

                int resolvedCatedraId = dto.CatedraId > 0 ? dto.CatedraId : await _context.Catedras.Select(c => c.Id).FirstOrDefaultAsync();

                ayudantia = new Ayudantia
                {
                    EstudianteId = targetEstudianteId,
                    CatedraId = resolvedCatedraId,
                    Estado = "Convocada",
                    HorasAsignadas = 16
                };

                _context.Ayudantias.Add(ayudantia);
                await _context.SaveChangesAsync();
            }
            else
            {
                ayudantia.Estado = "Convocada";
            }

            // Resolver jurados
            var juradoIds = new List<int>();
            if (dto.JuradoIds != null && dto.JuradoIds.Count > 0) juradoIds.AddRange(dto.JuradoIds);
            if (dto.DocentesIds != null && dto.DocentesIds.Count > 0) juradoIds.AddRange(dto.DocentesIds);
            if (dto.JuradoId > 0) juradoIds.Add(dto.JuradoId);
            if (!string.IsNullOrWhiteSpace(dto.ProfesoresAsignados))
            {
                var parsed = dto.ProfesoresAsignados.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(s => int.TryParse(s, out var id) ? id : 0)
                    .Where(id => id > 0);
                juradoIds.AddRange(parsed);
            }
            juradoIds = juradoIds.Distinct().ToList();

            var jurados = await _context.Users
                .Include(u => u.Persona)
                .Where(u => juradoIds.Contains(u.Id))
                .ToListAsync();

            if (!jurados.Any())
            {
                var fallback = await _context.Users.Include(u => u.Persona)
                    .FirstOrDefaultAsync(u => u.Persona != null && (u.Persona.Rol.Contains("Docente") || u.Persona.Rol.Contains("Jurado")));
                if (fallback != null) jurados.Add(fallback);
            }

            DateTime fecha = dto.FechaPresentacion != default ? dto.FechaPresentacion.ToUniversalTime()
                : (dto.Fecha != default ? dto.Fecha.ToUniversalTime() : DateTime.UtcNow.AddDays(3));

            var presentacion = await _context.Presentaciones.FirstOrDefaultAsync(p => p.AyudantiaId == ayudantia.Id);
            if (presentacion == null)
            {
                presentacion = new Presentacion
                {
                    AyudantiaId = ayudantia.Id,
                    Fecha = fecha,
                    Jurados = jurados,
                    DecanoId = dto.DecanoId,
                    CoordinadorCarreraId = dto.CoordinadorCarreraId
                };
                _context.Presentaciones.Add(presentacion);
            }
            else
            {
                presentacion.Fecha = fecha;
                presentacion.Jurados = jurados;
            }

            await _context.SaveChangesAsync();

            var juradoNombres = jurados.Select(j => j.Persona != null ? $"{j.Persona.Nombre} {j.Persona.Apellido}".Trim() : j.Username).ToList();

            return Ok(new
            {
                success = true,
                message = $"Tribunal convocado exitosamente. Reunión planificada para el {fecha:dd/MM/yyyy HH:mm}.",
                id = presentacion.Id,
                presentacionId = presentacion.Id,
                ayudantiaId = ayudantia.Id,
                fecha = presentacion.Fecha,
                fechaPresentacion = presentacion.Fecha,
                postulanteId = ayudantia.EstudianteId,
                estudianteId = ayudantia.EstudianteId,
                catedraId = ayudantia.CatedraId,
                catedraNombre = ayudantia.Catedra?.Nombre ?? "Cátedra Asignada",
                profesoresAsignados = juradoNombres,
                jurados = juradoNombres,
                estado = "Convocada",
                reunionPlanificada = true,
                estadoTribunal = "Tribunal Convocado - Reunión Planificada",
                mensajeTribunal = $"Tribunal convocado y reunión planificada para el {fecha:dd/MM/yyyy HH:mm}. Jurados: {string.Join(", ", juradoNombres)}"
            });
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
                    .ThenInclude(e => e.Persona)
                .Include(a => a.Catedra)
                .Include(a => a.Presentaciones)
                    .ThenInclude(p => p.Jurados)
                        .ThenInclude(j => j.Persona)
                .ToListAsync();

            var result = ayudantiasActivas.Select(a => MapToSolicitudDto(a)).ToList();
            return Ok(result);
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

            return Ok(reporte);
        }

        private static SolicitudAyudantiaDto MapToSolicitudDto(Ayudantia a)
        {
            var pres = a.Presentaciones?.OrderByDescending(p => p.Fecha).FirstOrDefault();
            var juradoNombres = pres?.Jurados?
                .Select(j => j.Persona != null ? $"{j.Persona.Nombre} {j.Persona.Apellido}".Trim() : j.Username)
                .Where(n => !string.IsNullOrWhiteSpace(n))
                .ToList() ?? new List<string>();

            bool esConvocada = string.Equals(a.Estado, "Convocada", StringComparison.OrdinalIgnoreCase) || pres != null;
            bool reunionPlanificada = pres != null && pres.Fecha != default;

            string estadoTribunal = "Tribunal No Convocado";
            string mensajeTribunal = "Pendiente de convocación a tribunal.";

            if (esConvocada)
            {
                if (reunionPlanificada)
                {
                    estadoTribunal = "Tribunal Convocado - Reunión Planificada";
                    var juradosStr = juradoNombres.Any() ? string.Join(", ", juradoNombres) : "Docentes Asignados";
                    mensajeTribunal = $"Tribunal convocado y reunión planificada para el {pres!.Fecha:dd/MM/yyyy HH:mm}. Jurados: {juradosStr}";
                }
                else
                {
                    estadoTribunal = "Tribunal Convocado - Pendiente Planificar Fecha/Jurados";
                    mensajeTribunal = "El tribunal ha sido convocado en el sistema, pero requiere planificar la fecha o asignar jurados.";
                }
            }

            var correo = a.Estudiante?.Persona?.Correo ?? (a.Estudiante != null ? a.Estudiante.Username + "@uteq.edu.ec" : string.Empty);
            var cedula = a.Estudiante?.Persona?.Cedula ?? string.Empty;

            return new SolicitudAyudantiaDto
            {
                AyudantiaId = a.Id,
                EstudianteId = a.EstudianteId,
                NombreEstudiante = a.Estudiante?.Persona != null
                    ? $"{a.Estudiante.Persona.Nombre} {a.Estudiante.Persona.Apellido}".Trim()
                    : (a.Estudiante?.Username ?? "Postulante"),
                CorreoEstudiante = correo,
                EmailEstudiante = correo,
                CedulaEstudiante = cedula,
                CatedraId = a.CatedraId,
                NombreCatedra = a.Catedra != null ? a.Catedra.Nombre : "Cátedra",
                Estado = !string.IsNullOrEmpty(a.Estado) ? a.Estado : (esConvocada ? "Convocada" : "Pendiente"),
                TieneTribunal = esConvocada,
                PresentacionId = pres?.Id,
                FechaPresentacion = pres?.Fecha,
                ReunionPlanificada = reunionPlanificada,
                Jurados = juradoNombres,
                EstadoTribunal = estadoTribunal,
                MensajeTribunal = mensajeTribunal
            };
        }
    }
}
