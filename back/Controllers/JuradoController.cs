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

        // Crear una presentación asociada a una postulacion/ayudantía
        // Solo Coordinador (o Decano) debe crear la instancia que luego los jurados evaluarán
        [HttpPost("presentaciones")]
        [Authorize(Roles = "Coordinador")]
        public async Task<IActionResult> CreatePresentacion([FromBody] CreatePresentacionDto dto)
        {
            var ayudantia = await _context.Ayudantias.FindAsync(dto.AyudantiaId);
            if (ayudantia == null) return NotFound("Ayudantía/postulación no encontrada.");

            var juradoIds = dto.JuradoIds?.Any() == true
                ? dto.JuradoIds.Distinct().ToList()
                : (dto.ProfesoresAsignados ?? string.Empty)
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(int.Parse)
                    .Distinct()
                    .ToList();

            var jurados = await _context.Users
                .Where(u => juradoIds.Contains(u.Id))
                .ToListAsync();

            var presentacion = new Presentacion
            {
                AyudantiaId = dto.AyudantiaId,
                Fecha = dto.Fecha,
                Jurados = jurados,
                DecanoId = dto.DecanoId,
                CoordinadorCarreraId = dto.CoordinadorCarreraId
            };

            _context.Presentaciones.Add(presentacion);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Presentación creada.", presentacionId = presentacion.Id });
        }

        // Endpoint para listar presentaciones convocadas (evita el error 405)
        [HttpGet("presentaciones")]
        [Authorize]
        public async Task<IActionResult> GetPresentaciones()
        {
            var presentaciones = await _context.Presentaciones
                .Include(p => p.Ayudantia)
                    .ThenInclude(a => a.Catedra)
                .Include(p => p.Ayudantia)
                    .ThenInclude(a => a.Estudiante)
                        .ThenInclude(e => e.Persona)
                .Include(p => p.Jurados)
                    .ThenInclude(j => j.Persona)
                .Select(p => new
                {
                    p.Id,
                    p.AyudantiaId,
                    p.Fecha,
                    Catedra = p.Ayudantia.Catedra.Nombre,
                    Postulante = p.Ayudantia.Estudiante.Persona.Nombre + " " + p.Ayudantia.Estudiante.Persona.Apellido,
                    Jurados = p.Jurados.Select(j => j.Persona.Nombre + " " + j.Persona.Apellido).ToList()
                })
                .ToListAsync();

            return Ok(presentaciones);
        }

        // Registrar una evaluación realizada por un jurado (rol Jurado)
        [HttpPost("presentaciones/{presentacionId}/evaluaciones")]
        [Authorize(Roles = "Jurado")]
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

            return Ok(new { message = "Evaluación registrada." });
        }

        // Obtener resultado/comparaciones para la presentación
        [HttpGet("presentaciones/{presentacionId}/resultado")]
        [Authorize]
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

            // Promedio del estudiante según Inscripcion
            var inscripcion = await _context.Inscripciones
                .FirstOrDefaultAsync(i => i.EstudianteId == presentacion.Ayudantia.EstudianteId && i.CatedraId == presentacion.Ayudantia.CatedraId);

            var promedioEstudiante = inscripcion?.PromedioActual ?? 0.0;

            // Promedio del semestre (promedio de inscritos en la cátedra)
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