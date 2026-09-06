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
    public class ClaseSesionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClaseSesionController(AppDbContext context)
        {
            _context = context;
        }

        private int? UserId
        {
            get
            {
                var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return int.TryParse(value, out var id) ? id : (int?)null;
            }
        }

        // Helper para verificar si el usuario es docente de la materia o de la clase
        private async Task<bool> IsDocenteOfClaseSesionContext(int materiaId, int? claseId)
        {
            if (UserId == null) return false;

            // Check if docente of Materia
            var isDocenteMateria = await _context.Catedras.AsNoTracking()
                                        .AnyAsync(m => m.Id == materiaId && m.DocenteId == UserId.Value);
            if (isDocenteMateria) return true;

            // Check if docente of Clase (if claseId is provided)
            if (claseId.HasValue)
            {
                var isDocenteClase = await _context.Clases.AsNoTracking()
                                            .AnyAsync(c => c.Id == claseId.Value && c.DocenteId == UserId.Value);
                if (isDocenteClase) return true;
            }

            return false;
        }

        // Endpoint para crear una sesión de clase (solo docentes)
        [HttpPost]
        public async Task<IActionResult> CreateClaseSesion([FromBody] CreateClaseSesionDto createClaseSesionDto)
        {
            if (UserId == null) return Unauthorized();
            if (!await IsDocenteOfClaseSesionContext(createClaseSesionDto.MateriaId, createClaseSesionDto.ClaseId))
            {
                // Cambiar:
                // return Forbid("Solo el docente responsable de la materia o clase puede crear sesiones.");

                // Por:
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Solo el docente responsable de la materia o clase puede crear sesiones." });
            }

            var claseSesion = new ClaseSesion
            {
                MateriaId = createClaseSesionDto.MateriaId,
                ClaseId = createClaseSesionDto.ClaseId,
                DocenteId = UserId.Value, // El docente que crea la sesión es el autenticado
                Fecha = createClaseSesionDto.Fecha,
                HoraInicio = createClaseSesionDto.HoraInicio,
                HoraFin = createClaseSesionDto.HoraFin,
                TipoClase = createClaseSesionDto.TipoClase,
                LinkVirtual = createClaseSesionDto.LinkVirtual,
                AplicacionVirtual = createClaseSesionDto.AplicacionVirtual,
                EdificioPresencial = createClaseSesionDto.EdificioPresencial,
                AulaPresencial = createClaseSesionDto.AulaPresencial,
                PisoPresencial = createClaseSesionDto.PisoPresencial
            };

            _context.ClasesSesiones.Add(claseSesion);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetClaseSesionById), new { id = claseSesion.Id }, new ClaseSesionDto
            {
                Id = claseSesion.Id,
                MateriaId = claseSesion.MateriaId,
                ClaseId = claseSesion.ClaseId,
                DocenteId = claseSesion.DocenteId,
                Fecha = claseSesion.Fecha,
                HoraInicio = claseSesion.HoraInicio,
                HoraFin = claseSesion.HoraFin,
                TipoClase = claseSesion.TipoClase,
                LinkVirtual = claseSesion.LinkVirtual,
                AplicacionVirtual = claseSesion.AplicacionVirtual,
                EdificioPresencial = claseSesion.EdificioPresencial,
                AulaPresencial = claseSesion.AulaPresencial,
                PisoPresencial = claseSesion.PisoPresencial
            });
        }

        // Endpoint para obtener una sesión de clase por ID
        [HttpGet("{id}")]
        public async Task<ActionResult<ClaseSesionDto>> GetClaseSesionById(int id)
        {
            if (UserId == null) return Unauthorized();

            var claseSesion = await _context.ClasesSesiones
                                .AsNoTracking()
                                .FirstOrDefaultAsync(cs => cs.Id == id);

            if (claseSesion == null) return NotFound();

            // Autorización: Docente de la sesión, o estudiante inscrito en la materia/clase
            var isDocente = claseSesion.DocenteId == UserId.Value;
            var isStudentInMateria = await _context.Inscripciones.AnyAsync(i => i.EstudianteId == UserId.Value && i.CatedraId == claseSesion.MateriaId);
            var isStudentInClase = claseSesion.ClaseId.HasValue && await _context.Clases.AnyAsync(c => c.Id == claseSesion.ClaseId.Value && c.Estudiantes.Any(e => e.Id == UserId.Value));


            if (!isDocente && !isStudentInMateria && !isStudentInClase)
            {
                // Cambiar:
                // return Forbid("No tienes permiso para ver esta sesión de clase.");

                // Por:
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "No tienes permiso para ver esta sesión de clase." });
            }

            return Ok(new ClaseSesionDto
            {
                Id = claseSesion.Id,
                MateriaId = claseSesion.MateriaId,
                ClaseId = claseSesion.ClaseId,
                DocenteId = claseSesion.DocenteId,
                Fecha = claseSesion.Fecha,
                HoraInicio = claseSesion.HoraInicio,
                HoraFin = claseSesion.HoraFin,
                TipoClase = claseSesion.TipoClase,
                LinkVirtual = claseSesion.LinkVirtual,
                AplicacionVirtual = claseSesion.AplicacionVirtual,
                EdificioPresencial = claseSesion.EdificioPresencial,
                AulaPresencial = claseSesion.AulaPresencial,
                PisoPresencial = claseSesion.PisoPresencial
            });
        }

        // Endpoint para obtener sesiones de clase por materia o clase (para estudiantes y docentes)
        [HttpGet("materia/{materiaId}")]
        [HttpGet("clase/{claseId}")]
        public async Task<ActionResult<IEnumerable<ClaseSesionDto>>> GetClaseSesiones(int? materiaId = null, int? claseId = null)
        {
            if (UserId == null) return Unauthorized();

            IQueryable<ClaseSesion> query = _context.ClasesSesiones.AsNoTracking();

            if (materiaId.HasValue)
            {
                query = query.Where(cs => cs.MateriaId == materiaId.Value);
            }
            else if (claseId.HasValue)
            {
                query = query.Where(cs => cs.ClaseId == claseId.Value);
            }
            else
            {
                return BadRequest("Debe proporcionar un ID de materia o un ID de clase.");
            }

            var sesiones = await query
                                .Select(cs => new ClaseSesionDto
                                {
                                    Id = cs.Id,
                                    MateriaId = cs.MateriaId,
                                    ClaseId = cs.ClaseId,
                                    DocenteId = cs.DocenteId,
                                    Fecha = cs.Fecha,
                                    HoraInicio = cs.HoraInicio,
                                    HoraFin = cs.HoraFin,
                                    TipoClase = cs.TipoClase,
                                    LinkVirtual = cs.LinkVirtual,
                                    AplicacionVirtual = cs.AplicacionVirtual,
                                    EdificioPresencial = cs.EdificioPresencial,
                                    AulaPresencial = cs.AulaPresencial,
                                    PisoPresencial = cs.PisoPresencial
                                })
                                .ToListAsync();

            if (!sesiones.Any()) return NotFound("No se encontraron sesiones de clase.");

            // Filtrar por autorización (docente de la materia/clase o estudiante inscrito)
            var authorizedSesiones = new List<ClaseSesionDto>();
            foreach (var sesion in sesiones)
            {
                var isDocente = sesion.DocenteId == UserId.Value;
                var isStudentInMateria = await _context.Inscripciones.AnyAsync(i => i.EstudianteId == UserId.Value && i.CatedraId == sesion.MateriaId);
                var isStudentInClase = sesion.ClaseId.HasValue && await _context.Clases.AnyAsync(c => c.Id == sesion.ClaseId.Value && c.Estudiantes.Any(e => e.Id == UserId.Value));

                if (isDocente || isStudentInMateria || isStudentInClase)
                {
                    authorizedSesiones.Add(sesion);
                }
            }

            // Cambiar:
            // if (!authorizedSesiones.Any()) return Forbid("No tienes permiso para ver estas sesiones de clase.");

            // Por:
            if (!authorizedSesiones.Any())
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "No tienes permiso para ver estas sesiones de clase." });

            return Ok(authorizedSesiones);
        }


        // Endpoint para registrar asistencia a una sesión de clase (solo docentes)
        [HttpPost("{claseSesionId}/asistencia")]
        public async Task<IActionResult> RegisterAsistencia(int claseSesionId, [FromBody] CreateAsistenciaDto createAsistenciaDto)
        {
            if (UserId == null) return Unauthorized();

            var claseSesion = await _context.ClasesSesiones.FindAsync(claseSesionId);
            if (claseSesion == null) return NotFound("Sesión de clase no encontrada.");

            if (claseSesion.DocenteId != UserId.Value)
            {
                // Cambiar:
                // return Forbid("Solo el docente de esta sesión puede registrar asistencia.");

                // Por:
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Solo el docente de esta sesión puede registrar asistencia." });
            }

            // CAMBIO AQUÍ: u.Persona.Tipo -> u.Persona.Rol
            var estudianteExists = await _context.Users.AnyAsync(u => u.Id == createAsistenciaDto.EstudianteId && u.Persona.Rol == "Estudiante");
            if (!estudianteExists) return BadRequest("El ID de estudiante proporcionado no es válido.");

            var existingAsistencia = await _context.Asistencias
                                            .AnyAsync(a => a.ClaseSesionId == claseSesionId && a.EstudianteId == createAsistenciaDto.EstudianteId);
            if (existingAsistencia)
            {
                return Conflict("La asistencia para este estudiante en esta sesión ya ha sido registrada.");
            }

            var asistencia = new Asistencia
            {
                ClaseSesionId = claseSesionId,
                EstudianteId = createAsistenciaDto.EstudianteId,
                Presente = createAsistenciaDto.Presente
            };

            _context.Asistencias.Add(asistencia);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAsistenciaByClaseSesion), new { claseSesionId = claseSesionId }, new AsistenciaDto
            {
                Id = asistencia.Id,
                ClaseSesionId = asistencia.ClaseSesionId,
                EstudianteId = asistencia.EstudianteId,
                Presente = asistencia.Presente
            });
        }

        // Endpoint para obtener la asistencia de una sesión de clase (solo docentes)
        [HttpGet("{claseSesionId}/asistencia")]
        public async Task<ActionResult<IEnumerable<AsistenciaDto>>> GetAsistenciaByClaseSesion(int claseSesionId)
        {
            if (UserId == null) return Unauthorized();

            var claseSesion = await _context.ClasesSesiones.AsNoTracking().FirstOrDefaultAsync(cs => cs.Id == claseSesionId);
            if (claseSesion == null) return NotFound("Sesión de clase no encontrada.");

            if (claseSesion.DocenteId != UserId.Value)
            {
                // Cambiar:
                // return Forbid("Solo el docente de esta sesión puede ver la asistencia.");

                // Por:
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Solo el docente de esta sesión puede ver la asistencia." });
            }

            var asistencias = await _context.Asistencias
                                    .Where(a => a.ClaseSesionId == claseSesionId)
                                    .Select(a => new AsistenciaDto
                                    {
                                        Id = a.Id,
                                        ClaseSesionId = a.ClaseSesionId,
                                        EstudianteId = a.EstudianteId,
                                        Presente = a.Presente
                                    })
                                    .ToListAsync();

            if (!asistencias.Any()) return NotFound("No se ha registrado asistencia para esta sesión.");

            return Ok(asistencias);
        }

        // Endpoint para que un estudiante vea su asistencia a una sesión específica
        [HttpGet("estudiante/asistencia/{claseSesionId}")]
        public async Task<ActionResult<AsistenciaDto>> GetMyAsistenciaForClaseSesion(int claseSesionId)
        {
            if (UserId == null) return Unauthorized();

            var asistencia = await _context.Asistencias
                                .AsNoTracking()
                                .FirstOrDefaultAsync(a => a.ClaseSesionId == claseSesionId && a.EstudianteId == UserId.Value);

            if (asistencia == null) return NotFound("No se encontró registro de asistencia para esta sesión.");

            return Ok(new AsistenciaDto
            {
                Id = asistencia.Id,
                ClaseSesionId = asistencia.ClaseSesionId,
                EstudianteId = asistencia.EstudianteId,
                Presente = asistencia.Presente
            });
        }
    }
}