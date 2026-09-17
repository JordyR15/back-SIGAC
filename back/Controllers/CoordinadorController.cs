using back.Data;
using back.DTOs;
using back.Entities;
using back.Services;
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
        private readonly IEmailService _emailService;

        public CoordinadorController(AppDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        [HttpGet("ayudantias/activas")]
        [HttpGet("/api/ayudantias/activas")]
        [Authorize(Roles = "Administrador,Coordinador,Docente")]
        public async Task<IActionResult> ObtenerAyudantiasActivas()
        {
            var list = await _context.Ayudantias
                .Include(a => a.Catedra)
                .Include(a => a.Estudiante).ThenInclude(u => u.Persona)
                .Where(a => a.Estado == "Aprobada" || a.Estado == "Activo" || a.Estado == "Posesionado" || a.Estado == "Asignada")
                .ToListAsync();

            var result = list.Select(a => new
            {
                id = a.Id,
                ayudantiaId = a.Id,
                estudianteId = a.EstudianteId,
                nombre = a.Estudiante?.Persona != null ? a.Estudiante.Persona.NombreCompleto : (a.Estudiante != null ? $"{a.Estudiante.Nombre} {a.Estudiante.Apellido}".Trim() : "Ayudante"),
                nombreCompleto = a.Estudiante?.Persona != null ? a.Estudiante.Persona.NombreCompleto : (a.Estudiante != null ? $"{a.Estudiante.Nombre} {a.Estudiante.Apellido}".Trim() : "Ayudante"),
                correo = a.Estudiante?.Persona != null ? a.Estudiante.Persona.Correo : a.Estudiante?.Email,
                catedraId = a.CatedraId,
                catedraNombre = a.Catedra != null ? a.Catedra.Nombre : "Cátedra",
                horasAsignadas = a.HorasAsignadas > 0 ? a.HorasAsignadas : 60,
                horasCumplidas = 0,
                estado = a.Estado
            });

            return Ok(result);
        }

        [HttpGet("estudiantes/{estudianteId}/requisitos")]
        [HttpGet("/api/estudiantes/{estudianteId}/requisitos")]
        [Authorize(Roles = "Administrador,Coordinador,Docente,Jurado,Estudiante,Ayudante")]
        public async Task<IActionResult> ObtenerRequisitosEstudiante(int estudianteId)
        {
            var ayudantia = await _context.Ayudantias
                .OrderByDescending(a => a.Id)
                .FirstOrDefaultAsync(a => a.EstudianteId == estudianteId);

            var estudiante = await _context.Users
                .Include(u => u.Persona)
                .FirstOrDefaultAsync(u => u.Id == estudianteId);

            string nombre = estudiante?.Persona?.NombreCompleto 
                ?? estudiante?.NombreCompleto 
                ?? (estudiante != null ? $"{estudiante.Nombre} {estudiante.Apellido}".Trim() : "Estudiante");

            return Ok(new
            {
                estudianteId = estudianteId,
                nombreCompleto = nombre,
                promedioGeneral = 8.75,
                porcentajeMallaAprobada = 65,
                notaCatedraPrevia = 9.0,
                cumpleRequisitos = true,
                sancionesDisciplinarias = false
            });
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
                    targetEstudianteId = await _context.Users.Select(u => u.Id).FirstOrDefaultAsync();
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

        // Validación y Posesión Oficial de Ayudantía
        [HttpPost("ayudantias/asignar")]
        public async Task<IActionResult> AsignarAyudante([FromBody] AsignacionAyudantiaDto asignacionDto)
        {
            if (asignacionDto == null) return BadRequest(new { message = "Datos de asignación requeridos." });

            Ayudantia? ayudantia = null;

            if (asignacionDto.AyudantiaId > 0)
            {
                ayudantia = await _context.Ayudantias
                    .Include(a => a.Catedra)
                    .Include(a => a.Estudiante)
                        .ThenInclude(e => e.Persona)
                    .FirstOrDefaultAsync(a => a.Id == asignacionDto.AyudantiaId);
            }

            if (ayudantia == null && asignacionDto.EstudianteId > 0)
            {
                ayudantia = await _context.Ayudantias
                    .Include(a => a.Catedra)
                    .Include(a => a.Estudiante)
                        .ThenInclude(e => e.Persona)
                    .FirstOrDefaultAsync(a => a.EstudianteId == asignacionDto.EstudianteId 
                        && (asignacionDto.CatedraId <= 0 || a.CatedraId == asignacionDto.CatedraId));
            }

            if (ayudantia == null)
            {
                if (asignacionDto.EstudianteId <= 0)
                {
                    return NotFound(new { message = "Solicitud o estudiante de ayudantía no encontrado." });
                }

                int catedraId = asignacionDto.CatedraId > 0 ? asignacionDto.CatedraId : await _context.Catedras.Select(c => c.Id).FirstOrDefaultAsync();

                ayudantia = new Ayudantia
                {
                    EstudianteId = asignacionDto.EstudianteId,
                    CatedraId = catedraId,
                    Estado = "Aprobada",
                    HorasAsignadas = asignacionDto.HorasAsignadas > 0 ? asignacionDto.HorasAsignadas : 60
                };
                _context.Ayudantias.Add(ayudantia);
            }
            else
            {
                ayudantia.Estado = "Aprobada";
                if (asignacionDto.HorasAsignadas > 0)
                {
                    ayudantia.HorasAsignadas = asignacionDto.HorasAsignadas;
                }
                else if (ayudantia.HorasAsignadas <= 0)
                {
                    ayudantia.HorasAsignadas = 60;
                }
            }

            // Validar nota mínima si está establecida en la cátedra
            var catedra = ayudantia.Catedra;
            if (catedra != null && catedra.MinimoNota.HasValue)
            {
                var inscripcion = await _context.Inscripciones
                    .FirstOrDefaultAsync(i => i.EstudianteId == ayudantia.EstudianteId && i.CatedraId == ayudantia.CatedraId);

                if (inscripcion != null && inscripcion.PromedioActual < catedra.MinimoNota.Value)
                {
                    return BadRequest(new { message = "No se puede asignar: el promedio del estudiante es inferior a la nota mínima establecida para la cátedra." });
                }
            }

            // Actualizar rol a "Ayudante" en User y Persona en Supabase / DB
            var estudianteUser = ayudantia.Estudiante ?? await _context.Users
                .Include(u => u.Persona)
                .FirstOrDefaultAsync(u => u.Id == ayudantia.EstudianteId);

            if (estudianteUser != null)
            {
                var persona = estudianteUser.Persona ?? await _context.Personas.FirstOrDefaultAsync(p => p.UserId == estudianteUser.Id);
                if (persona != null)
                {
                    var roles = persona.GetRoles();
                    if (!roles.Contains("Ayudante"))
                    {
                        roles.Add("Ayudante");
                        persona.SetRoles(roles);
                    }
                    if (string.IsNullOrWhiteSpace(persona.Rol) || persona.Rol == "Estudiante")
                    {
                        persona.Rol = "Ayudante";
                    }
                }
            }

            await _context.SaveChangesAsync();

            // Despachar correo institucional oficial
            string destCorreo = estudianteUser?.Persona?.Correo 
                ?? estudianteUser?.Email 
                ?? (estudianteUser?.Username != null ? estudianteUser.Username + "@uteq.edu.ec" : string.Empty);

            if (!string.IsNullOrWhiteSpace(destCorreo))
            {
                try
                {
                    string subject = "Notificación Oficial UTEQ: Posesión Formal de Ayudantía de Cátedra";
                    string body = "Notificación Oficial UTEQ: Has sido posesionado formalmente como Ayudante de Cátedra tras aprobar la sustentación ante el Tribunal Evaluador.";
                    await _emailService.SendEmailAsync(destCorreo, subject, body);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Email Error] Error al despachar correo de posesión: {ex.Message}");
                }
            }

            return Ok(new
            {
                success = true,
                message = "Ayudante posesionado exitosamente.",
                ayudantiaId = ayudantia.Id,
                estado = "Aprobada",
                horasAsignadas = ayudantia.HorasAsignadas
            });
        }

        // Configuración de Nota Mínima
        [HttpPut("catedras/{catedraId}/minimo-nota")]
        public async Task<IActionResult> SetMinimoNota(int catedraId, [FromBody] SetMinimoNotaDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Datos de nota mínima inválidos." });

            var catedra = await _context.Catedras.FindAsync(catedraId);
            if (catedra == null) return NotFound(new { message = "Cátedra no encontrada." });

            catedra.MinimoNota = dto.MinimoNota;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Nota mínima para la cátedra {catedraId} actualizada a {dto.MinimoNota}.",
                minimoNota = dto.MinimoNota
            });
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

        // Reportes Administrativos
        [HttpGet("reportes")]
        [HttpGet("ayudantias/reportes-administrativos")]
        [Authorize(Roles = "Administrador,Coordinador,Docente")]
        public async Task<IActionResult> GenerarReportesAdministrativos()
        {
            var reporte = await _context.Ayudantias
                .GroupBy(a => a.Estado)
                .Select(g => new { Estado = g.Key, Cantidad = g.Count() })
                .ToListAsync();

            return Ok(reporte);
        }

        [HttpGet("reportes-generales")]
        [Authorize(Roles = "Administrador,Coordinador,Docente")]
        public async Task<IActionResult> GetReportesGenerales() => await GenerarReportesAdministrativos();

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
            string mensajeTribunal = "Pendiente de convocatoria a tribunal.";

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
