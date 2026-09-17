using back.Data;
using back.DTOs;
using back.Entities;
using back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace back.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Route("api/docentes")]
    public class DocenteController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<DocenteController> _logger;

        public DocenteController(
            AppDbContext context,
            IEmailService emailService,
            ILogger<DocenteController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        // =========================================================
        // CONVOCATORIAS
        // =========================================================

        // POST /api/Docente/convocatorias
        [HttpPost("convocatorias")]
        public async Task<IActionResult> CrearConvocatoria(
            [FromBody] CreateConvocatoriaDto dto)
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(value, out var userId))
                return Unauthorized();

            var catedra = await _context.Catedras.FindAsync(dto.CatedraId);

            if (catedra == null)
                return NotFound("CÃ¡tedra no encontrada.");

            var convocatoria = new Convocatoria
            {
                CatedraId = dto.CatedraId,
                Descripcion = dto.Descripcion,
                Plazas = dto.Plazas,
                Estado = "PendienteAprobacion",
                CreatedByUserId = userId,
                CreatedAt = System.DateTime.UtcNow
            };

            _context.Convocatorias.Add(convocatoria);
            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(CrearConvocatoria),
                new { id = convocatoria.Id },
                new
                {
                    convocatoria.Id,
                    convocatoria.CatedraId,
                    convocatoria.Estado
                });
        }

        // =========================================================
        // DOCENTE AUTENTICADO
        // =========================================================

        private async Task<User> GetDefaultDocenteAsync(
            long? requestedDocenteId = null)
        {
            if (requestedDocenteId.HasValue &&
                requestedDocenteId.Value > 0 &&
                requestedDocenteId.Value <= int.MaxValue)
            {
                var doc = await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u =>
                        u.Id == (int)requestedDocenteId.Value ||
                        (u.Persona != null &&
                         (u.Persona.Id == (int)requestedDocenteId.Value ||
                          u.Persona.UserId == (int)requestedDocenteId.Value)));

                if (doc != null)
                    return doc;
            }

            var defaultDoc = await _context.Users
                .Include(u => u.Persona)
                .FirstOrDefaultAsync(u =>
                    (u.Persona != null &&
                     u.Persona.Correo.ToLower() == "docente@uteq.edu.ec") ||
                    u.Username.ToLower() == "docente@uteq.edu.ec" ||
                    u.Username.ToLower() == "docente");

            if (defaultDoc != null)
                return defaultDoc;

            var anyDoc = await _context.Users
                .Include(u => u.Persona)
                .FirstOrDefaultAsync(u =>
                    u.Persona != null &&
                    (u.Persona.Rol == "Docente" ||
                     u.Persona.Rol.Contains("Docente")));

            if (anyDoc != null)
                return anyDoc;

            return await _context.Users
                .Include(u => u.Persona)
                .FirstOrDefaultAsync();
        }

        // =========================================================
        // CLASES DEL DOCENTE
        // =========================================================

        // GET /api/Docente/clases
        [HttpGet("clases")]
        [Authorize(Roles = "Docente,Administrador")]
        public async Task<IActionResult> GetClasesDocenteLogueado()
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(value, out var userId))
            {
                return Unauthorized(new
                {
                    message = "Usuario no autenticado."
                });
            }

            var user = await _context.Users
                .Include(u => u.Persona)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Unauthorized(new
                {
                    message = "Usuario no encontrado."
                });
            }

            var roles = user.Persona?.GetRoles()
                        ?? new List<string>();

            var callerRoleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            var esAdmin = User.IsInRole("Administrador") ||
                          callerRoleClaim.Equals("Administrador", System.StringComparison.OrdinalIgnoreCase) ||
                          roles.Any(r => r.Equals("Administrador", System.StringComparison.OrdinalIgnoreCase));

            if (esAdmin)
            {
                return await GetAllClasesForAdminInternal();
            }

            var esDocente = User.IsInRole("Docente") ||
                roles.Any(r =>
                    r.Equals(
                        "Docente",
                        System.StringComparison.OrdinalIgnoreCase) ||
                    r.Equals(
                        "Profesor",
                        System.StringComparison.OrdinalIgnoreCase));

            if (!esDocente)
                return Forbid();

            return await GetClasesDocenteInternal(user.Id);
        }

        // GET /api/Docente/{id}/clases
        [HttpGet("{id}/clases")]
        [Authorize(Roles = "Docente,Administrador")]
        public async Task<IActionResult> GetClasesDocentePorId(long id)
        {
            var callerRoleClaim = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (id == 0 && (User.IsInRole("Administrador") || callerRoleClaim.Equals("Administrador", System.StringComparison.OrdinalIgnoreCase)))
            {
                return await GetAllClasesForAdminInternal();
            }
            return await GetClasesDocenteInternal(id);
        }

        // =========================================================
        // MATERIAS DEL DOCENTE
        // =========================================================

        // GET /api/Docente/{id}/materias
        [HttpGet("{id}/materias")]
        public async Task<IActionResult> GetMateriasPorDocente(long id)
        {
            if (id > int.MaxValue)
                return Ok(new List<object>());

            int docId = (int)id;

            var materias = await _context.Materias
                .Include(m => m.DocenteResponsable)
                .ThenInclude(d => d.Persona)
                .Where(m => m.DocenteResponsableId == docId)
                .Select(m => new
                {
                    id = m.Id,
                    nombre = m.Nombre,
                    codigo = m.Codigo,
                    descripcion = m.Descripcion,
                    docenteId = m.DocenteResponsableId,
                    nombreDocente =
                        m.DocenteResponsable != null &&
                        m.DocenteResponsable.Persona != null
                            ? $"{m.DocenteResponsable.Persona.Nombre} {m.DocenteResponsable.Persona.Apellido}"
                            : ""
                })
                .ToListAsync();

            var catedras = await _context.Catedras
                .Include(c => c.Docente)
                .ThenInclude(d => d.Persona)
                .Where(c => c.DocenteId == docId)
                .Select(c => new
                {
                    id = c.Id,
                    nombre = c.Nombre,
                    codigo = $"CAT-{c.Id}",
                    descripcion = "CÃ¡tedra",
                    docenteId = c.DocenteId,
                    nombreDocente =
                        c.Docente != null &&
                        c.Docente.Persona != null
                            ? $"{c.Docente.Persona.Nombre} {c.Docente.Persona.Apellido}"
                            : ""
                })
                .ToListAsync();

            var combined = materias
                .Concat(catedras)
                .ToList();

            return Ok(combined);
        }

        // =========================================================
        // RF-016 - MATERIAS ACTIVAS DEL DOCENTE AUTENTICADO
        // =========================================================

        // GET /api/Docente/materias-activas
        [HttpGet("materias-activas")]
        [Authorize(Roles = "Docente")]
        public async Task<IActionResult> GetMateriasActivasDocente()
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(value, out var docenteId))
            {
                return Unauthorized(new
                {
                    message = "Usuario no autenticado."
                });
            }

            var docente = await _context.Users
                .Include(u => u.Persona)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == docenteId);

            if (docente == null)
            {
                return Unauthorized(new
                {
                    message = "Usuario no encontrado."
                });
            }

            var roles = docente.Persona?.GetRoles()
                        ?? new List<string>();

            var esDocente = User.IsInRole("Docente") ||
                            roles.Any(r =>
                                r.Equals(
                                    "Docente",
                                    System.StringComparison.OrdinalIgnoreCase) ||
                                r.Equals(
                                    "Profesor",
                                    System.StringComparison.OrdinalIgnoreCase));

            if (!esDocente)
                return Forbid();

            var catedrasDocente = await _context.Catedras
                .AsNoTracking()
                .Where(c => c.DocenteId == docenteId)
                .ToListAsync();

            if (catedrasDocente.Count == 0)
                return Ok(new List<object>());

            // Mientras el modelo no tenga una entidad de periodo académico
            // con bandera "Activo", se toma como vigente el semestre más
            // reciente asignado al docente.
            var semestreActual = catedrasDocente
                .Where(c => !string.IsNullOrWhiteSpace(c.Semestre))
                .Select(c => c.Semestre.Trim())
                .OrderByDescending(s => s)
                .FirstOrDefault();

            var catedrasActivas = string.IsNullOrWhiteSpace(semestreActual)
                ? catedrasDocente
                : catedrasDocente
                    .Where(c => string.Equals(
                        c.Semestre?.Trim(),
                        semestreActual,
                        System.StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var catedraIds = catedrasActivas
                .Select(c => c.Id)
                .ToList();

            var inscripciones = await _context.Inscripciones
                .AsNoTracking()
                .Where(i =>
                    i.CatedraId.HasValue &&
                    catedraIds.Contains(i.CatedraId.Value) &&
                    i.ClaseId.HasValue)
                .Select(i => new
                {
                    CatedraId = i.CatedraId!.Value,
                    ClaseId = i.ClaseId!.Value
                })
                .Distinct()
                .ToListAsync();

            var materiasDocente = await _context.Materias
                .AsNoTracking()
                .Where(m => m.DocenteResponsableId == docenteId)
                .ToListAsync();

            var clasesDocente = await _context.Clases
                .Include(c => c.Materia)
                .AsNoTracking()
                .Where(c => c.DocenteId == docenteId)
                .ToListAsync();

            var resultado = catedrasActivas
                .OrderBy(c => c.Nombre)
                .Select(catedra =>
                {
                    var claseIdsRelacionadas = inscripciones
                        .Where(i => i.CatedraId == catedra.Id)
                        .Select(i => i.ClaseId)
                        .Distinct()
                        .ToHashSet();

                    var clasesRelacionadas = clasesDocente
                        .Where(c => claseIdsRelacionadas.Contains(c.Id))
                        .ToList();

                    var materiaRelacionada = clasesRelacionadas
                        .Where(c => c.Materia != null)
                        .Select(c => c.Materia)
                        .FirstOrDefault();

                    if (materiaRelacionada == null)
                    {
                        materiaRelacionada = materiasDocente
                            .FirstOrDefault(m =>
                                string.Equals(
                                    m.Nombre?.Trim(),
                                    catedra.Nombre?.Trim(),
                                    System.StringComparison.OrdinalIgnoreCase));
                    }

                    if (materiaRelacionada != null)
                    {
                        clasesRelacionadas = clasesDocente
                            .Where(c => c.MateriaId == materiaRelacionada.Id)
                            .OrderBy(c => c.Nombre)
                            .ToList();
                    }
                    else
                    {
                        clasesRelacionadas = clasesRelacionadas
                            .OrderBy(c => c.Nombre)
                            .ToList();
                    }

                    return new
                    {
                        catedraId = catedra.Id,
                        materiaId = materiaRelacionada != null
                            ? (int?)materiaRelacionada.Id
                            : null,
                        nombre = catedra.Nombre,
                        codigo = materiaRelacionada?.Codigo
                                 ?? $"CAT-{catedra.Id}",
                        descripcion = materiaRelacionada?.Descripcion
                                      ?? string.Empty,
                        semestre = catedra.Semestre,
                        docenteId = catedra.DocenteId,
                        clases = clasesRelacionadas.Select(clase => new
                        {
                            claseId = clase.Id,
                            nombre = clase.Nombre,
                            materiaId = clase.MateriaId
                        }).ToList()
                    };
                })
                .ToList();

            return Ok(resultado);
        }

        // =========================================================
        // OBTENER CLASES INTERNAMENTE
        // =========================================================
       private async Task<IActionResult> GetClasesDocenteInternal(
           long docenteId)
       {
           var doc = await GetDefaultDocenteAsync(docenteId);

           int targetDocId = doc != null
               ? doc.Id
               : (int)docenteId;



            var clases = await _context.Clases
                .Include(c => c.Materia)
                .Include(c => c.Docente)
                    .ThenInclude(d => d.Persona)
                .Include(c => c.Estudiantes)
                    .ThenInclude(e => e.Persona)
                .Where(c => c.DocenteId == targetDocId)
                .Select(c => new
                {
                    id = c.Id,
                    claseId = c.Id,
                    nombre = c.Nombre,
                    materiaId = c.MateriaId,

                    // Cátedra real relacionada con la clase
                    catedraId = _context.Inscripciones
                        .Where(i => i.ClaseId == c.Id)
                        .Select(i => (int?)i.CatedraId)
                        .FirstOrDefault(),

                   materia = c.Materia != null
                       ? c.Materia.Nombre
                       : "",

                   nombreMateria = c.Materia != null
                       ? c.Materia.Nombre
                       : "",

                   codigoMateria = c.Materia != null
                       ? c.Materia.Codigo
                       : "",

                   docenteId = c.DocenteId,

                   docente =
                       c.Docente != null &&
                       c.Docente.Persona != null
                           ? $"{c.Docente.Persona.Nombre} " +
                             $"{c.Docente.Persona.Apellido}".Trim()
                           : (c.Docente != null
                               ? c.Docente.Username
                               : "Docente"),

                   docenteNombre =
                       c.Docente != null &&
                       c.Docente.Persona != null
                           ? $"{c.Docente.Persona.Nombre} " +
                             $"{c.Docente.Persona.Apellido}".Trim()
                           : (c.Docente != null
                               ? c.Docente.Username
                               : "Docente"),

                   docenteEmail =
                       c.Docente != null &&
                       c.Docente.Persona != null
                           ? c.Docente.Persona.Correo
                           : (c.Docente != null
                               ? c.Docente.Username
                               : ""),

                   aula = "Aula Principal",
                   horario = "Horario Regular",
                   paralelo = "A",

                   estudiantesCount = c.Estudiantes.Count(e =>
                        e.Persona != null &&
                        e.Persona.Rol.Contains("Estudiante")),

                    estudianteIds = c.Estudiantes
                        .Where(e =>
                            e.Persona != null &&
                            e.Persona.Rol.Contains("Estudiante"))
                        .Select(e => e.Id)
                        .ToList(),

                    estudiantes = c.Estudiantes
                        .Where(e =>
                            e.Persona != null &&
                            e.Persona.Rol.Contains("Estudiante"))
                        .Select(e => new
                        {
                           id = e.Id,
                           estudianteId = e.Id,
                           username = e.Username,

                           nombreCompleto =
                               e.Persona != null
                                   ? $"{e.Persona.Nombre} " +
                                     $"{e.Persona.Apellido}".Trim()
                                   : e.Username,

                           nombre =
                               e.Persona != null
                                   ? $"{e.Persona.Nombre} " +
                                     $"{e.Persona.Apellido}".Trim()
                                   : e.Username,

                           correo =
                               e.Persona != null
                                   ? e.Persona.Correo
                                   : string.Empty,

                           cedula =
                               e.Persona != null
                                   ? (e.Persona.Cedula ??
                                      string.Empty)
                                   : string.Empty,

                           // RF-001: cátedra de la inscripción
                           catedraId = _context.Inscripciones
                               .Where(i =>
                                   i.ClaseId == c.Id &&
                                   i.EstudianteId == e.Id)
                               .Select(i => (int?)i.CatedraId)
                               .FirstOrDefault(),

                           // RF-001: promedio real guardado
                           promedioActual = _context.Inscripciones
                               .Where(i =>
                                   i.ClaseId == c.Id &&
                                   i.EstudianteId == e.Id)
                               .Select(i => (double?)i.PromedioActual)
                               .FirstOrDefault(),

                           // RF-001: alerta real guardada
                           alertaRendimiento = _context.Inscripciones
                               .Where(i =>
                                   i.ClaseId == c.Id &&
                                   i.EstudianteId == e.Id)
                               .Select(i => (bool?)i.AlertaRendimiento)
                               .FirstOrDefault()
                       })
                       .ToList()
               })
               .ToListAsync();

           return Ok(
               clases
                   .DistinctBy(c => c.id)
                   .ToList());
       }

        private async Task<IActionResult> GetAllClasesForAdminInternal()
        {
            var clases = await _context.Clases
                .Include(c => c.Materia)
                .Include(c => c.Docente)
                .ThenInclude(d => d.Persona)
                .Include(c => c.Estudiantes)
                .ThenInclude(e => e.Persona)
                .Select(c => new
                {
                    id = c.Id,
                    claseId = c.Id,
                    nombre = c.Nombre,
                    materiaId = c.MateriaId,

                    materia = c.Materia != null
                        ? c.Materia.Nombre
                        : "",

                    nombreMateria = c.Materia != null
                        ? c.Materia.Nombre
                        : "",

                    codigoMateria = c.Materia != null
                        ? c.Materia.Codigo
                        : "",

                    docenteId = c.DocenteId,

                    docente =
                        c.Docente != null &&
                        c.Docente.Persona != null
                            ? $"{c.Docente.Persona.Nombre} {c.Docente.Persona.Apellido}".Trim()
                            : c.Docente != null
                                ? c.Docente.Username
                                : "Docente",

                    docenteNombre =
                        c.Docente != null &&
                        c.Docente.Persona != null
                            ? $"{c.Docente.Persona.Nombre} {c.Docente.Persona.Apellido}".Trim()
                            : c.Docente != null
                                ? c.Docente.Username
                                : "Docente",

                    docenteEmail =
                        c.Docente != null &&
                        c.Docente.Persona != null
                            ? c.Docente.Persona.Correo
                            : c.Docente != null
                                ? c.Docente.Username
                                : "",

                    aula = "Aula Principal",
                    horario = "Horario Regular",
                    paralelo = "A",

                    estudiantesCount = c.Estudiantes.Count(e =>
                        e.Persona != null &&
                        e.Persona.Rol.Contains("Estudiante")),

                    estudianteIds = c.Estudiantes
                        .Where(e =>
                            e.Persona != null &&
                            e.Persona.Rol.Contains("Estudiante"))
                        .Select(e => e.Id)
                        .ToList(),

                    estudiantes = c.Estudiantes
                        .Where(e =>
                            e.Persona != null &&
                            e.Persona.Rol.Contains("Estudiante"))
                        .Select(e => new
                        {
                            id = e.Id,
                            estudianteId = e.Id,
                            username = e.Username,

                            nombreCompleto =
                                e.Persona != null
                                    ? $"{e.Persona.Nombre} {e.Persona.Apellido}".Trim()
                                    : e.Username,

                            nombre =
                                e.Persona != null
                                    ? $"{e.Persona.Nombre} {e.Persona.Apellido}".Trim()
                                    : e.Username,

                            correo =
                                e.Persona != null
                                    ? e.Persona.Correo
                                    : string.Empty,

                            cedula =
                                e.Persona != null
                                    ? e.Persona.Cedula ?? string.Empty
                                    : string.Empty,

                            catedraId = _context.Inscripciones
                                .Where(i =>
                                    i.ClaseId == c.Id &&
                                    i.EstudianteId == e.Id)
                                .Select(i => (int?)i.CatedraId)
                                .FirstOrDefault(),

                            promedioActual = _context.Inscripciones
                                .Where(i =>
                                    i.ClaseId == c.Id &&
                                    i.EstudianteId == e.Id)
                                .Select(i => (double?)i.PromedioActual)
                                .FirstOrDefault(),

                            alertaRendimiento = _context.Inscripciones
                                .Where(i =>
                                    i.ClaseId == c.Id &&
                                    i.EstudianteId == e.Id)
                                .Select(i => (bool?)i.AlertaRendimiento)
                                .FirstOrDefault()
                        })
                        .ToList()
                })
                .ToListAsync();

            return Ok(
                clases
                    .DistinctBy(c => c.id)
                    .ToList());
        }

        // =========================================================
        // EVALUACIÃ“N DIAGNÃ“STICA
        // =========================================================

       // =========================================================
       // RF-004 - EVALUACIÓN DIAGNÓSTICA
       // =========================================================

       [HttpPost("catedras/{catedraId}/evaluacion-diagnostica")]
       public async Task<IActionResult> RegistrarEvaluacionDiagnostica(
           int catedraId,
           [FromBody] EvaluacionDto evaluacionDto)
       {
           if (evaluacionDto == null)
           {
               return BadRequest(new
               {
                   message = "Los datos de la evaluación son obligatorios."
               });
           }

           if (string.IsNullOrWhiteSpace(evaluacionDto.Nombre))
           {
               return BadRequest(new
               {
                   message = "El nombre de la evaluación es obligatorio."
               });
           }

           if (!evaluacionDto.FechaInicio.HasValue)
           {
               return BadRequest(new
               {
                   message = "La fecha de inicio es obligatoria."
               });
           }

           if (!evaluacionDto.FechaFin.HasValue)
           {
               return BadRequest(new
               {
                   message = "La fecha de finalización es obligatoria."
               });
           }

           if (evaluacionDto.FechaFin.Value <=
               evaluacionDto.FechaInicio.Value)
           {
               return BadRequest(new
               {
                   message =
                       "La fecha de finalización debe ser posterior a la fecha de inicio."
               });
           }

           var tipoEvaluacion =
               evaluacionDto.TipoEvaluacion?.Trim();

           if (string.IsNullOrWhiteSpace(tipoEvaluacion))
           {
               return BadRequest(new
               {
                   message = "Debes seleccionar el tipo de evaluación."
               });
           }

           var tiposValidos = new[]
           {
               "Archivo",
               "Cuestionario"
           };

           var tipoNormalizado =
               tiposValidos.FirstOrDefault(t =>
                   t.Equals(
                       tipoEvaluacion,
                       System.StringComparison.OrdinalIgnoreCase));

           if (tipoNormalizado == null)
           {
               return BadRequest(new
               {
                   message =
                       "El tipo de evaluación debe ser Archivo o Cuestionario."
               });
           }

           if (tipoNormalizado == "Cuestionario" &&
               string.IsNullOrWhiteSpace(evaluacionDto.PreguntasCuestionario))
           {
               return BadRequest(new
               {
                   message = "Debes agregar al menos una pregunta al cuestionario."
               });
           }

           var userIdClaim =
               User.FindFirst(
                   ClaimTypes.NameIdentifier)?.Value;

           if (!int.TryParse(userIdClaim, out var docenteId))
           {
               return Unauthorized(new
               {
                   message = "Usuario no autenticado."
               });
           }

           var catedra =
               await _context.Catedras
                   .FirstOrDefaultAsync(c =>
                       c.Id == catedraId);

           if (catedra == null)
           {
               return NotFound(new
               {
                   message = "La cátedra indicada no existe."
               });
           }

           if (catedra.DocenteId != docenteId)
           {
               return Forbid();
           }

           var evaluacion = new Evaluacion
           {
               Nombre =
                   evaluacionDto.Nombre.Trim(),

               CatedraId =
                   catedraId,

               EsDiagnostica =
                   true,

               FechaInicio =
                   evaluacionDto.FechaInicio,

               FechaFin =
                   evaluacionDto.FechaFin,

               TipoEvaluacion =
                   tipoNormalizado,

               Instrucciones =
                   evaluacionDto.Instrucciones?.Trim()
                   ?? string.Empty,

               ArchivoDocenteUrl =
                   string.IsNullOrWhiteSpace(
                       evaluacionDto.ArchivoDocenteUrl)
                       ? null
                       : evaluacionDto.ArchivoDocenteUrl.Trim(),

               PreguntasCuestionario =
                   tipoNormalizado == "Cuestionario"
                       ? evaluacionDto.PreguntasCuestionario
                       : null
           };

           _context.Evaluaciones.Add(evaluacion);

           await _context.SaveChangesAsync();

           var respuesta = new EvaluacionDto
           {
               Id =
                   evaluacion.Id,

               Nombre =
                   evaluacion.Nombre,

               CatedraId =
                   evaluacion.CatedraId,

               EsDiagnostica =
                   true,

               FechaInicio =
                   evaluacion.FechaInicio,

               FechaFin =
                   evaluacion.FechaFin,

               TipoEvaluacion =
                   evaluacion.TipoEvaluacion,

               Instrucciones =
                   evaluacion.Instrucciones,

               ArchivoDocenteUrl =
                   evaluacion.ArchivoDocenteUrl,

               PreguntasCuestionario =
                   evaluacion.PreguntasCuestionario
           };

           return StatusCode(
               StatusCodes.Status201Created,
               respuesta);
       }


       // GET /api/Docente/catedras/{catedraId}/evaluaciones-diagnosticas
       [HttpGet("catedras/{catedraId}/evaluaciones-diagnosticas")]
       public async Task<IActionResult> ObtenerEvaluacionesDiagnosticas(
           int catedraId)
       {
           var userIdClaim =
               User.FindFirst(
                   ClaimTypes.NameIdentifier)?.Value;

           if (!int.TryParse(userIdClaim, out var docenteId))
           {
               return Unauthorized(new
               {
                   message = "Usuario no autenticado."
               });
           }

           var catedra =
               await _context.Catedras
                   .FirstOrDefaultAsync(c =>
                       c.Id == catedraId);

           if (catedra == null)
           {
               return NotFound(new
               {
                   message = "La cátedra indicada no existe."
               });
           }

           if (catedra.DocenteId != docenteId)
           {
               return Forbid();
           }

           var evaluaciones =
               await _context.Evaluaciones
                   .Where(e =>
                       e.CatedraId == catedraId &&
                       e.EsDiagnostica)
                   .OrderByDescending(e => e.Id)
                   .Select(e => new EvaluacionDto
                   {
                       Id = e.Id,

                       Nombre = e.Nombre,

                       CatedraId = e.CatedraId,

                       EsDiagnostica = e.EsDiagnostica,

                       FechaInicio = e.FechaInicio,

                       FechaFin = e.FechaFin,

                       TipoEvaluacion = e.TipoEvaluacion,

                       Instrucciones = e.Instrucciones,

                       ArchivoDocenteUrl =
                           e.ArchivoDocenteUrl,

                       PreguntasCuestionario =
                           e.PreguntasCuestionario
                   })
                   .ToListAsync();

           return Ok(evaluaciones);
       }



       // POST /api/Docente/catedras/{catedraId}/evaluaciones-diagnosticas/{evaluacionId}/resultados
       [HttpPost(
           "catedras/{catedraId}/evaluaciones-diagnosticas/{evaluacionId}/resultados")]
       public async Task<IActionResult> GuardarResultadosEvaluacionDiagnostica(
           int catedraId,
           int evaluacionId,
           [FromBody] RegistrarResultadosDiagnosticosDto dto)
       {
           var userIdClaim =
               User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

           if (!int.TryParse(userIdClaim, out var docenteId))
           {
               return Unauthorized(new
               {
                   message = "Usuario no autenticado."
               });
           }

           var catedra = await _context.Catedras
               .FirstOrDefaultAsync(c => c.Id == catedraId);

           if (catedra == null)
           {
               return NotFound(new
               {
                   message = "Cátedra no encontrada."
               });
           }

           if (catedra.DocenteId != docenteId)
           {
               return Forbid();
           }

           var evaluacion = await _context.Evaluaciones
               .FirstOrDefaultAsync(e =>
                   e.Id == evaluacionId &&
                   e.CatedraId == catedraId &&
                   e.EsDiagnostica);

           if (evaluacion == null)
           {
               return NotFound(new
               {
                   message = "Evaluación diagnóstica no encontrada."
               });
           }

           if (dto == null ||
               dto.Resultados == null ||
               dto.Resultados.Count == 0)
           {
               return BadRequest(new
               {
                   message = "Debe registrar al menos un resultado."
               });
           }

           var estudiantesDuplicados = dto.Resultados
               .GroupBy(r => r.EstudianteId)
               .Where(g => g.Count() > 1)
               .Select(g => g.Key)
               .ToList();

           if (estudiantesDuplicados.Count > 0)
           {
               return BadRequest(new
               {
                   message =
                       "No se puede registrar dos veces al mismo estudiante."
               });
           }

           foreach (var resultado in dto.Resultados)
           {
               if (resultado.Calificacion.HasValue &&
                   (resultado.Calificacion.Value < 0 ||
                    resultado.Calificacion.Value > 10))
               {
                   return BadRequest(new
                   {
                       message =
                           "Las calificaciones deben estar entre 0 y 10."
                   });
               }

               var estaInscrito =
                   await _context.Inscripciones
                       .AnyAsync(i =>
                           i.EstudianteId ==
                           resultado.EstudianteId &&
                           i.CatedraId == catedraId);

               if (!estaInscrito)
               {
                   return BadRequest(new
                   {
                       message =
                           $"El estudiante {resultado.EstudianteId} no está inscrito en la cátedra."
                   });
               }

               var resultadoExistente =
                   await _context.ResultadosEvaluacionesDiagnosticas
                       .FirstOrDefaultAsync(r =>
                           r.EvaluacionId == evaluacionId &&
                           r.EstudianteId ==
                           resultado.EstudianteId);

               if (resultadoExistente == null)
               {
                   var nuevoResultado =
                       new ResultadoEvaluacionDiagnostica
                       {
                           EvaluacionId = evaluacionId,
                           EstudianteId =
                               resultado.EstudianteId,
                           Calificacion =
                               resultado.Calificacion,
                           Observacion =
                               resultado.Observacion?.Trim()
                               ?? string.Empty,
                           Estado =
                               resultado.Calificacion.HasValue
                                   ? "Calificado"
                                   : "Pendiente",
                           FechaRegistro =
                               System.DateTime.UtcNow
                       };

                   _context
                       .ResultadosEvaluacionesDiagnosticas
                       .Add(nuevoResultado);
               }
               else
               {
                   resultadoExistente.Calificacion =
                       resultado.Calificacion;

                   resultadoExistente.Observacion =
                       resultado.Observacion?.Trim()
                       ?? string.Empty;

                   resultadoExistente.Estado =
                       resultado.Calificacion.HasValue
                           ? "Calificado"
                           : resultadoExistente.Estado;

                   resultadoExistente.FechaRegistro =
                       System.DateTime.UtcNow;
               }
           }

           await _context.SaveChangesAsync();

           var resultadosGuardados =
               await _context.ResultadosEvaluacionesDiagnosticas
                   .Where(r =>
                       r.EvaluacionId == evaluacionId)
                   .ToListAsync();

         var calificacionesValidas = resultadosGuardados
             .Where(r => r.Calificacion.HasValue)
             .Select(r => r.Calificacion!.Value)
             .ToList();

         var promedio = calificacionesValidas.Count > 0
             ? calificacionesValidas.Average()
             : 0;

           return Ok(new
           {
               message =
                   "Resultados diagnósticos registrados correctamente.",

               evaluacionId,

              estudiantesEvaluados =
                  calificacionesValidas.Count,

              promedioDiagnostico =
                  System.Math.Round(promedio, 2),

              calificacionMinima =
                  calificacionesValidas.Count > 0
                      ? calificacionesValidas.Min()
                      : 0,

              calificacionMaxima =
                  calificacionesValidas.Count > 0
                      ? calificacionesValidas.Max()
                      : 0,

              afectaPromedioAcademico = false
           });
       }

       // GET /api/Docente/catedras/{catedraId}/evaluaciones-diagnosticas/{evaluacionId}/resultados
       [HttpGet(
           "catedras/{catedraId}/evaluaciones-diagnosticas/{evaluacionId}/resultados")]
       public async Task<IActionResult> ObtenerResultadosEvaluacionDiagnostica(
           int catedraId,
           int evaluacionId)
       {
           var userIdClaim =
               User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

           if (!int.TryParse(userIdClaim, out var docenteId))
           {
               return Unauthorized(new
               {
                   message = "Usuario no autenticado."
               });
           }

           var catedra = await _context.Catedras
               .FirstOrDefaultAsync(c => c.Id == catedraId);

           if (catedra == null)
           {
               return NotFound(new
               {
                   message = "Cátedra no encontrada."
               });
           }

           if (catedra.DocenteId != docenteId)
           {
               return Forbid();
           }

           var evaluacion = await _context.Evaluaciones
               .FirstOrDefaultAsync(e =>
                   e.Id == evaluacionId &&
                   e.CatedraId == catedraId &&
                   e.EsDiagnostica);

           if (evaluacion == null)
           {
               return NotFound(new
               {
                   message = "Evaluación diagnóstica no encontrada."
               });
           }

           var resultados =
               await _context.ResultadosEvaluacionesDiagnosticas
                   .Where(r =>
                       r.EvaluacionId == evaluacionId)
                   .Include(r => r.Estudiante)
                       .ThenInclude(e => e.Persona)
                   .OrderBy(r =>
                       r.Estudiante.Persona != null
                           ? r.Estudiante.Persona.Apellido
                           : r.Estudiante.Username)
                   .Select(r =>
                       new ResultadoEvaluacionDiagnosticaDto
                       {
                           Id = r.Id,
                           EvaluacionId = r.EvaluacionId,
                           EstudianteId = r.EstudianteId,

                           NombreEstudiante =
                               r.Estudiante.Persona != null
                                   ? $"{r.Estudiante.Persona.Nombre} {r.Estudiante.Persona.Apellido}".Trim()
                                   : r.Estudiante.Username,

                           Calificacion = r.Calificacion,
                           Observacion = r.Observacion,
                           ArchivoEntregaUrl = r.ArchivoEntregaUrl,
                           FechaEntrega = r.FechaEntrega,
                           Estado = r.Estado,
                           RespuestasCuestionario =
                               r.RespuestasCuestionario,
                           FechaRegistro = r.FechaRegistro
                       })
                   .ToListAsync();

           var calificacionesValidas = resultados
               .Where(r => r.Calificacion.HasValue)
               .Select(r => r.Calificacion!.Value)
               .ToList();

           var promedio = calificacionesValidas.Count > 0
               ? calificacionesValidas.Average()
               : 0;

           return Ok(new
           {
               evaluacion = new
               {
                   evaluacion.Id,
                   evaluacion.Nombre,
                   evaluacion.CatedraId,
                   evaluacion.EsDiagnostica,
                   evaluacion.FechaInicio,
                   evaluacion.FechaFin,
                   evaluacion.TipoEvaluacion,
                   evaluacion.Instrucciones,
                   evaluacion.ArchivoDocenteUrl,
                   evaluacion.PreguntasCuestionario
               },

               resultados,

               resumen = new
               {
                   estudiantesEvaluados =
                       calificacionesValidas.Count,

                   promedioDiagnostico =
                       System.Math.Round(promedio, 2),

                   calificacionMinima =
                       calificacionesValidas.Count > 0
                           ? calificacionesValidas.Min()
                           : 0,

                   calificacionMaxima =
                       calificacionesValidas.Count > 0
                           ? calificacionesValidas.Max()
                           : 0,

                   afectaPromedioAcademico = false
               }
           });
       }

        // =========================================================
        // CRONOGRAMA
        // =========================================================



        // =========================================================
        // AYUDANTES ASIGNADOS POR CÁTEDRA
        // Soporte de integración frontend para RF-008 / RF-010 / RF-024
        // =========================================================

        // GET /api/Docente/catedras/{catedraId}/ayudantes
        // Devuelve los ayudantes reales asignados a una cátedra del docente autenticado.
        [HttpGet("catedras/{catedraId}/ayudantes")]
        [Authorize(Roles = "Docente")]
        public async Task<IActionResult> ObtenerAyudantesPorCatedra(int catedraId)
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(value, out var docenteId))
                return Unauthorized(new { message = "Usuario no autenticado." });

            var catedra = await _context.Catedras
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == catedraId);

            if (catedra == null)
                return NotFound(new { message = "Cátedra no encontrada." });

            if (catedra.DocenteId != docenteId)
            {
                return StatusCode(403, new
                {
                    message = "Solo el docente responsable puede consultar los ayudantes de esta cátedra."
                });
            }

            var ayudantes = await _context.Ayudantias
                .Include(a => a.Estudiante)
                    .ThenInclude(e => e.Persona)
                .AsNoTracking()
                .Where(a =>
                    a.CatedraId == catedraId &&
                    (a.Estado == "Aprobada" ||
                     a.Estado == "Asignada" ||
                     a.Estado == "Activa"))
                .OrderBy(a => a.Id)
                .Select(a => new
                {
                    id = a.EstudianteId,
                    ayudantiaId = a.Id,
                    catedraId = a.CatedraId,
                    estudianteId = a.EstudianteId,
                    nombre = a.Estudiante != null && a.Estudiante.Persona != null
                        ? (a.Estudiante.Persona.Nombre + " " + a.Estudiante.Persona.Apellido).Trim()
                        : a.Estudiante != null
                            ? a.Estudiante.Username
                            : "Ayudante",
                    correo = a.Estudiante != null && a.Estudiante.Persona != null
                        ? a.Estudiante.Persona.Correo
                        : string.Empty,
                    estado = a.Estado
                })
                .ToListAsync();

            return Ok(ayudantes);
        }

        // =========================================================
        // RF-008 - PLANIFICACIÃ“N DE AYUDANTÃAS
        // =========================================================

        // GET /api/Docente/ayudantias/{ayudantiaId}/planificacion
        // Docente responsable o ayudante asignado pueden consultar la planificaciÃ³n.
        [HttpGet("ayudantias/{ayudantiaId}/planificacion")]
        public async Task<IActionResult> ObtenerPlanificacionAyudantia(int ayudantiaId)
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(value, out var userId))
                return Unauthorized();

            var ayudantia = await _context.Ayudantias
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == ayudantiaId);

            if (ayudantia == null)
                return NotFound(new { message = "AyudantÃ­a no encontrada." });

            var esDocenteResponsable = await _context.Catedras
                .AsNoTracking()
                .AnyAsync(c => c.Id == ayudantia.CatedraId && c.DocenteId == userId);

            var esAyudanteAsignado = ayudantia.EstudianteId == userId;

            if (!esDocenteResponsable && !esAyudanteAsignado)
            {
                return StatusCode(403, new
                {
                    message = "No tienes permiso para consultar la planificaciÃ³n de esta ayudantÃ­a."
                });
            }

            var planificacion = await _context.ActividadesAyudantia
                .AsNoTracking()
                .Where(a => a.AyudantiaId == ayudantiaId)
                .OrderBy(a => a.FechaPlanificada)
                .Select(a => new ActividadAyudantiaDto
                {
                    Id = a.Id,
                    AyudantiaId = a.AyudantiaId,
                    Descripcion = a.Descripcion,
                    FechaPlanificada = a.FechaPlanificada,
                    Completada = a.Completada
                })
                .ToListAsync();

            return Ok(planificacion);
        }

        // POST /api/Docente/ayudantias/{ayudantiaId}/planificacion
        // Solo el docente responsable de la cÃ¡tedra puede planificar.
        [HttpPost("ayudantias/{ayudantiaId}/planificacion")]
        public async Task<IActionResult> PlanificarActividadesAyudantia(
            int ayudantiaId,
            [FromBody] ActividadAyudantiaDto actividadDto)
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(value, out var docenteId))
                return Unauthorized();

            if (actividadDto == null)
                return BadRequest(new { message = "Datos de planificaciÃ³n requeridos." });

            if (string.IsNullOrWhiteSpace(actividadDto.Descripcion))
                return BadRequest(new { message = "Debe indicar el tema o descripciÃ³n de la actividad." });

            if (actividadDto.FechaPlanificada == default)
                return BadRequest(new { message = "Debe indicar la fecha y horario de la actividad." });

            var ayudantia = await _context.Ayudantias
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == ayudantiaId);

            if (ayudantia == null)
                return NotFound(new { message = "AyudantÃ­a no encontrada." });

            var esDocenteResponsable = await _context.Catedras
                .AsNoTracking()
                .AnyAsync(c => c.Id == ayudantia.CatedraId && c.DocenteId == docenteId);

            if (!esDocenteResponsable)
            {
                return StatusCode(403, new
                {
                    message = "Solo el docente responsable de la cÃ¡tedra puede planificar actividades de ayudantÃ­a."
                });
            }

            var fechaPlanificada = actividadDto.FechaPlanificada.Kind == System.DateTimeKind.Unspecified
                ? System.DateTime.SpecifyKind(actividadDto.FechaPlanificada, System.DateTimeKind.Utc)
                : actividadDto.FechaPlanificada.ToUniversalTime();

            // Con el modelo actual el horario se representa mediante FechaPlanificada
            // (fecha + hora). Se evita que el mismo ayudante tenga dos actividades
            // programadas en el mismo instante.
            var ayudantiasDelMismoAyudante = await _context.Ayudantias
                .AsNoTracking()
                .Where(a => a.EstudianteId == ayudantia.EstudianteId)
                .Select(a => a.Id)
                .ToListAsync();

            var existeConflicto = await _context.ActividadesAyudantia
                .AsNoTracking()
                .AnyAsync(a =>
                    ayudantiasDelMismoAyudante.Contains(a.AyudantiaId) &&
                    a.FechaPlanificada == fechaPlanificada);

            if (existeConflicto)
            {
                return Conflict(new
                {
                    message = "El ayudante ya tiene una actividad planificada en esa fecha y horario."
                });
            }

            var actividad = new ActividadAyudantia
            {
                AyudantiaId = ayudantiaId,
                Descripcion = actividadDto.Descripcion.Trim(),
                FechaPlanificada = fechaPlanificada,
                Completada = false
            };

            _context.ActividadesAyudantia.Add(actividad);
            await _context.SaveChangesAsync();

            actividadDto.Id = actividad.Id;
            actividadDto.AyudantiaId = actividad.AyudantiaId;
            actividadDto.Descripcion = actividad.Descripcion;
            actividadDto.FechaPlanificada = actividad.FechaPlanificada;
            actividadDto.Completada = actividad.Completada;

            return CreatedAtAction(
                nameof(ObtenerPlanificacionAyudantia),
                new { ayudantiaId = ayudantiaId },
                actividadDto);
        }

        // PUT /api/Docente/ayudantias/{ayudantiaId}/planificacion/{actividadId}
        // Permite al docente modificar una actividad programada.
        [HttpPut("ayudantias/{ayudantiaId}/planificacion/{actividadId}")]
        public async Task<IActionResult> ModificarPlanificacionAyudantia(
            int ayudantiaId,
            int actividadId,
            [FromBody] ActividadAyudantiaDto actividadDto)
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(value, out var docenteId))
                return Unauthorized();

            if (actividadDto == null)
                return BadRequest(new { message = "Datos de planificaciÃ³n requeridos." });

            if (string.IsNullOrWhiteSpace(actividadDto.Descripcion))
                return BadRequest(new { message = "Debe indicar el tema o descripciÃ³n de la actividad." });

            if (actividadDto.FechaPlanificada == default)
                return BadRequest(new { message = "Debe indicar la fecha y horario de la actividad." });

            var ayudantia = await _context.Ayudantias
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == ayudantiaId);

            if (ayudantia == null)
                return NotFound(new { message = "AyudantÃ­a no encontrada." });

            var esDocenteResponsable = await _context.Catedras
                .AsNoTracking()
                .AnyAsync(c => c.Id == ayudantia.CatedraId && c.DocenteId == docenteId);

            if (!esDocenteResponsable)
            {
                return StatusCode(403, new
                {
                    message = "Solo el docente responsable de la cÃ¡tedra puede modificar la planificaciÃ³n."
                });
            }

            var actividad = await _context.ActividadesAyudantia
                .FirstOrDefaultAsync(a => a.Id == actividadId && a.AyudantiaId == ayudantiaId);

            if (actividad == null)
                return NotFound(new { message = "Actividad planificada no encontrada." });

            var fechaPlanificada = actividadDto.FechaPlanificada.Kind == System.DateTimeKind.Unspecified
                ? System.DateTime.SpecifyKind(actividadDto.FechaPlanificada, System.DateTimeKind.Utc)
                : actividadDto.FechaPlanificada.ToUniversalTime();

            var ayudantiasDelMismoAyudante = await _context.Ayudantias
                .AsNoTracking()
                .Where(a => a.EstudianteId == ayudantia.EstudianteId)
                .Select(a => a.Id)
                .ToListAsync();

            var existeConflicto = await _context.ActividadesAyudantia
                .AsNoTracking()
                .AnyAsync(a =>
                    a.Id != actividadId &&
                    ayudantiasDelMismoAyudante.Contains(a.AyudantiaId) &&
                    a.FechaPlanificada == fechaPlanificada);

            if (existeConflicto)
            {
                return Conflict(new
                {
                    message = "El ayudante ya tiene una actividad planificada en esa fecha y horario."
                });
            }

            actividad.Descripcion = actividadDto.Descripcion.Trim();
            actividad.FechaPlanificada = fechaPlanificada;

            await _context.SaveChangesAsync();

            return Ok(new ActividadAyudantiaDto
            {
                Id = actividad.Id,
                AyudantiaId = actividad.AyudantiaId,
                Descripcion = actividad.Descripcion,
                FechaPlanificada = actividad.FechaPlanificada,
                Completada = actividad.Completada
            });
        }


// =========================================================
        // RF-010 - MONITOREO DEL CUMPLIMIENTO DE ACTIVIDADES
        // =========================================================

        // GET /api/Docente/ayudantias/{ayudantiaId}/monitoreo
        // Compara la planificación con las bitácoras registradas y calcula
        // el estado de cumplimiento sin modificar otros módulos.
        [HttpGet("ayudantias/{ayudantiaId}/monitoreo")]
        [Authorize(Roles = "Docente")]
        public async Task<IActionResult> MonitorearCumplimientoAyudante(
            int ayudantiaId)
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(value, out var docenteId))
                return Unauthorized(new { message = "Usuario no autenticado." });

            var ayudantia = await _context.Ayudantias
                .Include(a => a.Estudiante)
                    .ThenInclude(e => e.Persona)
                .Include(a => a.Planificacion)
                .Include(a => a.Bitacoras)
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == ayudantiaId);

            if (ayudantia == null)
                return NotFound(new { message = "Ayudantía no encontrada." });

            var esDocenteResponsable = await _context.Catedras
                .AsNoTracking()
                .AnyAsync(c =>
                    c.Id == ayudantia.CatedraId &&
                    c.DocenteId == docenteId);

            if (!esDocenteResponsable)
            {
                return StatusCode(403, new
                {
                    message = "Solo el docente responsable de la cátedra puede consultar el monitoreo de esta ayudantía."
                });
            }

            var planificacion = ayudantia.Planificacion
                .OrderBy(p => p.FechaPlanificada)
                .ToList();

            var bitacoras = ayudantia.Bitacoras
                .OrderByDescending(b => b.Fecha)
                .ToList();

            // El modelo actual de Bitacora no posee ActividadAyudantiaId.
            // Por eso el cumplimiento se calcula dinámicamente usando:
            // 1) el estado Completada ya registrado en la planificación, o
            // 2) una bitácora de la misma ayudantía registrada en la fecha
            //    correspondiente a la actividad planificada.
            // Así, al existir una nueva bitácora, el siguiente GET refleja
            // automáticamente el nuevo estado sin cambiar RF-007.
            var actividades = planificacion
                .Select(p =>
                {
                    var bitacoraRelacionada = bitacoras
                        .Where(b => b.Fecha.Date == p.FechaPlanificada.Date)
                        .OrderBy(b => System.Math.Abs(
                            (b.Fecha - p.FechaPlanificada).TotalMinutes))
                        .FirstOrDefault();

                    var cumplida = p.Completada || bitacoraRelacionada != null;

                    return new
                    {
                        id = p.Id,
                        ayudantiaId = p.AyudantiaId,
                        descripcion = p.Descripcion,
                        fechaPlanificada = p.FechaPlanificada,
                        completada = cumplida,
                        estadoCumplimiento = cumplida ? "Cumplida" : "Pendiente",
                        bitacora = bitacoraRelacionada == null
                            ? null
                            : new
                            {
                                id = bitacoraRelacionada.Id,
                                fecha = bitacoraRelacionada.Fecha,
                                actividadesRealizadas = bitacoraRelacionada.ActividadesRealizadas,
                                evidenciaUrl = bitacoraRelacionada.EvidenciaUrl,
                                tieneEvidencia = !string.IsNullOrWhiteSpace(bitacoraRelacionada.EvidenciaUrl)
                            }
                    };
                })
                .ToList();

            var totalActividades = actividades.Count;
            var actividadesCumplidas = actividades.Count(a => a.completada);
            var actividadesPendientes = totalActividades - actividadesCumplidas;
            var porcentajeAvance = totalActividades == 0
                ? 0
                : System.Math.Round(
                    actividadesCumplidas * 100.0 / totalActividades,
                    2);

            var nombreAyudante = ayudantia.Estudiante != null &&
                                 ayudantia.Estudiante.Persona != null
                ? $"{ayudantia.Estudiante.Persona.Nombre} {ayudantia.Estudiante.Persona.Apellido}".Trim()
                : ayudantia.Estudiante?.Username ?? "Ayudante";

            return Ok(new
            {
                ayudantiaId = ayudantia.Id,
                catedraId = ayudantia.CatedraId,
                estudianteId = ayudantia.EstudianteId,
                nombreAyudante,
                estadoAyudantia = ayudantia.Estado,

                resumen = new
                {
                    totalActividades,
                    actividadesCumplidas,
                    actividadesPendientes,
                    porcentajeAvance
                },

                actividades,

                actividadesPendientes = actividades
                    .Where(a => !a.completada)
                    .Select(a => new
                    {
                        a.id,
                        a.descripcion,
                        a.fechaPlanificada,
                        a.estadoCumplimiento
                    })
                    .ToList(),

                bitacoras = bitacoras
                    .Select(b => new
                    {
                        id = b.Id,
                        fecha = b.Fecha,
                        actividadesRealizadas = b.ActividadesRealizadas,
                        evidenciaUrl = b.EvidenciaUrl,
                        tieneEvidencia = !string.IsNullOrWhiteSpace(b.EvidenciaUrl)
                    })
                    .ToList()
            });
        }

        // =========================================================
        // RF-024 - GESTIÓN DE DISPONIBILIDAD PARA CLASES DE AYUDANTÍA
        // =========================================================

        // GET /api/Docente/ayudantias/{ayudantiaId}/disponibilidad
        // Analiza los horarios académicos de estudiantes, docente y ayudante
        // y devuelve alternativas sin conflictos. No crea ninguna sesión.
        [HttpGet("ayudantias/{ayudantiaId}/disponibilidad")]
        public async Task<IActionResult> ObtenerDisponibilidadAyudantia(
            int ayudantiaId,
            [FromQuery] int claseId,
            [FromQuery] int duracionMinutos = 60,
            [FromQuery] System.DateTime? fechaInicio = null,
            [FromQuery] System.DateTime? fechaFin = null)
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(value, out var userId))
                return Unauthorized(new { message = "Usuario no autenticado." });

            if (claseId <= 0)
                return BadRequest(new { message = "Debe indicar una clase válida." });

            if (duracionMinutos < 30 || duracionMinutos > 240 || duracionMinutos % 30 != 0)
            {
                return BadRequest(new
                {
                    message = "La duración debe estar entre 30 y 240 minutos, en bloques de 30 minutos."
                });
            }

            var ayudantia = await _context.Ayudantias
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == ayudantiaId);

            if (ayudantia == null)
                return NotFound(new { message = "Ayudantía no encontrada." });

            var catedra = await _context.Catedras
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == ayudantia.CatedraId);

            if (catedra == null)
                return NotFound(new { message = "Cátedra de la ayudantía no encontrada." });

            var esDocente = catedra.DocenteId == userId;
            var esAyudante = ayudantia.EstudianteId == userId;

            if (!esDocente && !esAyudante)
            {
                return StatusCode(403, new
                {
                    message = "Solo el docente responsable o el ayudante asignado pueden consultar la disponibilidad."
                });
            }

            var clase = await _context.Clases
                .Include(c => c.Estudiantes)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == claseId);

            if (clase == null)
                return NotFound(new { message = "Clase no encontrada." });

            if (!clase.CatedraId.HasValue || clase.CatedraId.Value != ayudantia.CatedraId)
            {
                return BadRequest(new
                {
                    message = "La clase seleccionada no pertenece a la cátedra de esta ayudantía."
                });
            }

            var periodoVigente = await _context.Catedras
                .AsNoTracking()
                .Where(c => c.Semestre != null && c.Semestre != "")
                .OrderByDescending(c => c.Semestre)
                .Select(c => c.Semestre)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(periodoVigente) &&
                !string.Equals(catedra.Semestre, periodoVigente, System.StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = $"La cátedra pertenece al periodo {catedra.Semestre}, pero el periodo académico vigente es {periodoVigente}."
                });
            }

            var desde = fechaInicio?.Date ?? System.DateTime.UtcNow.Date;
            var hasta = fechaFin?.Date ?? desde.AddDays(6);

            desde = System.DateTime.SpecifyKind(desde, System.DateTimeKind.Utc);
            hasta = System.DateTime.SpecifyKind(hasta, System.DateTimeKind.Utc);

            if (hasta < desde)
                return BadRequest(new { message = "La fecha final no puede ser anterior a la fecha inicial." });

            if ((hasta - desde).TotalDays > 31)
                return BadRequest(new { message = "El rango máximo de consulta es de 31 días." });

            var participanteIds = clase.Estudiantes
                .Select(e => e.Id)
                .ToList();

            if (!participanteIds.Contains(ayudantia.EstudianteId))
                participanteIds.Add(ayudantia.EstudianteId);

            var catedrasPeriodoIds = await _context.Catedras
                .AsNoTracking()
                .Where(c => c.Semestre == catedra.Semestre)
                .Select(c => c.Id)
                .ToListAsync();

            var clasesRelacionadas = await _context.Clases
                .Include(c => c.Estudiantes)
                .AsNoTracking()
                .Where(c =>
                    c.CatedraId.HasValue &&
                    catedrasPeriodoIds.Contains(c.CatedraId.Value) &&
                    (c.DocenteId == catedra.DocenteId ||
                     c.Estudiantes.Any(e => participanteIds.Contains(e.Id))))
                .ToListAsync();

            var clasesRelacionadasIds = clasesRelacionadas
                .Select(c => c.Id)
                .Distinct()
                .ToList();

            var sesiones = await _context.ClasesSesiones
                .AsNoTracking()
                .Where(s =>
                    s.Fecha >= desde &&
                    s.Fecha < hasta.AddDays(1) &&
                    (s.DocenteId == catedra.DocenteId ||
                     (s.ClaseId.HasValue && clasesRelacionadasIds.Contains(s.ClaseId.Value))))
                .OrderBy(s => s.Fecha)
                .ThenBy(s => s.HoraInicio)
                .ToListAsync();

            var ayudantiasDelMismoAyudante = await _context.Ayudantias
                .AsNoTracking()
                .Where(a => a.EstudianteId == ayudantia.EstudianteId)
                .Select(a => a.Id)
                .ToListAsync();

            var actividadesAyudantia = await _context.ActividadesAyudantia
                .AsNoTracking()
                .Where(a =>
                    ayudantiasDelMismoAyudante.Contains(a.AyudantiaId) &&
                    a.FechaPlanificada >= desde &&
                    a.FechaPlanificada < hasta.AddDays(1))
                .OrderBy(a => a.FechaPlanificada)
                .ToListAsync();

            var horaApertura = new System.TimeSpan(8, 0, 0);
            var horaCierre = new System.TimeSpan(18, 0, 0);
            var paso = System.TimeSpan.FromMinutes(30);
            var duracion = System.TimeSpan.FromMinutes(duracionMinutos);

            var alternativas = new List<object>();

            for (var fecha = desde; fecha <= hasta; fecha = fecha.AddDays(1))
            {
                // No se proponen domingos.
                if (fecha.DayOfWeek == System.DayOfWeek.Sunday)
                    continue;

                for (var inicio = horaApertura; inicio + duracion <= horaCierre; inicio += paso)
                {
                    var fin = inicio + duracion;

                    var conflictoSesion = sesiones.Any(s =>
                        s.Fecha.Date == fecha.Date &&
                        inicio < s.HoraFin &&
                        fin > s.HoraInicio);

                    if (conflictoSesion)
                        continue;

                    var inicioCandidato = fecha.Add(inicio);
                    var finCandidato = inicioCandidato.AddMinutes(duracionMinutos);

                    // El modelo actual de ActividadAyudantia no almacena duración.
                    // Para evitar doble reserva del ayudante se considera cada actividad
                    // planificada como un bloque de la misma duración solicitada.
                    var conflictoAyudantia = actividadesAyudantia.Any(a =>
                    {
                        var inicioExistente = a.FechaPlanificada;
                        var finExistente = inicioExistente.AddMinutes(duracionMinutos);
                        return inicioCandidato < finExistente && finCandidato > inicioExistente;
                    });

                    if (conflictoAyudantia)
                        continue;

                    alternativas.Add(new
                    {
                        fecha = fecha.ToString("yyyy-MM-dd"),
                        horaInicio = inicio.ToString(@"hh\:mm"),
                        horaFin = fin.ToString(@"hh\:mm"),
                        duracionMinutos
                    });

                    if (alternativas.Count >= 40)
                        break;
                }

                if (alternativas.Count >= 40)
                    break;
            }

            var conflictos = sesiones
                .Select(s =>
                {
                    var claseSesion = s.ClaseId.HasValue
                        ? clasesRelacionadas.FirstOrDefault(c => c.Id == s.ClaseId.Value)
                        : null;

                    var estudiantesAfectados = claseSesion == null
                        ? 0
                        : claseSesion.Estudiantes.Count(e => participanteIds.Contains(e.Id));

                    var razones = new List<string>();

                    if (s.DocenteId == catedra.DocenteId)
                        razones.Add("Docente ocupado");

                    if (estudiantesAfectados > 0)
                        razones.Add($"{estudiantesAfectados} participante(s) con clase");

                    return new
                    {
                        sesionId = s.Id,
                        claseId = s.ClaseId,
                        clase = claseSesion?.Nombre ?? "Sesión académica",
                        fecha = s.Fecha,
                        horaInicio = s.HoraInicio.ToString(@"hh\:mm"),
                        horaFin = s.HoraFin.ToString(@"hh\:mm"),
                        razones
                    };
                })
                .ToList();

            var conflictosAyudantia = actividadesAyudantia
                .Select(a => new
                {
                    actividadAyudantiaId = a.Id,
                    fecha = a.FechaPlanificada,
                    descripcion = a.Descripcion,
                    razon = "Ayudante con actividad de ayudantía ya planificada"
                })
                .ToList();

            return Ok(new
            {
                ayudantiaId,
                clase = new
                {
                    id = clase.Id,
                    nombre = clase.Nombre,
                    catedraId = clase.CatedraId
                },
                periodoAcademico = catedra.Semestre,
                duracionMinutos,
                rangoConsultado = new
                {
                    fechaInicio = desde.ToString("yyyy-MM-dd"),
                    fechaFin = hasta.ToString("yyyy-MM-dd"),
                    horaApertura = "08:00",
                    horaCierre = "18:00"
                },
                participantes = new
                {
                    estudiantesClase = clase.Estudiantes.Count,
                    ayudanteId = ayudantia.EstudianteId,
                    docenteId = catedra.DocenteId
                },
                horariosDisponibles = alternativas,
                posiblesConflictos = conflictos,
                conflictosAyudantia,
                totalAlternativas = alternativas.Count,
                sesionCreada = false,
                mensaje = alternativas.Count > 0
                    ? "Se encontraron alternativas sin conflictos. Seleccione una para continuar."
                    : "No se encontraron horarios disponibles en el rango consultado."
            });
        }

        // POST /api/Docente/ayudantias/{ayudantiaId}/disponibilidad/seleccionar
        // Valida y selecciona una alternativa. La selección NO crea una ClaseSesion;
        // la sesión real se confirma posteriormente mediante RF-019.
        [HttpPost("ayudantias/{ayudantiaId}/disponibilidad/seleccionar")]
        public async Task<IActionResult> SeleccionarHorarioAyudantia(
            int ayudantiaId,
            [FromBody] SeleccionarHorarioAyudantiaRequest request)
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(value, out var userId))
                return Unauthorized(new { message = "Usuario no autenticado." });

            if (request == null)
                return BadRequest(new { message = "Datos de horario requeridos." });

            if (request.ClaseId <= 0)
                return BadRequest(new { message = "Debe indicar una clase válida." });

            if (request.DuracionMinutos < 30 || request.DuracionMinutos > 240 || request.DuracionMinutos % 30 != 0)
            {
                return BadRequest(new
                {
                    message = "La duración debe estar entre 30 y 240 minutos, en bloques de 30 minutos."
                });
            }

            if (request.FechaHoraInicio == default)
                return BadRequest(new { message = "Debe indicar la fecha y hora seleccionadas." });

            var inicioSeleccionado = request.FechaHoraInicio.Kind == System.DateTimeKind.Unspecified
                ? System.DateTime.SpecifyKind(request.FechaHoraInicio, System.DateTimeKind.Utc)
                : request.FechaHoraInicio.ToUniversalTime();

            var finSeleccionado = inicioSeleccionado.AddMinutes(request.DuracionMinutos);

            if (inicioSeleccionado.DayOfWeek == System.DayOfWeek.Sunday)
                return BadRequest(new { message = "No se permiten horarios de ayudantía en domingo." });

            if (inicioSeleccionado.TimeOfDay < new System.TimeSpan(8, 0, 0) ||
                finSeleccionado.TimeOfDay > new System.TimeSpan(18, 0, 0))
            {
                return BadRequest(new
                {
                    message = "El horario seleccionado debe estar entre las 08:00 y las 18:00."
                });
            }

            var ayudantia = await _context.Ayudantias
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == ayudantiaId);

            if (ayudantia == null)
                return NotFound(new { message = "Ayudantía no encontrada." });

            var catedra = await _context.Catedras
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == ayudantia.CatedraId);

            if (catedra == null)
                return NotFound(new { message = "Cátedra de la ayudantía no encontrada." });

            var esDocente = catedra.DocenteId == userId;
            var esAyudante = ayudantia.EstudianteId == userId;

            if (!esDocente && !esAyudante)
            {
                return StatusCode(403, new
                {
                    message = "Solo el docente responsable o el ayudante asignado pueden seleccionar el horario."
                });
            }

            var clase = await _context.Clases
                .Include(c => c.Estudiantes)
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == request.ClaseId);

            if (clase == null)
                return NotFound(new { message = "Clase no encontrada." });

            if (!clase.CatedraId.HasValue || clase.CatedraId.Value != ayudantia.CatedraId)
            {
                return BadRequest(new
                {
                    message = "La clase seleccionada no pertenece a la cátedra de esta ayudantía."
                });
            }

            var periodoVigente = await _context.Catedras
                .AsNoTracking()
                .Where(c => c.Semestre != null && c.Semestre != "")
                .OrderByDescending(c => c.Semestre)
                .Select(c => c.Semestre)
                .FirstOrDefaultAsync();

            if (!string.IsNullOrWhiteSpace(periodoVigente) &&
                !string.Equals(catedra.Semestre, periodoVigente, System.StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = $"La cátedra pertenece al periodo {catedra.Semestre}, pero el periodo académico vigente es {periodoVigente}."
                });
            }

            var participanteIds = clase.Estudiantes.Select(e => e.Id).ToList();
            if (!participanteIds.Contains(ayudantia.EstudianteId))
                participanteIds.Add(ayudantia.EstudianteId);

            var catedrasPeriodoIds = await _context.Catedras
                .AsNoTracking()
                .Where(c => c.Semestre == catedra.Semestre)
                .Select(c => c.Id)
                .ToListAsync();

            var clasesRelacionadasIds = await _context.Clases
                .AsNoTracking()
                .Where(c =>
                    c.CatedraId.HasValue &&
                    catedrasPeriodoIds.Contains(c.CatedraId.Value) &&
                    (c.DocenteId == catedra.DocenteId ||
                     c.Estudiantes.Any(e => participanteIds.Contains(e.Id))))
                .Select(c => c.Id)
                .Distinct()
                .ToListAsync();

            var fechaDia = System.DateTime.SpecifyKind(inicioSeleccionado.Date, System.DateTimeKind.Utc);

            var conflictoSesion = await _context.ClasesSesiones
                .AsNoTracking()
                .AnyAsync(s =>
                    s.Fecha >= fechaDia &&
                    s.Fecha < fechaDia.AddDays(1) &&
                    (s.DocenteId == catedra.DocenteId ||
                     (s.ClaseId.HasValue && clasesRelacionadasIds.Contains(s.ClaseId.Value))) &&
                    inicioSeleccionado.TimeOfDay < s.HoraFin &&
                    finSeleccionado.TimeOfDay > s.HoraInicio);

            if (conflictoSesion)
            {
                return Conflict(new
                {
                    message = "El horario seleccionado presenta conflicto con una clase registrada."
                });
            }

            var ayudantiasDelMismoAyudante = await _context.Ayudantias
                .AsNoTracking()
                .Where(a => a.EstudianteId == ayudantia.EstudianteId)
                .Select(a => a.Id)
                .ToListAsync();

            var actividadesMismoDia = await _context.ActividadesAyudantia
                .AsNoTracking()
                .Where(a =>
                    ayudantiasDelMismoAyudante.Contains(a.AyudantiaId) &&
                    a.FechaPlanificada >= fechaDia &&
                    a.FechaPlanificada < fechaDia.AddDays(1))
                .ToListAsync();

            var conflictoAyudantia = actividadesMismoDia.Any(a =>
            {
                var inicioExistente = a.FechaPlanificada;
                var finExistente = inicioExistente.AddMinutes(request.DuracionMinutos);
                return inicioSeleccionado < finExistente && finSeleccionado > inicioExistente;
            });

            if (conflictoAyudantia)
            {
                return Conflict(new
                {
                    message = "El ayudante ya tiene una actividad planificada que se cruza con el horario seleccionado."
                });
            }

            return Ok(new
            {
                ayudantiaId,
                claseId = clase.Id,
                clase = clase.Nombre,
                periodoAcademico = catedra.Semestre,
                horarioSeleccionado = new
                {
                    fecha = inicioSeleccionado.ToString("yyyy-MM-dd"),
                    horaInicio = inicioSeleccionado.ToString("HH:mm"),
                    horaFin = finSeleccionado.ToString("HH:mm"),
                    duracionMinutos = request.DuracionMinutos
                },
                estado = "SeleccionadoPendienteConfirmacion",
                sesionCreada = false,
                claseSesionId = (int?)null,
                mensaje = "Horario seleccionado y validado. No se creó ninguna sesión; debe confirmarse posteriormente."
            });
        }

        public class SeleccionarHorarioAyudantiaRequest
        {
            public int ClaseId { get; set; }
            public System.DateTime FechaHoraInicio { get; set; }
            public int DuracionMinutos { get; set; } = 60;
        }

        // =========================================================
        // ENTREGAS DE ACTIVIDADES
        // =========================================================

        [HttpGet("actividades/{actividadId}/entregas")]
        public async Task<IActionResult> ObtenerEntregasPorActividad(
            int actividadId)
        {
            var actividad =
                await _context.Actividades
                    .FindAsync(actividadId);

            if (actividad == null)
                return NotFound(
                    "Actividad no encontrada.");

            var entregas = await _context
                .EstudianteActividadesRealizadas
                .Where(e =>
                    e.ActividadId == actividadId)
                .Include(e => e.Estudiante)
                .ThenInclude(u => u.Persona)
                .Select(e => new
                {
                    e.Id,
                    e.EstudianteId,

                    NombreEstudiante =
                        e.Estudiante.Persona.Nombre +
                        " " +
                        e.Estudiante.Persona.Apellido,

                    e.ArchivoUrl,
                    e.FechaRealizada,
                    e.Completada,
                    e.Calificacion,
                    e.Retroalimentacion
                })
                .ToListAsync();

            return Ok(entregas);
        }

        // =========================================================
        // CALIFICAR ENTREGA
        // =========================================================
       [HttpPost("actividades/calificar")]
       public async Task<IActionResult> CalificarEntrega(
           [FromBody] CalificarEntregaDto dto)
       {
           if (dto == null)
           {
               return BadRequest(new
               {
                   message = "Datos necesarios."
               });
           }

           // El sistema trabaja con calificaciones de 0 a 100.
           if (dto.Calificacion < 0 || dto.Calificacion > 100)
           {
               return BadRequest(new
               {
                   message = "La calificaciÃ³n debe estar entre 0 y 100."
               });
           }

           // Obtener docente autenticado.
           var userIdClaim =
               User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

           if (!int.TryParse(userIdClaim, out var docenteId))
           {
               return Unauthorized(new
               {
                   message = "Usuario no autenticado."
               });
           }

           // Buscar la entrega y su actividad.
           var entrega = await _context.EstudianteActividadesRealizadas
               .Include(e => e.Actividad)
               .FirstOrDefaultAsync(e => e.Id == dto.EntregaId);

           if (entrega == null)
           {
               return NotFound(new
               {
                   message = "Entrega no encontrada."
               });
           }

           if (entrega.Actividad == null)
           {
               return BadRequest(new
               {
                   message = "La entrega no tiene una actividad asociada."
               });
           }

           var materiaId = entrega.Actividad.MateriaId;

           // Verificar que la materia corresponda a una clase del docente.
           var claseIds = await _context.Clases
               .Where(c =>
                   c.MateriaId == materiaId &&
                   c.DocenteId == docenteId)
               .Select(c => c.Id)
               .ToListAsync();

           if (claseIds.Count == 0)
           {
               return Forbid();
           }

           // Buscar la inscripciÃ³n correspondiente al estudiante.
           var inscripcion = await _context.Inscripciones
               .Include(i => i.Catedra)
               .FirstOrDefaultAsync(i =>
                   i.EstudianteId == entrega.EstudianteId &&
                   i.ClaseId.HasValue &&
                   claseIds.Contains(i.ClaseId.Value));

           if (inscripcion == null)
           {
               return BadRequest(new
               {
                   message = "No se encontrÃ³ la inscripciÃ³n del estudiante para esta materia."
               });
           }

           // Registrar la nueva calificaciÃ³n.
           entrega.Calificacion = dto.Calificacion;
           entrega.Retroalimentacion =
               dto.Retroalimentacion ?? string.Empty;
           entrega.Completada = true;

           await _context.SaveChangesAsync();

           // Obtener todas las calificaciones reales del estudiante
           // para actividades de la misma materia.
           var calificaciones = await _context.EstudianteActividadesRealizadas
               .Where(e =>
                   e.EstudianteId == entrega.EstudianteId &&
                   e.Actividad.MateriaId == materiaId &&
                   e.Calificacion.HasValue)
               .Select(e => e.Calificacion!.Value)
               .ToListAsync();

           if (calificaciones.Count > 0)
           {
               var promedio = calificaciones.Average();

               inscripcion.PromedioActual =
                   Math.Round((double)promedio, 2);

               // RF-001:
               // activar alerta Ãºnicamente cuando ya existen calificaciones
               // y el promedio estÃ¡ debajo del umbral configurado.
               if (inscripcion.Catedra?.MinimoNota != null)
               {
                   inscripcion.AlertaRendimiento =
                       inscripcion.PromedioActual <
                       inscripcion.Catedra.MinimoNota.Value;
               }
               else
               {
                   inscripcion.AlertaRendimiento = false;
               }
           }
           else
           {
               // Sin calificaciones no se considera al estudiante en riesgo.
               inscripcion.PromedioActual = 0;
               inscripcion.AlertaRendimiento = false;
           }

           await _context.SaveChangesAsync();

           return Ok(new
           {
               message = "CalificaciÃ³n registrada correctamente.",
               entregaId = entrega.Id,
               calificacion = entrega.Calificacion,
               promedioActual = inscripcion.PromedioActual,
               umbral = inscripcion.Catedra?.MinimoNota,
               alertaRendimiento = inscripcion.AlertaRendimiento
           });
       }

        // =========================================================
        // EXPEDIENTE E HISTORIAL INTEGRAL DEL ESTUDIANTE - RF-002
        // =========================================================

        // GET /api/Docente/estudiantes/{estudianteId}/expediente
        [Authorize(Roles = "Docente,Administrador,Coordinador")]
        [HttpGet("estudiantes/{estudianteId}/expediente")]
        public async Task<IActionResult> ObtenerExpedienteEstudiante(
            int estudianteId)
        {
            var userIdClaim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(
                    userIdClaim,
                    out var docenteId))
            {
                return Unauthorized(new
                {
                    message =
                        "Usuario no autenticado."
                });
            }

            var docente =
                await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u =>
                        u.Id == docenteId);

            if (docente == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Usuario no encontrado."
                });
            }

            var rolesDocente =
                docente.Persona?.GetRoles()
                ?? new List<string>();

            var esDocente =
                rolesDocente.Any(r =>
                    r.Equals(
                        "Docente",
                        System.StringComparison.OrdinalIgnoreCase) ||
                    r.Equals(
                        "Profesor",
                        System.StringComparison.OrdinalIgnoreCase));

            var esAdminOCoordinador =
                rolesDocente.Any(r =>
                    r.Equals("Administrador", System.StringComparison.OrdinalIgnoreCase) ||
                    r.Equals("Coordinador", System.StringComparison.OrdinalIgnoreCase)) ||
                User.IsInRole("Administrador") ||
                User.IsInRole("Coordinador");

            if (!esDocente && !esAdminOCoordinador)
                return Forbid();

            var estudiante =
                await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u =>
                        u.Id == estudianteId);

            if (estudiante == null)
            {
                return NotFound(new
                {
                    message =
                        "Estudiante no encontrado."
                });
            }

            var rolesEstudiante =
                estudiante.Persona?.GetRoles()
                ?? new List<string>();

            var esEstudiante =
                rolesEstudiante.Any(r =>
                    r.Equals(
                        "Estudiante",
                        System.StringComparison.OrdinalIgnoreCase));

            if (!esEstudiante)
            {
                return BadRequest(new
                {
                    message =
                        "El usuario seleccionado no tiene rol de Estudiante."
                });
            }

            var tieneAcceso = esAdminOCoordinador || await (
                from inscripcion in _context.Inscripciones
                join catedra in _context.Catedras
                    on inscripcion.CatedraId equals catedra.Id
                where inscripcion.EstudianteId == estudianteId &&
                      catedra.DocenteId == docenteId
                select inscripcion.Id
            ).AnyAsync();

            if (!tieneAcceso)
            {
                return StatusCode(
                    403,
                    new
                    {
                        message =
                            "El docente no tiene acceso al expediente de este estudiante."
                    });
            }

            var historial = await (
                from inscripcion in _context.Inscripciones
                join catedra in _context.Catedras
                    on inscripcion.CatedraId equals catedra.Id
                where inscripcion.EstudianteId == estudianteId
                orderby catedra.Semestre descending,
                        catedra.Nombre
                select new HistorialAcademicoDto
                {
                    NombreCatedra =
                        catedra.Nombre,

                    CalificacionFinal =
                        inscripcion.PromedioActual,

                    Periodo =
                        catedra.Semestre
                }
            ).ToListAsync();

            var indicadores =
                await _context
                    .IndicadoresCualitativos
                    .Where(i =>
                        i.EstudianteId ==
                        estudianteId)
                    .OrderByDescending(i =>
                        i.Fecha)
                    .Select(i =>
                        new IndicadorCualitativoDto
                        {
                            Id =
                                i.Id,

                            EstudianteId =
                                i.EstudianteId,

                            CatedraId =
                                i.CatedraId,

                            Indicador =
                                i.Indicador,

                            Observacion =
                                i.Observacion,

                            Fecha =
                                i.Fecha
                        })
                    .ToListAsync();

            var nombreEstudiante =
                estudiante.Persona != null
                    ? $"{estudiante.Persona.Nombre} {estudiante.Persona.Apellido}".Trim()
                    : estudiante.Username;

            var expediente =
                new ExpedienteDto
                {
                    EstudianteId =
                        estudiante.Id,

                    NombreEstudiante =
                        nombreEstudiante,

                    Historial =
                        historial,

                    Indicadores =
                        indicadores
                };

            return Ok(expediente);
        }

        // =========================================================
        // CÃTEDRAS DEL ESTUDIANTE
        // =========================================================

        // GET /api/Docente/estudiantes/{estudianteId}/catedras
        [Authorize(Roles = "Docente,Administrador,Coordinador")]
        [HttpGet("estudiantes/{estudianteId}/catedras")]
        public async Task<IActionResult> ObtenerCatedrasEstudiante(
            int estudianteId, [FromRoute] int? id = null)
        {
            var targetEstudianteId = id.HasValue && id.Value > 0 ? id.Value : estudianteId;

            var userIdClaim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(
                    userIdClaim,
                    out var docenteId))
            {
                return Unauthorized(new
                {
                    message =
                        "Usuario no autenticado."
                });
            }

            var docente =
                await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u =>
                        u.Id == docenteId);

            if (docente == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Usuario no encontrado."
                });
            }

            var roles =
                docente.Persona?.GetRoles()
                ?? new List<string>();

            var esDocente =
                roles.Any(r =>
                    r.Equals(
                        "Docente",
                        System.StringComparison.OrdinalIgnoreCase) ||
                    r.Equals(
                        "Profesor",
                        System.StringComparison.OrdinalIgnoreCase));

            var esAdminOCoordinador =
                roles.Any(r =>
                    r.Equals("Administrador", System.StringComparison.OrdinalIgnoreCase) ||
                    r.Equals("Coordinador", System.StringComparison.OrdinalIgnoreCase)) ||
                User.IsInRole("Administrador") ||
                User.IsInRole("Coordinador");

            if (!esDocente && !esAdminOCoordinador)
                return Forbid();

            var estudiante =
                await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u =>
                        u.Id == targetEstudianteId);

            if (estudiante == null)
            {
                return NotFound(new
                {
                    message =
                        "Estudiante no encontrado."
                });
            }

            var rolesEstudiante =
                estudiante.Persona?.GetRoles()
                ?? new List<string>();

            var esEstudiante =
                rolesEstudiante.Any(r =>
                    r.Equals(
                        "Estudiante",
                        System.StringComparison.OrdinalIgnoreCase));

            if (!esEstudiante)
            {
                return BadRequest(new
                {
                    message =
                        "El usuario no tiene rol de Estudiante."
                });
            }

            var query = _context.Catedras.AsQueryable();
            if (!esAdminOCoordinador)
            {
                query = query.Where(c => c.DocenteId == docenteId);
            }

            var catedras =
                await query
                    .Where(c =>
                        c.Inscripciones.Any(i =>
                            i.EstudianteId ==
                            targetEstudianteId))
                    .Select(c => new
                    {
                        id = c.Id,
                        nombre = c.Nombre,
                        semestre = c.Semestre
                    })
                    .OrderBy(c =>
                        c.nombre)
                    .ToListAsync();

            return Ok(catedras);
        }

        // =========================================================
        // INDICADORES CUALITATIVOS - RF-003
        // =========================================================

        // POST /api/Docente/catedras/{catedraId}/estudiantes/{estudianteId}/indicadores
        [HttpPost(
            "catedras/{catedraId}/estudiantes/{estudianteId}/indicadores")]
        public async Task<IActionResult> RegistrarIndicador(
            int catedraId,
            int estudianteId,
            [FromBody] CreateIndicadorCualitativoDto dto)
        {
            var userIdClaim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(
                    userIdClaim,
                    out var userId))
            {
                return Unauthorized(new
                {
                    message =
                        "Usuario no autenticado."
                });
            }

            var docente =
                await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u =>
                        u.Id == userId);

            if (docente == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Usuario no encontrado."
                });
            }

            var roles =
                docente.Persona?.GetRoles()
                ?? new List<string>();

            if (!roles.Any(r =>
                    r.Equals(
                        "Docente",
                        System.StringComparison.OrdinalIgnoreCase) ||
                    r.Equals(
                        "Profesor",
                        System.StringComparison.OrdinalIgnoreCase)))
            {
                return Forbid();
            }

            var catedra =
                await _context.Catedras
                    .FindAsync(catedraId);

            if (catedra == null)
            {
                return NotFound(new
                {
                    message =
                        "CÃ¡tedra no encontrada."
                });
            }

            if (catedra.DocenteId != userId)
                return Forbid();

            if (dto == null ||
                string.IsNullOrWhiteSpace(
                    dto.Indicador) ||
                string.IsNullOrWhiteSpace(
                    dto.Observacion))
            {
                return BadRequest(new
                {
                    message =
                        "Indicador y ObservaciÃ³n son requeridos."
                });
            }

            var indicadorTrimmed =
                dto.Indicador.Trim();

            var indicadoresValidos =
                new[]
                {
                    "InterÃ©s",
                    "ParticipaciÃ³n",
                    "DesempeÃ±o"
                };

            var indicadorNormalizado =
                indicadoresValidos
                    .FirstOrDefault(i =>
                        i.Equals(
                            indicadorTrimmed,
                            System.StringComparison.OrdinalIgnoreCase));

            if (indicadorNormalizado == null)
            {
                return BadRequest(new
                {
                    message =
                        "Indicador no vÃ¡lido. Debe ser: InterÃ©s, ParticipaciÃ³n o DesempeÃ±o."
                });
            }

            var estudiante =
                await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u =>
                        u.Id == estudianteId);

            if (estudiante == null)
            {
                return NotFound(new
                {
                    message =
                        "Estudiante no encontrado."
                });
            }

            var rolesEstudiante =
                estudiante.Persona?.GetRoles()
                ?? new List<string>();

            if (!rolesEstudiante.Any(r =>
                    r.Equals(
                        "Estudiante",
                        System.StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new
                {
                    message =
                        "El usuario no tiene rol de Estudiante."
                });
            }

            var inscripcion =
                await _context.Inscripciones
                    .AnyAsync(i =>
                        i.EstudianteId ==
                        estudianteId &&
                        i.CatedraId ==
                        catedraId);

            if (!inscripcion)
            {
                return BadRequest(new
                {
                    message =
                        "El estudiante no estÃ¡ inscrito en esta cÃ¡tedra."
                });
            }

            var nuevoIndicador =
                new IndicadorCualitativo
                {
                    EstudianteId =
                        estudianteId,

                    CatedraId =
                        catedraId,

                    Indicador =
                        indicadorNormalizado,

                    Observacion =
                        dto.Observacion.Trim(),

                    Fecha =
                        System.DateTime.UtcNow
                };

            _context
                .IndicadoresCualitativos
                .Add(nuevoIndicador);

            await _context.SaveChangesAsync();

            var resultDto =
                new IndicadorCualitativoDto
                {
                    Id =
                        nuevoIndicador.Id,

                    EstudianteId =
                        nuevoIndicador.EstudianteId,

                    CatedraId =
                        nuevoIndicador.CatedraId,

                    Indicador =
                        nuevoIndicador.Indicador,

                    Observacion =
                        nuevoIndicador.Observacion,

                    Fecha =
                        nuevoIndicador.Fecha
                };

            return CreatedAtAction(
                nameof(ObtenerIndicadoresHistorial),
                new
                {
                    catedraId,
                    estudianteId
                },
                resultDto);
        }

        // GET /api/Docente/catedras/{catedraId}/estudiantes/{estudianteId}/indicadores
        [HttpGet(
            "catedras/{catedraId}/estudiantes/{estudianteId}/indicadores")]
        public async Task<IActionResult> ObtenerIndicadoresHistorial(
            int catedraId,
            int estudianteId)
        {
            var userIdClaim =
                User.FindFirst(
                    ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(
                    userIdClaim,
                    out var userId))
            {
                return Unauthorized(new
                {
                    message =
                        "Usuario no autenticado."
                });
            }

            var docente =
                await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u =>
                        u.Id == userId);

            if (docente == null)
            {
                return Unauthorized(new
                {
                    message =
                        "Usuario no encontrado."
                });
            }

            var roles =
                docente.Persona?.GetRoles()
                ?? new List<string>();

            if (!roles.Any(r =>
                    r.Equals(
                        "Docente",
                        System.StringComparison.OrdinalIgnoreCase) ||
                    r.Equals(
                        "Profesor",
                        System.StringComparison.OrdinalIgnoreCase)))
            {
                return Forbid();
            }

            var catedra =
                await _context.Catedras
                    .FindAsync(catedraId);

            if (catedra == null)
            {
                return NotFound(new
                {
                    message =
                        "CÃ¡tedra no encontrada."
                });
            }

            if (catedra.DocenteId != userId)
                return Forbid();

            var estudiante =
                await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u =>
                        u.Id == estudianteId);

            if (estudiante == null)
            {
                return NotFound(new
                {
                    message =
                        "Estudiante no encontrado."
                });
            }

            var rolesEstudiante =
                estudiante.Persona?.GetRoles()
                ?? new List<string>();

            if (!rolesEstudiante.Any(r =>
                    r.Equals(
                        "Estudiante",
                        System.StringComparison.OrdinalIgnoreCase)))
            {
                return BadRequest(new
                {
                    message =
                        "El usuario no tiene rol de Estudiante."
                });
            }

            var inscripcion =
                await _context.Inscripciones
                    .AnyAsync(i =>
                        i.EstudianteId ==
                        estudianteId &&
                        i.CatedraId ==
                        catedraId);

            if (!inscripcion)
            {
                return BadRequest(new
                {
                    message =
                        "El estudiante no estÃ¡ inscrito en esta cÃ¡tedra."
                });
            }

            var indicadores =
                await _context
                    .IndicadoresCualitativos
                    .Where(ind =>
                        ind.EstudianteId ==
                        estudianteId &&
                        ind.CatedraId ==
                        catedraId)
                    .OrderByDescending(ind =>
                        ind.Fecha)
                    .Select(ind =>
                        new IndicadorCualitativoDto
                        {
                            Id =
                                ind.Id,

                            EstudianteId =
                                ind.EstudianteId,

                            CatedraId =
                                ind.CatedraId,

                            Indicador =
                                ind.Indicador,

                            Observacion =
                                ind.Observacion,

                            Fecha =
                                ind.Fecha
                        })
                    .ToListAsync();

            return Ok(indicadores);
        }
    }
}
