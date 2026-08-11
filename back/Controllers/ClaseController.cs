using back.Data;
using back.DTOs;
using back.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace back.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class ClaseController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ClaseController(AppDbContext context)
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
        private async Task<bool> IsDocenteOfMateria(int materiaId)
        {
            if (UserId == null) return false;
            return await _context.Catedras.AnyAsync(m => m.Id == materiaId && m.DocenteId == UserId.Value);
        }

        private async Task<bool> IsDocenteOfClase(int claseId)
        {
            if (UserId == null) return false;
            return await _context.Clases.AnyAsync(c => c.Id == claseId && c.DocenteId == UserId.Value);
        }

        // Endpoint para crear una nueva instancia de Clase (solo docentes)
        [HttpPost]
        public async Task<IActionResult> CreateClase([FromBody] CreateClaseDto createClaseDto)
        {
            if (UserId == null) return Unauthorized();

            // Verificar si el usuario autenticado es el docente que se está asignando a la clase
            if (createClaseDto.DocenteId != UserId.Value)
            {
                return Forbid("Solo puedes crear clases para ti mismo como docente.");
            }

            // Verificar si la materia existe
            var materia = await _context.Catedras.FindAsync(createClaseDto.MateriaId);
            if (materia == null) return NotFound(new { message = "Materia no encontrada." });

            // Verificar si el docente es realmente docente de esa materia (opcional, pero buena práctica)
            if (!await IsDocenteOfMateria(createClaseDto.MateriaId))
            {
                return Forbid("El docente asignado no es responsable de esta materia.");
            }

            var clase = new Clase
            {
                Nombre = createClaseDto.Nombre,
                MateriaId = createClaseDto.MateriaId,
                DocenteId = createClaseDto.DocenteId
            };

            // Añadir estudiantes si se proporcionan
            if (createClaseDto.EstudianteIds != null && createClaseDto.EstudianteIds.Any())
            {
                var estudiantes = await _context.Users
                                                .Where(u => createClaseDto.EstudianteIds.Contains(u.Id) && u.Persona.Rol == "Estudiante") // Corregido: Tipo -> Rol
                                                .ToListAsync();
                if (estudiantes.Count != createClaseDto.EstudianteIds.Count)
                {
                    return BadRequest("Algunos IDs de estudiantes proporcionados no son válidos o no corresponden a estudiantes.");
                }
                foreach (var estudiante in estudiantes)
                {
                    clase.Estudiantes.Add(estudiante);
                }
            }

            _context.Clases.Add(clase);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetClaseById), new { id = clase.Id }, new ClaseDto
            {
                Id = clase.Id,
                Nombre = clase.Nombre,
                MateriaId = clase.MateriaId,
                DocenteId = clase.DocenteId,
                EstudianteIds = clase.Estudiantes.Select(e => e.Id).ToList()
            });
        }

        // Endpoint para obtener una clase por ID
        [HttpGet("{id}")]
        public async Task<ActionResult<ClaseDto>> GetClaseById(int id)
        {
            if (UserId == null) return Unauthorized();

            var clase = await _context.Clases
                                .Include(c => c.Estudiantes)
                                .AsNoTracking()
                                .FirstOrDefaultAsync(c => c.Id == id);

            if (clase == null) return NotFound();

            // Autorización: Docente de la clase o estudiante inscrito en la clase
            var isDocente = clase.DocenteId == UserId.Value;
            var isStudent = clase.Estudiantes.Any(e => e.Id == UserId.Value);

            if (!isDocente && !isStudent)
            {
                return Forbid("No tienes permiso para ver esta clase.");
            }

            return Ok(new ClaseDto
            {
                Id = clase.Id,
                Nombre = clase.Nombre,
                MateriaId = clase.MateriaId,
                DocenteId = clase.DocenteId,
                EstudianteIds = clase.Estudiantes.Select(e => e.Id).ToList()
            });
        }

        // Endpoint para añadir estudiantes a una clase existente (solo docentes de la clase)
        [HttpPost("{claseId}/estudiantes")]
        public async Task<IActionResult> AddEstudiantesToClase(int claseId, [FromBody] List<int> estudianteIds)
        {
            if (UserId == null) return Unauthorized();
            if (!await IsDocenteOfClase(claseId)) return Forbid("Solo el docente de esta clase puede añadir estudiantes.");

            var clase = await _context.Clases
                                .Include(c => c.Estudiantes)
                                .FirstOrDefaultAsync(c => c.Id == claseId);

            if (clase == null) return NotFound(new { message = "Clase no encontrada." });

            var newEstudiantes = await _context.Users
                                            .Where(u => estudianteIds.Contains(u.Id) && u.Persona.Rol == "Estudiante") // Corregido: Tipo -> Rol
                                            .ToListAsync();

            if (newEstudiantes.Count != estudianteIds.Count)
            {
                return BadRequest("Algunos IDs de estudiantes proporcionados no son válidos o no corresponden a estudiantes.");
            }

            foreach (var estudiante in newEstudiantes)
            {
                if (!clase.Estudiantes.Any(e => e.Id == estudiante.Id))
                {
                    clase.Estudiantes.Add(estudiante);
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Estudiantes añadidos exitosamente a la clase." });
        }

        // Endpoint para obtener los estudiantes de una clase (docentes de la clase o estudiantes de la clase)
        [HttpGet("{claseId}/estudiantes")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetEstudiantesFromClase(int claseId)
        {
            if (UserId == null) return Unauthorized();

            var clase = await _context.Clases
                                .Include(c => c.Estudiantes)
                                .AsNoTracking()
                                .FirstOrDefaultAsync(c => c.Id == claseId);

            if (clase == null) return NotFound(new { message = "Clase no encontrada." });

            // Autorización: Docente de la clase o estudiante inscrito en la clase
            var isDocente = clase.DocenteId == UserId.Value;
            var isStudent = clase.Estudiantes.Any(e => e.Id == UserId.Value);

            if (!isDocente && !isStudent)
            {
                return Forbid("No tienes permiso para ver los estudiantes de esta clase.");
            }

            var estudiantesDto = clase.Estudiantes.Select(e => new UserDto
            {
                Id = e.Id,
                Username = e.Username,
                // No incluir PasswordHash ni PasswordSalt por seguridad
                // Puedes incluir otros datos de Persona si es necesario
            }).ToList();

            return Ok(estudiantesDto);
        }
    }
}
