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

        // RF-019: valida que la materia/clase pertenezca realmente al docente autenticado.
        // MateriaId es el ID real de Materia; ya no se compara directamente con Catedra.Id.
        private async Task<bool> IsDocenteOfClaseSesionContext(int materiaId, int? claseId)
        {
            if (UserId == null)
                return false;

            if (claseId.HasValue)
            {
                var claseValida = await _context.Clases
                    .AsNoTracking()
                    .AnyAsync(c =>
                        c.Id == claseId.Value &&
                        c.MateriaId == materiaId &&
                        c.DocenteId == UserId.Value);

                if (claseValida)
                    return true;
            }

            var materia = await _context.Materias
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == materiaId);

            if (materia == null)
                return false;

            if (materia.DocenteResponsableId == UserId.Value)
                return true;

            var nombreMateria = (materia.Nombre ?? string.Empty).Trim().ToLower();

            return await _context.Catedras
                .AsNoTracking()
                .AnyAsync(c =>
                    c.DocenteId == UserId.Value &&
                    c.Nombre != null &&
                    c.Nombre.Trim().ToLower() == nombreMateria);
        }

        // RF-019: comprueba si el estudiante pertenece a la clase/cátedra de la sesión.
        private async Task<bool> IsEstudianteOfClaseSesionContext(ClaseSesion claseSesion)
        {
            if (UserId == null)
                return false;

            if (claseSesion.ClaseId.HasValue)
            {
                var inscritoEnClase = await _context.Inscripciones
                    .AsNoTracking()
                    .AnyAsync(i =>
                        i.EstudianteId == UserId.Value &&
                        i.ClaseId == claseSesion.ClaseId.Value);

                if (inscritoEnClase)
                    return true;

                var asociadoEnClase = await _context.Clases
                    .AsNoTracking()
                    .AnyAsync(c =>
                        c.Id == claseSesion.ClaseId.Value &&
                        c.Estudiantes.Any(e => e.Id == UserId.Value));

                if (asociadoEnClase)
                    return true;
            }

            var materia = await _context.Materias
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == claseSesion.MateriaId);

            if (materia == null)
                return false;

            var nombreMateria = (materia.Nombre ?? string.Empty).Trim().ToLower();

            var catedraIds = await _context.Catedras
                .AsNoTracking()
                .Where(c =>
                    c.Nombre != null &&
                    c.Nombre.Trim().ToLower() == nombreMateria)
                .Select(c => c.Id)
                .ToListAsync();

            if (catedraIds.Count == 0)
                return false;

            return await _context.Inscripciones
                .AsNoTracking()
                .AnyAsync(i =>
                    i.EstudianteId == UserId.Value &&
                    i.CatedraId.HasValue &&
                    catedraIds.Contains(i.CatedraId.Value));
        }

        private static ClaseSesionDto ToDto(ClaseSesion claseSesion)
        {
            return new ClaseSesionDto
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
                PisoPresencial = claseSesion.PisoPresencial,
                Observaciones = claseSesion.Observaciones
            };
        }

        // Endpoint para crear una sesión de clase (solo docentes)
        [HttpPost]
        public async Task<IActionResult> CreateClaseSesion(
            [FromBody] CreateClaseSesionDto createClaseSesionDto)
        {
            if (UserId == null)
                return Unauthorized(new { message = "Usuario no autenticado." });

            if (createClaseSesionDto == null)
            {
                return BadRequest(new
                {
                    message = "Los datos de la sesión son obligatorios."
                });
            }

            if (createClaseSesionDto.MateriaId <= 0)
            {
                return BadRequest(new
                {
                    message = "Debe seleccionar una materia válida."
                });
            }

            var materia = await _context.Materias
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == createClaseSesionDto.MateriaId);

            if (materia == null)
            {
                return NotFound(new
                {
                    message = "Materia no encontrada."
                });
            }

            if (createClaseSesionDto.ClaseId.HasValue)
            {
                var clase = await _context.Clases
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == createClaseSesionDto.ClaseId.Value);

                if (clase == null)
                {
                    return NotFound(new
                    {
                        message = "Clase no encontrada."
                    });
                }

                if (clase.MateriaId != createClaseSesionDto.MateriaId)
                {
                    return BadRequest(new
                    {
                        message = "La clase seleccionada no pertenece a la materia indicada."
                    });
                }
            }

            if (!await IsDocenteOfClaseSesionContext(
                    createClaseSesionDto.MateriaId,
                    createClaseSesionDto.ClaseId))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "Solo el docente responsable de la materia o clase puede crear sesiones."
                    });
            }

            if (createClaseSesionDto.Fecha == default)
            {
                return BadRequest(new
                {
                    message = "La fecha de la clase es obligatoria."
                });
            }

            if (createClaseSesionDto.HoraInicio < TimeSpan.Zero ||
                createClaseSesionDto.HoraInicio >= TimeSpan.FromDays(1) ||
                createClaseSesionDto.HoraFin <= TimeSpan.Zero ||
                createClaseSesionDto.HoraFin > TimeSpan.FromDays(1) ||
                createClaseSesionDto.HoraFin <= createClaseSesionDto.HoraInicio)
            {
                return BadRequest(new
                {
                    message = "El horario no es válido. La hora de finalización debe ser posterior a la hora de inicio."
                });
            }

            var tipoClase = createClaseSesionDto.TipoClase?.Trim();

            if (string.IsNullOrWhiteSpace(tipoClase))
            {
                return BadRequest(new
                {
                    message = "Debe indicar la modalidad de la clase: Virtual o Presencial."
                });
            }

            if (tipoClase.Equals("Virtual", StringComparison.OrdinalIgnoreCase))
            {
                tipoClase = "Virtual";

                if (string.IsNullOrWhiteSpace(createClaseSesionDto.AplicacionVirtual) ||
                    string.IsNullOrWhiteSpace(createClaseSesionDto.LinkVirtual))
                {
                    return BadRequest(new
                    {
                        message = "Las clases virtuales requieren plataforma y enlace de acceso."
                    });
                }
            }
            else if (tipoClase.Equals("Presencial", StringComparison.OrdinalIgnoreCase))
            {
                tipoClase = "Presencial";

                if (string.IsNullOrWhiteSpace(createClaseSesionDto.EdificioPresencial) ||
                    string.IsNullOrWhiteSpace(createClaseSesionDto.AulaPresencial) ||
                    string.IsNullOrWhiteSpace(createClaseSesionDto.PisoPresencial))
                {
                    return BadRequest(new
                    {
                        message = "Las clases presenciales requieren edificio, aula y piso."
                    });
                }
            }
            else
            {
                return BadRequest(new
                {
                    message = "La modalidad debe ser Virtual o Presencial."
                });
            }

            // Se guarda como fecha UTC a medianoche para evitar errores de Npgsql
            // con timestamp with time zone cuando Swagger envía una fecha sin Z.
            var fechaSesion = DateTime.SpecifyKind(
                createClaseSesionDto.Fecha.Date,
                DateTimeKind.Utc);
            var fechaSiguiente = fechaSesion.AddDays(1);

            var conflictoDocente = await _context.ClasesSesiones
                .AsNoTracking()
                .AnyAsync(cs =>
                    cs.DocenteId == UserId.Value &&
                    cs.Fecha >= fechaSesion &&
                    cs.Fecha < fechaSiguiente &&
                    cs.HoraInicio < createClaseSesionDto.HoraFin &&
                    createClaseSesionDto.HoraInicio < cs.HoraFin);

            if (conflictoDocente)
            {
                return Conflict(new
                {
                    message = "El docente ya tiene una sesión programada que se cruza con ese horario."
                });
            }

            if (createClaseSesionDto.ClaseId.HasValue)
            {
                var conflictoClase = await _context.ClasesSesiones
                    .AsNoTracking()
                    .AnyAsync(cs =>
                        cs.ClaseId == createClaseSesionDto.ClaseId.Value &&
                        cs.Fecha >= fechaSesion &&
                        cs.Fecha < fechaSiguiente &&
                        cs.HoraInicio < createClaseSesionDto.HoraFin &&
                        createClaseSesionDto.HoraInicio < cs.HoraFin);

                if (conflictoClase)
                {
                    return Conflict(new
                    {
                        message = "La clase ya tiene una sesión programada que se cruza con ese horario."
                    });
                }
            }

            var claseSesion = new ClaseSesion
            {
                MateriaId = createClaseSesionDto.MateriaId,
                ClaseId = createClaseSesionDto.ClaseId,
                DocenteId = UserId.Value,
                Fecha = fechaSesion,
                HoraInicio = createClaseSesionDto.HoraInicio,
                HoraFin = createClaseSesionDto.HoraFin,
                TipoClase = tipoClase,
                LinkVirtual = tipoClase == "Virtual"
                    ? createClaseSesionDto.LinkVirtual?.Trim() ?? string.Empty
                    : string.Empty,
                AplicacionVirtual = tipoClase == "Virtual"
                    ? createClaseSesionDto.AplicacionVirtual?.Trim() ?? string.Empty
                    : string.Empty,
                EdificioPresencial = tipoClase == "Presencial"
                    ? createClaseSesionDto.EdificioPresencial?.Trim() ?? string.Empty
                    : string.Empty,
                AulaPresencial = tipoClase == "Presencial"
                    ? createClaseSesionDto.AulaPresencial?.Trim() ?? string.Empty
                    : string.Empty,
                PisoPresencial = tipoClase == "Presencial"
                    ? createClaseSesionDto.PisoPresencial?.Trim() ?? string.Empty
                    : string.Empty,
                Observaciones = createClaseSesionDto.Observaciones?.Trim() ?? string.Empty
            };

            _context.ClasesSesiones.Add(claseSesion);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(GetClaseSesionById),
                new { id = claseSesion.Id },
                ToDto(claseSesion));
        }

        // Endpoint para obtener una sesión de clase por ID
        [HttpGet("{id}")]
        public async Task<ActionResult<ClaseSesionDto>> GetClaseSesionById(int id)
        {
            if (UserId == null)
                return Unauthorized();

            var claseSesion = await _context.ClasesSesiones
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.Id == id);

            if (claseSesion == null)
                return NotFound(new { message = "Sesión de clase no encontrada." });

            var isDocente = claseSesion.DocenteId == UserId.Value;
            var isStudent = await IsEstudianteOfClaseSesionContext(claseSesion);

            if (!isDocente && !isStudent)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "No tienes permiso para ver esta sesión de clase."
                    });
            }

            return Ok(ToDto(claseSesion));
        }

        // Endpoint para obtener sesiones de clase por materia o clase
        // (para estudiantes y docentes asociados).
        [HttpGet("materia/{materiaId}")]
        [HttpGet("clase/{claseId}")]
        public async Task<ActionResult<IEnumerable<ClaseSesionDto>>> GetClaseSesiones(
            int? materiaId = null,
            int? claseId = null)
        {
            if (UserId == null)
                return Unauthorized();

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
                return BadRequest(new
                {
                    message = "Debe proporcionar un ID de materia o un ID de clase."
                });
            }

            var sesiones = await query
                .OrderBy(cs => cs.Fecha)
                .ThenBy(cs => cs.HoraInicio)
                .ToListAsync();

            if (!sesiones.Any())
                return NotFound(new { message = "No se encontraron sesiones de clase." });

            var authorizedSesiones = new List<ClaseSesionDto>();

            foreach (var sesion in sesiones)
            {
                var isDocente = sesion.DocenteId == UserId.Value;
                var isStudent = await IsEstudianteOfClaseSesionContext(sesion);

                if (isDocente || isStudent)
                    authorizedSesiones.Add(ToDto(sesion));
            }

            if (!authorizedSesiones.Any())
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "No tienes permiso para ver estas sesiones de clase."
                    });
            }

            return Ok(authorizedSesiones);
        }

        // =========================================================
        // RF-020 - REGISTRO DE ASISTENCIA POR SESIÓN DE CLASE
        // =========================================================

        // Verifica si un estudiante realmente pertenece a la clase asociada a la sesión.
        private async Task<bool> IsEstudianteRegistradoEnSesion(
            ClaseSesion claseSesion,
            int estudianteId)
        {
            if (claseSesion.ClaseId.HasValue)
            {
                var inscritoPorInscripcion = await _context.Inscripciones
                    .AsNoTracking()
                    .AnyAsync(i =>
                        i.EstudianteId == estudianteId &&
                        i.ClaseId == claseSesion.ClaseId.Value);

                if (inscritoPorInscripcion)
                    return true;

                var inscritoPorClase = await _context.Clases
                    .AsNoTracking()
                    .AnyAsync(c =>
                        c.Id == claseSesion.ClaseId.Value &&
                        c.Estudiantes.Any(e => e.Id == estudianteId));

                if (inscritoPorClase)
                    return true;
            }

            // Respaldo para sesiones antiguas que no tengan ClaseId.
            var materia = await _context.Materias
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == claseSesion.MateriaId);

            if (materia == null)
                return false;

            var nombreMateria = (materia.Nombre ?? string.Empty)
                .Trim()
                .ToLower();

            var catedraIds = await _context.Catedras
                .AsNoTracking()
                .Where(c =>
                    c.DocenteId == claseSesion.DocenteId &&
                    c.Nombre != null &&
                    c.Nombre.Trim().ToLower() == nombreMateria)
                .Select(c => c.Id)
                .ToListAsync();

            if (catedraIds.Count == 0)
                return false;

            return await _context.Inscripciones
                .AsNoTracking()
                .AnyAsync(i =>
                    i.EstudianteId == estudianteId &&
                    i.CatedraId.HasValue &&
                    catedraIds.Contains(i.CatedraId.Value));
        }

        // GET /api/ClaseSesion/{claseSesionId}/estudiantes
        // El docente selecciona una sesión y obtiene los estudiantes de esa clase,
        // incluyendo si su asistencia ya fue registrada.
        [HttpGet("{claseSesionId}/estudiantes")]
        public async Task<IActionResult> GetEstudiantesDeSesion(int claseSesionId)
        {
            if (UserId == null)
                return Unauthorized(new { message = "Usuario no autenticado." });

            var claseSesion = await _context.ClasesSesiones
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.Id == claseSesionId);

            if (claseSesion == null)
            {
                return NotFound(new
                {
                    message = "Sesión de clase no encontrada."
                });
            }

            if (claseSesion.DocenteId != UserId.Value)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "Solo el docente de esta sesión puede consultar la lista de estudiantes."
                    });
            }

            var estudianteIds = new HashSet<int>();

            if (claseSesion.ClaseId.HasValue)
            {
                var idsInscripcion = await _context.Inscripciones
                    .AsNoTracking()
                    .Where(i => i.ClaseId == claseSesion.ClaseId.Value)
                    .Select(i => i.EstudianteId)
                    .ToListAsync();

                foreach (var id in idsInscripcion)
                    estudianteIds.Add(id);

                var idsClase = await _context.Clases
                    .AsNoTracking()
                    .Where(c => c.Id == claseSesion.ClaseId.Value)
                    .SelectMany(c => c.Estudiantes.Select(e => e.Id))
                    .ToListAsync();

                foreach (var id in idsClase)
                    estudianteIds.Add(id);
            }

            // Respaldo para sesiones antiguas o clases sin relación completa.
            if (estudianteIds.Count == 0)
            {
                var materia = await _context.Materias
                    .AsNoTracking()
                    .FirstOrDefaultAsync(m => m.Id == claseSesion.MateriaId);

                if (materia != null)
                {
                    var nombreMateria = (materia.Nombre ?? string.Empty)
                        .Trim()
                        .ToLower();

                    var catedraIds = await _context.Catedras
                        .AsNoTracking()
                        .Where(c =>
                            c.DocenteId == claseSesion.DocenteId &&
                            c.Nombre != null &&
                            c.Nombre.Trim().ToLower() == nombreMateria)
                        .Select(c => c.Id)
                        .ToListAsync();

                    if (catedraIds.Count > 0)
                    {
                        var idsCatedra = await _context.Inscripciones
                            .AsNoTracking()
                            .Where(i =>
                                i.CatedraId.HasValue &&
                                catedraIds.Contains(i.CatedraId.Value))
                            .Select(i => i.EstudianteId)
                            .ToListAsync();

                        foreach (var id in idsCatedra)
                            estudianteIds.Add(id);
                    }
                }
            }

            var estudiantes = await _context.Users
                .AsNoTracking()
                .Include(u => u.Persona)
                .Where(u => estudianteIds.Contains(u.Id))
                .ToListAsync();

            var asistencias = await _context.Asistencias
                .AsNoTracking()
                .Where(a => a.ClaseSesionId == claseSesionId)
                .ToListAsync();

            var resultado = estudiantes
                .Select(e =>
                {
                    var asistencia = asistencias
                        .FirstOrDefault(a => a.EstudianteId == e.Id);

                    return new
                    {
                        estudianteId = e.Id,
                        nombre = e.Persona != null
                            ? $"{e.Persona.Nombre} {e.Persona.Apellido}".Trim()
                            : e.Username,
                        correo = e.Persona?.Correo ?? e.Username,
                        asistenciaRegistrada = asistencia != null,
                        asistenciaId = asistencia?.Id,
                        presente = asistencia?.Presente
                    };
                })
                .OrderBy(e => e.nombre)
                .ToList();

            return Ok(new
            {
                claseSesionId = claseSesion.Id,
                materiaId = claseSesion.MateriaId,
                claseId = claseSesion.ClaseId,
                fecha = claseSesion.Fecha,
                horaInicio = claseSesion.HoraInicio,
                horaFin = claseSesion.HoraFin,
                estudiantes = resultado
            });
        }

        // POST /api/ClaseSesion/{claseSesionId}/asistencia
        // Registra una sola asistencia para un estudiante que pertenezca a la clase.
        [HttpPost("{claseSesionId}/asistencia")]
        public async Task<IActionResult> RegisterAsistencia(
            int claseSesionId,
            [FromBody] CreateAsistenciaDto createAsistenciaDto)
        {
            if (UserId == null)
                return Unauthorized(new { message = "Usuario no autenticado." });

            if (createAsistenciaDto == null)
            {
                return BadRequest(new
                {
                    message = "Los datos de asistencia son obligatorios."
                });
            }

            var claseSesion = await _context.ClasesSesiones
                .FirstOrDefaultAsync(cs => cs.Id == claseSesionId);

            if (claseSesion == null)
            {
                return NotFound(new
                {
                    message = "Sesión de clase no encontrada."
                });
            }

            if (claseSesion.DocenteId != UserId.Value)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "Solo el docente de esta sesión puede registrar asistencia."
                    });
            }

            var estudiante = await _context.Users
                .Include(u => u.Persona)
                .FirstOrDefaultAsync(u => u.Id == createAsistenciaDto.EstudianteId);

            if (estudiante == null)
            {
                return BadRequest(new
                {
                    message = "El ID de estudiante proporcionado no es válido."
                });
            }

            var rolesEstudiante = estudiante.Persona?.GetRoles()
                ?? new List<string>();

            var esEstudiante = rolesEstudiante.Any(r =>
                r.Equals(
                    "Estudiante",
                    StringComparison.OrdinalIgnoreCase));

            if (!esEstudiante)
            {
                return BadRequest(new
                {
                    message = "El usuario indicado no tiene rol de Estudiante."
                });
            }

            var perteneceAClase = await IsEstudianteRegistradoEnSesion(
                claseSesion,
                createAsistenciaDto.EstudianteId);

            if (!perteneceAClase)
            {
                return BadRequest(new
                {
                    message = "Solo se puede registrar asistencia a estudiantes pertenecientes a la clase de esta sesión."
                });
            }

            var existingAsistencia = await _context.Asistencias
                .AsNoTracking()
                .AnyAsync(a =>
                    a.ClaseSesionId == claseSesionId &&
                    a.EstudianteId == createAsistenciaDto.EstudianteId);

            if (existingAsistencia)
            {
                return Conflict(new
                {
                    message = "La asistencia para este estudiante en esta sesión ya ha sido registrada."
                });
            }

            var asistencia = new Asistencia
            {
                ClaseSesionId = claseSesionId,
                EstudianteId = createAsistenciaDto.EstudianteId,
                Presente = createAsistenciaDto.Presente
            };

            _context.Asistencias.Add(asistencia);
            await _context.SaveChangesAsync();

            return StatusCode(
                StatusCodes.Status201Created,
                new
                {
                    message = "Asistencia registrada correctamente.",
                    asistencia = new
                    {
                        id = asistencia.Id,
                        claseSesionId = asistencia.ClaseSesionId,
                        estudianteId = asistencia.EstudianteId,
                        presente = asistencia.Presente,
                        fechaSesion = claseSesion.Fecha
                    }
                });
        }

        // GET /api/ClaseSesion/{claseSesionId}/asistencia
        // Historial de asistencia de una sesión para el docente responsable.
        [HttpGet("{claseSesionId}/asistencia")]
        public async Task<IActionResult> GetAsistenciaByClaseSesion(int claseSesionId)
        {
            if (UserId == null)
                return Unauthorized(new { message = "Usuario no autenticado." });

            var claseSesion = await _context.ClasesSesiones
                .AsNoTracking()
                .FirstOrDefaultAsync(cs => cs.Id == claseSesionId);

            if (claseSesion == null)
            {
                return NotFound(new
                {
                    message = "Sesión de clase no encontrada."
                });
            }

            if (claseSesion.DocenteId != UserId.Value)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new
                    {
                        message = "Solo el docente de esta sesión puede ver la asistencia."
                    });
            }

            var asistencias = await _context.Asistencias
                .AsNoTracking()
                .Where(a => a.ClaseSesionId == claseSesionId)
                .Include(a => a.Estudiante)
                    .ThenInclude(e => e.Persona)
                .OrderBy(a => a.Estudiante.Persona != null
                    ? a.Estudiante.Persona.Apellido
                    : a.Estudiante.Username)
                .Select(a => new
                {
                    id = a.Id,
                    claseSesionId = a.ClaseSesionId,
                    estudianteId = a.EstudianteId,
                    nombreEstudiante = a.Estudiante.Persona != null
                        ? (a.Estudiante.Persona.Nombre + " " + a.Estudiante.Persona.Apellido).Trim()
                        : a.Estudiante.Username,
                    presente = a.Presente,
                    fechaSesion = claseSesion.Fecha
                })
                .ToListAsync();

            return Ok(new
            {
                claseSesionId = claseSesion.Id,
                fecha = claseSesion.Fecha,
                horaInicio = claseSesion.HoraInicio,
                horaFin = claseSesion.HoraFin,
                registros = asistencias
            });
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
