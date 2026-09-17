using back.Data;
using back.DTOs;
using back.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace back.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class JuradoController : ControllerBase
    {
        private readonly AppDbContext _context;

        public JuradoController(AppDbContext context)
        {
            _context = context;
        }

        // Crear o actualizar una presentación asociada a una postulación/ayudantía (convocar tribunal)
        [HttpPost("presentaciones")]
        [HttpPost("/api/ayudantias/presentaciones")]
        [HttpPost("/api/ayudantias/convocar-tribunal")]
        [HttpPost("/api/ayudantias/{ayudantiaId}/convocar-tribunal")]
        [Authorize(Roles = "Administrador,Coordinador,Jurado,Docente")]
        public async Task<IActionResult> CreatePresentacion([FromRoute] int? ayudantiaId, [FromBody] CreatePresentacionDto dto)
        {
            if (dto == null) dto = new CreatePresentacionDto();

            int targetAyudantiaId = ayudantiaId ?? dto.AyudantiaId;
            int pId = dto.PostulanteId > 0 ? dto.PostulanteId : (dto.EstudianteId ?? 0);
            
            Ayudantia? ayudantia = null;
            if (targetAyudantiaId > 0)
            {
                ayudantia = await _context.Ayudantias
                    .Include(a => a.Catedra)
                    .Include(a => a.Estudiante)
                        .ThenInclude(e => e.Persona)
                    .FirstOrDefaultAsync(a => a.Id == targetAyudantiaId);
            }

            if (ayudantia == null && pId > 0)
            {
                ayudantia = await _context.Ayudantias
                    .Include(a => a.Catedra)
                    .Include(a => a.Estudiante)
                        .ThenInclude(e => e.Persona)
                    .FirstOrDefaultAsync(a => a.EstudianteId == pId && (dto.CatedraId <= 0 || a.CatedraId == dto.CatedraId));
            }

            if (ayudantia == null)
            {
                if (pId <= 0 && targetAyudantiaId <= 0)
                {
                    return BadRequest(new { message = "Se requiere un postulante o postulación válida." });
                }

                int resolvedCatedraId = dto.CatedraId;
                if (!await _context.Catedras.AnyAsync(c => c.Id == resolvedCatedraId))
                {
                    var matId = dto.MateriaId ?? dto.CatedraId;
                    var mat = matId > 0 ? await _context.Materias.FindAsync(matId) : null;
                    var cat = mat != null ? await _context.Catedras.FirstOrDefaultAsync(c => c.Nombre == mat.Nombre) : null;
                    resolvedCatedraId = cat != null ? cat.Id : await _context.Catedras.Select(c => c.Id).FirstOrDefaultAsync();
                }

                if (resolvedCatedraId <= 0)
                {
                    resolvedCatedraId = await _context.Catedras.Select(c => c.Id).FirstOrDefaultAsync();
                }

                ayudantia = new Ayudantia
                {
                    EstudianteId = pId,
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
                var fallbackJurado = await _context.Users.Include(u => u.Persona).FirstOrDefaultAsync(u => u.Persona != null && (u.Persona.Rol.Contains("Docente") || u.Persona.Rol.Contains("Jurado")));
                if (fallbackJurado != null) jurados.Add(fallbackJurado);
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
                ayudantiaId = presentacion.AyudantiaId,
                fecha = presentacion.Fecha,
                fechaPresentacion = presentacion.Fecha,
                postulanteId = ayudantia.EstudianteId,
                estudianteId = ayudantia.EstudianteId,
                catedraId = ayudantia.CatedraId,
                materiaId = ayudantia.CatedraId,
                catedraNombre = ayudantia.Catedra?.Nombre ?? "Cátedra Asignada",
                profesoresAsignados = juradoNombres,
                jurados = juradoNombres,
                estado = "Convocada",
                reunionPlanificada = true,
                estadoTribunal = "Tribunal Convocado - Reunión Planificada",
                mensajeTribunal = $"Tribunal convocado y reunión planificada para el {fecha:dd/MM/yyyy HH:mm}. Jurados: {string.Join(", ", juradoNombres)}"
            });
        }

        // Endpoint para listar presentaciones convocadas
        [HttpGet("presentaciones")]
        [Authorize(Roles = "Administrador,Coordinador,Jurado,Docente")]
        public async Task<IActionResult> GetPresentaciones()
        {
            var query = _context.Presentaciones
                .Include(p => p.Ayudantia)
                    .ThenInclude(a => a.Catedra)
                .Include(p => p.Ayudantia)
                    .ThenInclude(a => a.Estudiante)
                        .ThenInclude(e => e.Persona)
                .Include(p => p.Jurados)
                    .ThenInclude(j => j.Persona)
                .AsQueryable();

            bool esAdminOCoord = User.IsInRole("Administrador") || User.IsInRole("Coordinador") 
                || User.Claims.Any(c => c.Value == "Administrador" || c.Value == "Coordinador");

            if (!esAdminOCoord)
            {
                var userIdClaim = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier || c.Type == "id" || c.Type == "nameid")?.Value;
                if (int.TryParse(userIdClaim, out int currentUserId) && currentUserId > 0)
                {
                    query = query.Where(p => p.Jurados.Any(j => j.Id == currentUserId));
                }
            }

            var list = await query.OrderByDescending(p => p.Fecha).ToListAsync();

            var result = list.Select(p =>
            {
                var juradoNombres = p.Jurados.Select(j => j.Persona != null ? $"{j.Persona.Nombre} {j.Persona.Apellido}".Trim() : j.Username).ToList();
                var estadoActual = p.Ayudantia != null && !string.IsNullOrEmpty(p.Ayudantia.Estado) ? p.Ayudantia.Estado : "Convocada";

                return new
                {
                    id = p.Id,
                    presentacionId = p.Id,
                    ayudantiaId = p.AyudantiaId,
                    fecha = p.Fecha,
                    fechaPresentacion = p.Fecha,
                    postulanteId = p.Ayudantia != null ? p.Ayudantia.EstudianteId : 0,
                    estudianteId = p.Ayudantia != null ? p.Ayudantia.EstudianteId : 0,
                    estudianteNombre = p.Ayudantia?.Estudiante?.Persona != null 
                        ? $"{p.Ayudantia.Estudiante.Persona.Nombre} {p.Ayudantia.Estudiante.Persona.Apellido}".Trim()
                        : (p.Ayudantia?.Estudiante != null ? p.Ayudantia.Estudiante.Username : "Postulante"),
                    estudianteCorreo = p.Ayudantia?.Estudiante?.Persona != null 
                        ? p.Ayudantia.Estudiante.Persona.Correo 
                        : (p.Ayudantia?.Estudiante != null ? p.Ayudantia.Estudiante.Username + "@uteq.edu.ec" : "estudiante@uteq.edu.ec"),
                    catedraId = p.Ayudantia != null ? p.Ayudantia.CatedraId : 0,
                    materiaId = p.Ayudantia != null ? p.Ayudantia.CatedraId : 0,
                    catedraNombre = p.Ayudantia?.Catedra != null ? p.Ayudantia.Catedra.Nombre : "Cátedra de Ayudantía",
                    profesoresAsignados = juradoNombres,
                    jurados = juradoNombres,
                    juradoNombre = juradoNombres.FirstOrDefault() ?? "Docente Jurado",
                    temaSilabo = "Evaluación de Destrezas Pedagógicas y Conocimientos en la Cátedra",
                    lugarOEnlace = "Aula Magna / Enlace Virtual Teams UTEQ",
                    estado = estadoActual,
                    reunionPlanificada = true,
                    estadoTribunal = "Tribunal Convocado - Reunión Planificada",
                    mensajeTribunal = $"Tribunal convocado y reunión planificada para el {p.Fecha:dd/MM/yyyy HH:mm}. Jurados: {string.Join(", ", juradoNombres)}"
                };
            });

            return Ok(result);
        }

        // Registrar una evaluación realizada por un jurado
        [HttpPost("presentaciones/{presentacionId}/evaluaciones")]
        [Authorize(Roles = "Administrador,Coordinador,Jurado,Docente")]
        public async Task<IActionResult> AddEvaluacion(int presentacionId, [FromBody] CreateEvaluacionDto dto)
        {
            var presentacion = await _context.Presentaciones
                .Include(p => p.Ayudantia)
                .FirstOrDefaultAsync(p => p.Id == presentacionId);
            if (presentacion == null) return NotFound("Presentación no encontrada.");

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdClaim, out var juradoId))
            {
                return Unauthorized("No se pudo identificar al jurado autenticado.");
            }

            var eval = new PresentacionEvaluacion
            {
                PresentacionId = presentacionId,
                JuradoId = juradoId,
                Nota = dto.Nota,
                Observaciones = dto.Observaciones,
                Fecha = DateTime.UtcNow
            };

            _context.PresentacionEvaluaciones.Add(eval);
            await _context.SaveChangesAsync();

            // Recalcular promedio y, si aprueba, marcar la postulación/ayudantía como aprobada y asignar rol
            var presentacionConEvaluaciones = await _context.Presentaciones
                .Include(p => p.Evaluaciones)
                .Include(p => p.Ayudantia)
                    .ThenInclude(a => a.Estudiante)
                        .ThenInclude(u => u.Persona)
                .FirstOrDefaultAsync(p => p.Id == presentacionId);

            if (presentacionConEvaluaciones != null)
            {
                var todas = presentacionConEvaluaciones.Evaluaciones.Select(e => e.Nota).ToList();
                if (todas.Any())
                {
                    var promedio = todas.Average();
                    if (promedio >= 7.0)
                    {
                        var ayud = presentacionConEvaluaciones.Ayudantia;
                        if (ayud != null)
                        {
                            ayud.Estado = "Aprobada";

                            // Asignar rol Ayudante al estudiante si no lo tiene
                            var estudiante = ayud.Estudiante;
                            if (estudiante != null)
                            {
                                var persona = estudiante.Persona ?? await _context.Personas.FirstOrDefaultAsync(p => p.UserId == estudiante.Id);
                                if (persona != null)
                                {
                                    var roles = persona.GetRoles();
                                    if (!roles.Contains("Ayudante"))
                                    {
                                        roles.Add("Ayudante");
                                        persona.SetRoles(roles);
                                    }
                                }
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
            }

            return Ok(new { success = true, message = "Evaluación registrada exitosamente." });
        }

        // Obtener resultado/comparaciones para la presentación
        [HttpGet("presentaciones/{presentacionId}/resultado")]
        [Authorize(Roles = "Administrador,Coordinador,Jurado,Docente")]
        public async Task<IActionResult> GetResultado(int presentacionId)
        {
            var presentacion = await _context.Presentaciones
                .Include(p => p.Evaluaciones)
                .Include(p => p.Ayudantia)
                .ThenInclude(a => a.Catedra)
                .FirstOrDefaultAsync(p => p.Id == presentacionId);

            if (presentacion == null) return NotFound("Presentación no encontrada.");

            var evaluaciones = presentacion.Evaluaciones;
            var promedioEvaluaciones = evaluaciones.Any() ? evaluaciones.Average(e => e.Nota) : 0.0;

            var inscripcion = await _context.Inscripciones
                .FirstOrDefaultAsync(i => i.EstudianteId == presentacion.Ayudantia.EstudianteId && i.CatedraId == presentacion.Ayudantia.CatedraId);

            var promedioEstudiante = inscripcion?.PromedioActual ?? 0.0;

            var catedraId = presentacion.Ayudantia.CatedraId;
            var inscritos = _context.Inscripciones.Where(i => i.CatedraId == catedraId);
            var promedioCatedra = inscritos.Any() ? await inscritos.AverageAsync(i => i.PromedioActual) : 0.0;

            var dto = new PresentacionResultadoDto
            {
                PromedioEvaluaciones = Math.Round(promedioEvaluaciones, 2),
                PromedioEstudiante = Math.Round(promedioEstudiante, 2),
                PromedioCatedra = Math.Round(promedioCatedra, 2),
                EstudianteSuperiorPromedioCatedra = promedioEstudiante >= promedioCatedra,
                EstudianteSuperiorPromedioPresentacion = promedioEstudiante >= promedioEvaluaciones,
                EvaluacionesCount = evaluaciones.Count
            };

            return Ok(dto);
        }
    }
}
