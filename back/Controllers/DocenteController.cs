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
                return NotFound("Cátedra no encontrada.");

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

            var docente = await _context.Users
                .Include(u => u.Persona)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (docente == null)
            {
                return Unauthorized(new
                {
                    message = "Usuario no encontrado."
                });
            }

            var roles = docente.Persona?.GetRoles()
                        ?? new List<string>();

            var esDocente = roles.Any(r =>
                r.Equals(
                    "Docente",
                    System.StringComparison.OrdinalIgnoreCase) ||
                r.Equals(
                    "Profesor",
                    System.StringComparison.OrdinalIgnoreCase));

            if (!esDocente)
                return Forbid();

            return await GetClasesDocenteInternal(docente.Id);
        }

        // GET /api/Docente/{id}/clases
        [HttpGet("{id}/clases")]
        public async Task<IActionResult> GetClasesDocentePorId(long id)
        {
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
                    descripcion = "Cátedra",
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
        // OBTENER CLASES INTERNAMENTE
        // =========================================================

        private async Task<IActionResult> GetClasesDocenteInternal(
            long docenteId)
        {
            var doc = await GetDefaultDocenteAsync(docenteId);

            int targetDocId = doc != null
                ? doc.Id
                : (int)docenteId;

            if (targetDocId > 0)
            {
                var materiasDoc = await _context.Materias
                    .Where(m =>
                        m.DocenteResponsableId == targetDocId)
                    .ToListAsync();

                foreach (var mat in materiasDoc)
                {
                    var hasClase = await _context.Clases
                        .AnyAsync(c =>
                            c.MateriaId == mat.Id &&
                            c.DocenteId == targetDocId);

                    if (!hasClase)
                    {
                        var autoClase = new Clase
                        {
                            Nombre = $"{mat.Nombre} - Paralelo A",
                            MateriaId = mat.Id,
                            DocenteId = targetDocId
                        };

                        _context.Clases.Add(autoClase);
                        await _context.SaveChangesAsync();
                    }
                }
            }

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
        // EVALUACIÓN DIAGNÓSTICA
        // =========================================================

        [HttpPost("catedras/{catedraId}/evaluacion-diagnostica")]
        public async Task<IActionResult> RegistrarEvaluacionDiagnostica(
            int catedraId,
            [FromBody] EvaluacionDto evaluacionDto)
        {
            var evaluacion = new Evaluacion
            {
                Nombre = evaluacionDto.Nombre,
                CatedraId = catedraId,
                EsDiagnostica = true,
                AdaptadaConIA = false
            };

            _context.Evaluaciones.Add(evaluacion);
            await _context.SaveChangesAsync();

            evaluacionDto.Id = evaluacion.Id;
            evaluacionDto.CatedraId = catedraId;
            evaluacionDto.EsDiagnostica = true;

            return CreatedAtAction(
                nameof(RegistrarEvaluacionDiagnostica),
                new { id = evaluacion.Id },
                evaluacionDto);
        }

        // =========================================================
        // CRONOGRAMA
        // =========================================================

        [HttpPut("catedras/{catedraId}/cronograma")]
        public async Task<IActionResult> ReprogramarCronograma(
            int catedraId,
            [FromBody] CronogramaActividadDto cronogramaDto)
        {
            var actividad = await _context.Cronogramas
                .FindAsync(cronogramaDto.Id);

            if (actividad == null ||
                actividad.CatedraId != catedraId)
            {
                return NotFound(
                    "Actividad del cronograma no encontrada.");
            }

            actividad.Descripcion = cronogramaDto.Descripcion;
            actividad.FechaPrevista = cronogramaDto.FechaPrevista;
            actividad.FechaReal = cronogramaDto.FechaReal;

            await _context.SaveChangesAsync();

            return Ok(cronogramaDto);
        }

        // =========================================================
        // PLANIFICACIÓN DE AYUDANTÍAS
        // =========================================================

        [HttpPost("ayudantias/{ayudantiaId}/planificacion")]
        public async Task<IActionResult> PlanificarActividadesAyudantia(
            int ayudantiaId,
            [FromBody] ActividadAyudantiaDto actividadDto)
        {
            var actividad = new ActividadAyudantia
            {
                AyudantiaId = ayudantiaId,
                Descripcion = actividadDto.Descripcion,
                FechaPlanificada = actividadDto.FechaPlanificada,
                Completada = false
            };

            _context.ActividadesAyudantia.Add(actividad);
            await _context.SaveChangesAsync();

            actividadDto.Id = actividad.Id;

            return CreatedAtAction(
                nameof(PlanificarActividadesAyudantia),
                new { id = actividad.Id },
                actividadDto);
        }

        // =========================================================
        // MONITOREO DE AYUDANTÍA
        // =========================================================

        [HttpGet("ayudantias/{ayudantiaId}/monitoreo")]
        public async Task<IActionResult> MonitorearCumplimientoAyudante(
            int ayudantiaId)
        {
            var ayudantia = await _context.Ayudantias
                .Include(a => a.Estudiante)
                .Include(a => a.Planificacion)
                .Include(a => a.Bitacoras)
                .FirstOrDefaultAsync(a =>
                    a.Id == ayudantiaId);

            if (ayudantia == null)
                return NotFound(
                    "Ayudantía no encontrada.");

            var monitoreoDto = new MonitoreoAyudantiaDto
            {
                AyudantiaId = ayudantia.Id,

                NombreAyudante =
                    ayudantia.Estudiante != null &&
                    ayudantia.Estudiante.Persona != null
                        ? $"{ayudantia.Estudiante.Persona.Nombre} {ayudantia.Estudiante.Persona.Apellido}"
                        : ayudantia.Estudiante.Username,

                Planificacion = ayudantia.Planificacion
                    .Select(p => new ActividadAyudantiaDto
                    {
                        Id = p.Id,
                        AyudantiaId = p.AyudantiaId,
                        Descripcion = p.Descripcion,
                        FechaPlanificada = p.FechaPlanificada,
                        Completada = p.Completada
                    })
                    .ToList(),

                Bitacoras = ayudantia.Bitacoras
                    .Select(b => new BitacoraDto
                    {
                        Id = b.Id,
                        Fecha = b.Fecha,
                        ActividadesRealizadas =
                            b.ActividadesRealizadas,
                        EvidenciaUrl =
                            b.EvidenciaUrl
                    })
                    .ToList()
            };

            return Ok(monitoreoDto);
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
                   message = "La calificación debe estar entre 0 y 100."
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

           // Buscar la inscripción correspondiente al estudiante.
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
                   message = "No se encontró la inscripción del estudiante para esta materia."
               });
           }

           // Registrar la nueva calificación.
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
               // activar alerta únicamente cuando ya existen calificaciones
               // y el promedio está debajo del umbral configurado.
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
               message = "Calificación registrada correctamente.",
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

            if (!esDocente)
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

            var tieneAcceso = await (
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
        // CÁTEDRAS DEL ESTUDIANTE
        // =========================================================

        // GET /api/Docente/estudiantes/{estudianteId}/catedras
        [HttpGet("estudiantes/{estudianteId}/catedras")]
        public async Task<IActionResult> ObtenerCatedrasEstudiante(
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

            if (!esDocente)
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
                        "El usuario no tiene rol de Estudiante."
                });
            }

            var catedras =
                await _context.Catedras
                    .Where(c =>
                        c.DocenteId == docenteId &&
                        c.Inscripciones.Any(i =>
                            i.EstudianteId ==
                            estudianteId))
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
                        "Cátedra no encontrada."
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
                        "Indicador y Observación son requeridos."
                });
            }

            var indicadorTrimmed =
                dto.Indicador.Trim();

            var indicadoresValidos =
                new[]
                {
                    "Interés",
                    "Participación",
                    "Desempeño"
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
                        "Indicador no válido. Debe ser: Interés, Participación o Desempeño."
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
                        "El estudiante no está inscrito en esta cátedra."
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
                        "Cátedra no encontrada."
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
                        "El estudiante no está inscrito en esta cátedra."
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