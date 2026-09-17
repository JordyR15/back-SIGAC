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

            // 1. Postulante
            int pId = dto.PostulanteId > 0 ? dto.PostulanteId : (dto.EstudianteId ?? 0);
            if (pId <= 0 && (ayudantiaId.HasValue && ayudantiaId.Value > 0 || dto.AyudantiaId > 0))
            {
                int targetAyudantiaId = ayudantiaId ?? dto.AyudantiaId;
                pId = await _context.Ayudantias
                    .Where(a => a.Id == targetAyudantiaId)
                    .Select(a => a.EstudianteId)
                    .FirstOrDefaultAsync();
            }
            if (pId <= 0)
            {
                pId = await _context.Ayudantias
                    .OrderByDescending(a => a.Id)
                    .Select(a => a.EstudianteId)
                    .FirstOrDefaultAsync();
            }
            if (pId <= 0)
            {
                pId = await _context.Users.Select(u => u.Id).FirstOrDefaultAsync();
            }

            // 2. Cátedra válida resuelta en Catedras
            int resolvedCatedraId = dto.CatedraId;
            if (!await _context.Catedras.AnyAsync(c => c.Id == resolvedCatedraId))
            {
                var matId = dto.MateriaId ?? dto.CatedraId;
                var mat = matId > 0 ? await _context.Materias.FindAsync(matId) : null;
                var cat = mat != null 
                    ? await _context.Catedras.FirstOrDefaultAsync(c => c.Nombre == mat.Nombre) 
                    : null;
                resolvedCatedraId = cat != null ? cat.Id : await _context.Catedras.Select(c => c.Id).FirstOrDefaultAsync();
            }
            if (resolvedCatedraId <= 0)
            {
                resolvedCatedraId = await _context.Catedras.Select(c => c.Id).FirstOrDefaultAsync();
            }

            // 3. Jurado (Aceptar usuarios con rol Docente, Jurado o Admin)
            int resolvedJuradoId = dto.JuradoId;
            if (resolvedJuradoId <= 0 && dto.DocentesIds != null && dto.DocentesIds.Count > 0)
                resolvedJuradoId = dto.DocentesIds[0];
            
            var juradoIds = new List<int>();
            if (resolvedJuradoId > 0) juradoIds.Add(resolvedJuradoId);
            if (dto.DocentesIds != null && dto.DocentesIds.Count > 0) juradoIds.AddRange(dto.DocentesIds);
            if (dto.JuradoIds != null && dto.JuradoIds.Count > 0) juradoIds.AddRange(dto.JuradoIds);
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
                var fallbackJurado = await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u => u.Persona != null && (u.Persona.Rol.Contains("Docente") || u.Persona.Rol.Contains("Jurado") || u.Persona.Rol.Contains("Administrador")));
                if (fallbackJurado != null) jurados.Add(fallbackJurado);
            }

            // 4. Fecha
            DateTime fecha = dto.FechaPresentacion != default ? dto.FechaPresentacion.ToUniversalTime() 
                : (dto.Fecha != default ? dto.Fecha.ToUniversalTime() : DateTime.UtcNow.AddDays(3));

            // 5. Ayudantía
            var ayudantia = await _context.Ayudantias
                .FirstOrDefaultAsync(a => a.EstudianteId == pId && (resolvedCatedraId <= 0 || a.CatedraId == resolvedCatedraId));

            if (ayudantia == null)
            {
                ayudantia = await _context.Ayudantias.FirstOrDefaultAsync(a => a.EstudianteId == pId);
            }

            if (ayudantia == null)
            {
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

            // 6. Presentación
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
                message = "Tribunal convocado exitosamente.",
                id = presentacion.Id,
                presentacionId = presentacion.Id,
                ayudantiaId = ayudantia.Id,
                postulanteId = pId,
                estudianteId = pId,
                catedraId = resolvedCatedraId,
                fecha = presentacion.Fecha,
                fechaPresentacion = presentacion.Fecha,
                tema = !string.IsNullOrWhiteSpace(dto.Tema) ? dto.Tema : "Defensa de Méritos y Oposición",
                lugar = !string.IsNullOrWhiteSpace(dto.Lugar) ? dto.Lugar : "Aula 204 / Teams UTEQ",
                profesoresAsignados = juradoNombres,
                jurados = juradoNombres,
                estado = "Convocada"
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

        // Registrar una evaluación realizada por un jurado con Rúbrica
        [HttpPost("presentaciones/{presentacionId}/evaluaciones")]
        [Authorize(Roles = "Jurado,Docente,Administrador,Coordinador")]
        public async Task<IActionResult> AddEvaluacion(int presentacionId, [FromBody] CreateEvaluacionDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Datos de evaluación requeridos." });

            var presentacion = await _context.Presentaciones
                .Include(p => p.Ayudantia)
                .FirstOrDefaultAsync(p => p.Id == presentacionId);
            if (presentacion == null) return NotFound(new { message = "Presentación no encontrada." });

            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("id")?.Value
                ?? User.FindFirst("nameid")?.Value;
            
            if (!int.TryParse(userIdClaim, out var juradoId) || juradoId <= 0)
            {
                var juradoUser = await _context.Users.FirstOrDefaultAsync();
                juradoId = juradoUser != null ? juradoUser.Id : 1;
            }

            // Calcular nota individual a partir de la rúbrica si viene presente
            double notaCalculada = dto.Nota;
            if (dto.DominioCientifico.HasValue || dto.DestrezaPedagogica.HasValue || dto.Desenvolvimiento.HasValue)
            {
                double dom = dto.DominioCientifico ?? 0.0;
                double desP = dto.DestrezaPedagogica ?? 0.0;
                double desE = dto.Desenvolvimiento ?? 0.0;

                double suma = dom + desP + desE;
                if (suma > 0 && suma <= 10.0)
                {
                    notaCalculada = suma;
                }
                else if (suma > 10.0)
                {
                    notaCalculada = (dom + desP + desE) / 3.0;
                }
            }

            if (notaCalculada <= 0 && dto.Nota > 0)
            {
                notaCalculada = dto.Nota;
            }

            notaCalculada = Math.Round(notaCalculada, 2);

            var eval = new PresentacionEvaluacion
            {
                PresentacionId = presentacionId,
                JuradoId = juradoId,
                DominioCientifico = dto.DominioCientifico,
                DestrezaPedagogica = dto.DestrezaPedagogica,
                Desenvolvimiento = dto.Desenvolvimiento,
                Nota = notaCalculada,
                Observaciones = dto.Observaciones ?? string.Empty,
                Fecha = DateTime.UtcNow
            };

            _context.PresentacionEvaluaciones.Add(eval);

            // Actualizar estado de la presentación y la ayudantía a "Evaluada"
            if (presentacion.Ayudantia != null)
            {
                presentacion.Ayudantia.Estado = "Evaluada";
            }

            await _context.SaveChangesAsync();

            // Calcular la nota final promedio acumulada de todas las evaluaciones del tribunal
            var todasEvaluaciones = await _context.PresentacionEvaluaciones
                .Where(e => e.PresentacionId == presentacionId)
                .Select(e => e.Nota)
                .ToListAsync();

            double notaFinal = todasEvaluaciones.Any() ? Math.Round(todasEvaluaciones.Average(), 2) : notaCalculada;

            return Ok(new
            {
                success = true,
                notaFinal = notaFinal,
                estado = "Evaluada",
                mensaje = "Calificación con rúbrica registrada exitosamente."
            });
        }

        // Obtener resultado de la presentación (Protegido contra 500 NRE / EF Include)
        [HttpGet("presentaciones/{presentacionId}/resultado")]
        [Authorize(Roles = "Administrador,Coordinador,Jurado,Docente")]
        public async Task<IActionResult> GetResultado(int presentacionId)
        {
            var presentacion = await _context.Presentaciones
                .Include(p => p.Ayudantia)
                    .ThenInclude(a => a.Catedra)
                .Include(p => p.Ayudantia)
                    .ThenInclude(a => a.Estudiante)
                        .ThenInclude(u => u.Persona)
                .FirstOrDefaultAsync(p => p.Id == presentacionId);

            if (presentacion == null) return NotFound(new { message = "Presentación no encontrada." });

            var evaluaciones = await _context.PresentacionEvaluaciones
                .Include(e => e.Jurado)
                    .ThenInclude(u => u.Persona)
                .Where(e => e.PresentacionId == presentacionId)
                .ToListAsync();

            double promedio = evaluaciones.Any() ? evaluaciones.Average(e => e.Nota) : 0.0;
            promedio = Math.Round(promedio, 2);

            string estudianteNombre = presentacion.Ayudantia?.Estudiante?.Persona != null 
                ? $"{presentacion.Ayudantia.Estudiante.Persona.Nombre} {presentacion.Ayudantia.Estudiante.Persona.Apellido}".Trim()
                : (presentacion.Ayudantia?.Estudiante != null ? presentacion.Ayudantia.Estudiante.Username : "Estudiante");

            string catedraNombre = presentacion.Ayudantia?.Catedra != null 
                ? presentacion.Ayudantia.Catedra.Nombre 
                : "Cátedra";

            string estadoCalculado = promedio >= 7.0 
                ? "Aprobado" 
                : (evaluaciones.Any() ? "Reprobado" : "Pendiente");

            var evalFormatted = evaluaciones.Select(e => new
            {
                juradoNombre = e.Jurado?.Persona != null 
                    ? $"{e.Jurado.Persona.Nombre} {e.Jurado.Persona.Apellido}".Trim() 
                    : (e.Jurado != null ? e.Jurado.Username : "Docente Jurado"),
                nota = e.Nota,
                dominioCientifico = e.DominioCientifico,
                destrezaPedagogica = e.DestrezaPedagogica,
                desenvolvimiento = e.Desenvolvimiento,
                observaciones = !string.IsNullOrWhiteSpace(e.Observaciones) ? e.Observaciones : "Sin observaciones adicionales",
                fecha = e.Fecha
            }).ToList();

            var obsJurados = evaluaciones
                .Where(e => !string.IsNullOrWhiteSpace(e.Observaciones))
                .Select(e => $"{e.Jurado?.Persona?.Nombre ?? e.Jurado?.Username ?? "Jurado"}: {e.Observaciones}")
                .ToList();

            string obsConcat = obsJurados.Any() ? string.Join(" | ", obsJurados) : "Sin observaciones adicionales";

            return Ok(new
            {
                presentacionId = presentacion.Id,
                estudianteNombre = estudianteNombre,
                catedraNombre = catedraNombre,
                promedioFinal = promedio,
                promedioFinalPonderado = promedio,
                promedioEvaluaciones = promedio,
                notaFinal = promedio,
                estado = estadoCalculado,
                estadoFinal = estadoCalculado,
                aprobado = promedio >= 7.0,
                observaciones = obsConcat,
                observacionesJurado = obsJurados,
                evaluaciones = evalFormatted,
                evaluacionesCount = evaluaciones.Count
            });
        }
    }
}
