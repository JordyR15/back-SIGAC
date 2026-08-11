using back.Data;
using back.DTOs;
using back.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace back.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class MateriaController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MateriaController(AppDbContext context)
        {
            _context = context;
        }

        // Propiedad para obtener de forma segura el ID del usuario autenticado
        private int? UserId
        {
            get
            {
                var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return int.TryParse(value, out var id) ? id : (int?)null;
            }
        }

        // Helper para verificar si el usuario es docente de la materia
        private async Task<bool> IsDocenteOfMateria(int materiaId)
        {
            if (UserId == null) return false;

            var materia = await _context.Catedras.AsNoTracking()
                                .FirstOrDefaultAsync(m => m.Id == materiaId && m.DocenteId == UserId.Value);
            return materia != null;
        }

        // Endpoint para añadir un recurso a una materia (solo docentes)
        [HttpPost("{materiaId}/recursos")]
        public async Task<IActionResult> AddRecurso(int materiaId, [FromBody] CreateRecursoDto createRecursoDto)
        {
            if (UserId == null) return Unauthorized();
            if (!await IsDocenteOfMateria(materiaId)) return Forbid("Solo el docente responsable puede añadir recursos a esta materia.");

            var materia = await _context.Catedras.FindAsync(materiaId);
            if (materia == null) return NotFound(new { message = "Materia no encontrada." });

            var recurso = new Recurso
            {
                Titulo = createRecursoDto.Titulo,
                Descripcion = createRecursoDto.Descripcion,
                Url = createRecursoDto.Url,
                EsEsencial = createRecursoDto.EsEsencial,
                MateriaId = materiaId
            };

            _context.Recursos.Add(recurso);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRecursosByMateria), new { materiaId = materiaId }, new RecursoDto
            {
                Id = recurso.Id,
                Titulo = recurso.Titulo,
                Descripcion = recurso.Descripcion,
                Url = recurso.Url,
                EsEsencial = recurso.EsEsencial,
                MateriaId = recurso.MateriaId
            });
        }

        // Endpoint para obtener todos los recursos de una materia (todos los usuarios autorizados)
        [HttpGet("{materiaId}/recursos")]
        public async Task<ActionResult<IEnumerable<RecursoDto>>> GetRecursosByMateria(int materiaId)
        {
            if (UserId == null) return Unauthorized();

            var recursos = await _context.Recursos
                                .Where(r => r.MateriaId == materiaId)
                                .Select(r => new RecursoDto
                                {
                                    Id = r.Id,
                                    Titulo = r.Titulo,
                                    Descripcion = r.Descripcion,
                                    Url = r.Url,
                                    EsEsencial = r.EsEsencial,
                                    MateriaId = r.MateriaId
                                })
                                .ToListAsync();

            if (!recursos.Any()) return NotFound(new { message = "No se encontraron recursos para esta materia." });

            return Ok(recursos);
        }

        // Endpoint para añadir una actividad a una materia (solo docentes)
        [HttpPost("{materiaId}/actividades")]
        public async Task<IActionResult> AddActividad(int materiaId, [FromBody] CreateActividadDto createActividadDto)
        {
            if (UserId == null) return Unauthorized();
            if (!await IsDocenteOfMateria(materiaId)) return Forbid("Solo el docente responsable puede añadir actividades a esta materia.");

            var materia = await _context.Catedras.FindAsync(materiaId);
            if (materia == null) return NotFound(new { message = "Materia no encontrada." });

            var actividad = new Actividad
            {
                Titulo = createActividadDto.Titulo,
                Descripcion = createActividadDto.Descripcion,
                FechaEntrega = createActividadDto.FechaEntrega,
                Tipo = createActividadDto.Tipo,
                Estado = "Pendiente", // Estado inicial por defecto
                MateriaId = materiaId
            };

            _context.Actividades.Add(actividad);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetActividadesByMateria), new { materiaId = materiaId }, new ActividadDto
            {
                Id = actividad.Id,
                Titulo = actividad.Titulo,
                Descripcion = actividad.Descripcion,
                FechaEntrega = actividad.FechaEntrega,
                Tipo = actividad.Tipo,
                Estado = actividad.Estado,
                MateriaId = actividad.MateriaId
            });
        }

        // Endpoint para obtener todas las actividades de una materia (todos los usuarios autorizados)
        [HttpGet("{materiaId}/actividades")]
        public async Task<ActionResult<IEnumerable<ActividadDto>>> GetActividadesByMateria(int materiaId)
        {
            if (UserId == null) return Unauthorized();

            var actividades = await _context.Actividades
                                .Where(a => a.MateriaId == materiaId)
                                .Select(a => new ActividadDto
                                {
                                    Id = a.Id,
                                    Titulo = a.Titulo,
                                    Descripcion = a.Descripcion,
                                    FechaEntrega = a.FechaEntrega,
                                    Tipo = a.Tipo,
                                    Estado = a.Estado,
                                    MateriaId = a.MateriaId
                                })
                                .ToListAsync();

            if (!actividades.Any()) return NotFound(new { message = "No se encontraron actividades para esta materia." });

            return Ok(actividades);
        }

        // Nuevo endpoint para que un estudiante marque un recurso como visto
        [HttpPost("recursos/marcar-visto")]
        public async Task<IActionResult> MarkRecursoAsSeen([FromBody] MarkRecursoAsSeenDto markRecursoAsSeenDto)
        {
            if (UserId == null) return Unauthorized();

            // Verificar que el recurso existe
            var recurso = await _context.Recursos.FindAsync(markRecursoAsSeenDto.RecursoId);
            if (recurso == null) return NotFound(new { message = "Recurso no encontrado." });

            // Verificar si el estudiante está inscrito en la materia del recurso
            var isStudentInMateria = await _context.Inscripciones
                                        .AnyAsync(i => i.EstudianteId == UserId.Value && i.CatedraId == recurso.MateriaId);
            if (!isStudentInMateria)
            {
                return Forbid("No tienes permiso para marcar este recurso como visto, ya que no estás inscrito en la materia.");
            }

            // Verificar si ya está marcado como visto
            var existingEntry = await _context.RecursosVistosPorEstudiante
                                            .FirstOrDefaultAsync(rv => rv.RecursoId == markRecursoAsSeenDto.RecursoId && rv.EstudianteId == UserId.Value);

            if (existingEntry != null)
            {
                return Conflict(new { message = "Este recurso ya ha sido marcado como visto por este estudiante." });
            }

            var recursoVisto = new RecursoVistoPorEstudiante
            {
                RecursoId = markRecursoAsSeenDto.RecursoId,
                EstudianteId = UserId.Value,
                FechaVisto = DateTime.UtcNow
            };

            _context.RecursosVistosPorEstudiante.Add(recursoVisto);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Recurso marcado como visto exitosamente." });
        }

        // Nuevo endpoint para obtener el estado de los recursos de una materia (visto/no visto por el estudiante)
        [HttpGet("{materiaId}/recursos/estado")]
        public async Task<ActionResult<IEnumerable<RecursoConEstadoDto>>> GetRecursosConEstadoByMateria(int materiaId)
        {
            if (UserId == null) return Unauthorized();

            // Verificar si el estudiante está inscrito en la materia
            var isStudentInMateria = await _context.Inscripciones
                                        .AnyAsync(i => i.EstudianteId == UserId.Value && i.CatedraId == materiaId);
            if (!isStudentInMateria)
            {
                return Forbid("No tienes permiso para ver el estado de los recursos de esta materia.");
            }

            var recursos = await _context.Recursos
                                .Where(r => r.MateriaId == materiaId)
                                .Select(r => new RecursoConEstadoDto
                                {
                                    Id = r.Id,
                                    Titulo = r.Titulo,
                                    Descripcion = r.Descripcion,
                                    Url = r.Url,
                                    EsEsencial = r.EsEsencial,
                                    MateriaId = r.MateriaId,
                                    Visto = _context.RecursosVistosPorEstudiante
                                                    .Any(rv => rv.RecursoId == r.Id && rv.EstudianteId == UserId.Value)
                                })
                                .ToListAsync();

            if (!recursos.Any()) return NotFound(new { message = "No se encontraron recursos para esta materia." });

            return Ok(recursos);
        }
    }
}
