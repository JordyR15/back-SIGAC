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

        public DocenteController(AppDbContext context, IEmailService emailService, ILogger<DocenteController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        // POST /api/Docente/convocatorias : crear una convocatoria en estado PendienteAprobacion
        [HttpPost("convocatorias")]
        public async Task<IActionResult> CrearConvocatoria([FromBody] DTOs.CreateConvocatoriaDto dto)
        {
            var value = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(value, out var userId)) return Unauthorized();

            var catedra = await _context.Catedras.FindAsync(dto.CatedraId);
            if (catedra == null) return NotFound("Cátedra no encontrada.");

            var convocatoria = new Entities.Convocatoria
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

            return CreatedAtAction(nameof(CrearConvocatoria), new { id = convocatoria.Id }, new { convocatoria.Id, convocatoria.CatedraId, convocatoria.Estado });
        }

        private async Task<User> GetDefaultDocenteAsync(long? requestedDocenteId = null)
        {
            if (requestedDocenteId.HasValue && requestedDocenteId.Value > 0 && requestedDocenteId.Value <= int.MaxValue)
            {
                var doc = await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u => u.Id == (int)requestedDocenteId.Value || (u.Persona != null && (u.Persona.Id == (int)requestedDocenteId.Value || u.Persona.UserId == (int)requestedDocenteId.Value)));
                if (doc != null) return doc;
            }

            // Buscar por correo docente@uteq.edu.ec
            var defaultDoc = await _context.Users
                .Include(u => u.Persona)
                .FirstOrDefaultAsync(u => (u.Persona != null && u.Persona.Correo.ToLower() == "docente@uteq.edu.ec") 
                                       || u.Username.ToLower() == "docente@uteq.edu.ec"
                                       || u.Username.ToLower() == "docente");
            if (defaultDoc != null) return defaultDoc;

            // Buscar cualquier usuario con rol Docente
            var anyDoc = await _context.Users
                .Include(u => u.Persona)
                .FirstOrDefaultAsync(u => u.Persona != null && (u.Persona.Rol == "Docente" || u.Persona.Rol.Contains("Docente")));
            if (anyDoc != null) return anyDoc;

            return await _context.Users.Include(u => u.Persona).FirstOrDefaultAsync();
        }

        // GET /api/Docente/clases: Clases asignadas al docente logueado
        [HttpGet("clases")]
        public async Task<IActionResult> GetClasesDocenteLogueado()
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            int? userId = int.TryParse(value, out var id) ? id : (int?)null;

            User doc = null;
            if (userId.HasValue)
            {
                doc = await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u => u.Id == userId.Value);
            }

            if (doc == null || doc.Persona == null || (!doc.Persona.Rol.Contains("Docente") && !doc.Persona.Rol.Contains("Profesor")))
            {
                doc = await GetDefaultDocenteAsync();
            }

            int targetId = doc != null ? doc.Id : (userId ?? 1);
            return await GetClasesDocenteInternal(targetId);
        }

        // GET /api/Docente/{id}/clases: Clases asignadas al docente por ID
        [HttpGet("{id}/clases")]
        public async Task<IActionResult> GetClasesDocentePorId(long id)
        {
            return await GetClasesDocenteInternal(id);
        }

        // GET /api/Docente/{id}/materias : listar materias/cátedras donde es docente
        [HttpGet("{id}/materias")]
        public async Task<IActionResult> GetMateriasPorDocente(long id)
        {
            if (id > int.MaxValue) return Ok(new List<object>());
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
                    nombreDocente = m.DocenteResponsable != null && m.DocenteResponsable.Persona != null ? $"{m.DocenteResponsable.Persona.Nombre} {m.DocenteResponsable.Persona.Apellido}" : ""
                })
                .ToListAsync();

            // incluir cátedras que no tienen Materia vinculada pero sí Docente
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
                    nombreDocente = c.Docente != null && c.Docente.Persona != null ? $"{c.Docente.Persona.Nombre} {c.Docente.Persona.Apellido}" : ""
                })
                .ToListAsync();

            var combined = materias.Concat(catedras).ToList();
            return Ok(combined);
        }

        private async Task<IActionResult> GetClasesDocenteInternal(long docenteId)
        {
            var doc = await GetDefaultDocenteAsync(docenteId);
            int targetDocId = doc != null ? doc.Id : (int)docenteId;

            if (targetDocId > 0)
            {
                // Sincronizar materias asignadas al docente que aún no tengan una Clase
                var materiasDoc = await _context.Materias
                    .Where(m => m.DocenteResponsableId == targetDocId)
                    .ToListAsync();

                foreach (var mat in materiasDoc)
                {
                    var hasClase = await _context.Clases.AnyAsync(c => c.MateriaId == mat.Id && c.DocenteId == targetDocId);
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
                // Filtrado estricto: solo cátedras donde el docente es titular directo
                .Where(c => c.DocenteId == targetDocId)
                .Select(c => new
                {
                    id = c.Id,
                    claseId = c.Id,
                    nombre = c.Nombre,
                    materiaId = c.MateriaId,
                    materia = c.Materia != null ? c.Materia.Nombre : "",
                    nombreMateria = c.Materia != null ? c.Materia.Nombre : "",
                    codigoMateria = c.Materia != null ? c.Materia.Codigo : "",
                    docenteId = c.DocenteId,
                    docente = c.Docente != null && c.Docente.Persona != null
                        ? $"{c.Docente.Persona.Nombre} {c.Docente.Persona.Apellido}".Trim()
                        : (c.Docente != null ? c.Docente.Username : "Docente"),
                    docenteNombre = c.Docente != null && c.Docente.Persona != null
                        ? $"{c.Docente.Persona.Nombre} {c.Docente.Persona.Apellido}".Trim()
                        : (c.Docente != null ? c.Docente.Username : "Docente"),
                    docenteEmail = c.Docente != null && c.Docente.Persona != null ? c.Docente.Persona.Correo : (c.Docente != null ? c.Docente.Username : ""),
                    aula = "Aula Principal",
                    horario = "Horario Regular",
                    paralelo = "A",
                    estudiantesCount = c.Estudiantes.Count,
                    estudianteIds = c.Estudiantes.Select(e => e.Id).ToList(),
                    estudiantes = c.Estudiantes.Select(e => new
                    {
                        id = e.Id,
                        username = e.Username,
                        nombreCompleto = e.Persona != null ? $"{e.Persona.Nombre} {e.Persona.Apellido}".Trim() : string.Empty,
                        nombre = e.Persona != null ? $"{e.Persona.Nombre} {e.Persona.Apellido}".Trim() : e.Username,
                        correo = e.Persona != null ? e.Persona.Correo : string.Empty,
                        cedula = e.Persona != null ? (e.Persona.Cedula ?? string.Empty) : string.Empty
                    }).ToList()
                })
                .ToListAsync();

            return Ok(clases.DistinctBy(c => c.id).ToList());
        }

        // ... (métodos ya implementados) ...

        [HttpPost("catedras/{catedraId}/evaluacion-diagnostica")]
        public async Task<IActionResult> RegistrarEvaluacionDiagnostica(int catedraId, [FromBody] EvaluacionDto evaluacionDto)
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

            return CreatedAtAction(nameof(RegistrarEvaluacionDiagnostica), new { id = evaluacion.Id }, evaluacionDto);
        }

        [HttpPut("catedras/{catedraId}/cronograma")]
        public async Task<IActionResult> ReprogramarCronograma(int catedraId, [FromBody] CronogramaActividadDto cronogramaDto)
        {
            var actividad = await _context.Cronogramas.FindAsync(cronogramaDto.Id);
            if (actividad == null || actividad.CatedraId != catedraId)
            {
                return NotFound("Actividad del cronograma no encontrada.");
            }

            actividad.Descripcion = cronogramaDto.Descripcion;
            actividad.FechaPrevista = cronogramaDto.FechaPrevista;
            actividad.FechaReal = cronogramaDto.FechaReal;

            await _context.SaveChangesAsync();
            return Ok(cronogramaDto);
        }

        [HttpPost("ayudantias/{ayudantiaId}/planificacion")]
        public async Task<IActionResult> PlanificarActividadesAyudantia(int ayudantiaId, [FromBody] ActividadAyudantiaDto actividadDto)
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
            return CreatedAtAction(nameof(PlanificarActividadesAyudantia), new { id = actividad.Id }, actividadDto);
        }



        [HttpGet("ayudantias/{ayudantiaId}/monitoreo")]
        public async Task<IActionResult> MonitorearCumplimientoAyudante(int ayudantiaId)
        {
            var ayudantia = await _context.Ayudantias
                .Include(a => a.Estudiante)
                .Include(a => a.Planificacion)
                .Include(a => a.Bitacoras)
                .FirstOrDefaultAsync(a => a.Id == ayudantiaId);

            if (ayudantia == null) return NotFound("Ayudantía no encontrada.");

            var monitoreoDto = new MonitoreoAyudantiaDto
            {
                AyudantiaId = ayudantia.Id,
                NombreAyudante = ayudantia.Estudiante.Username,
                Planificacion = ayudantia.Planificacion.Select(p => new ActividadAyudantiaDto
                {
                    Id = p.Id,
                    AyudantiaId = p.AyudantiaId,
                    Descripcion = p.Descripcion,
                    FechaPlanificada = p.FechaPlanificada,
                    Completada = p.Completada
                }).ToList(),
                Bitacoras = ayudantia.Bitacoras.Select(b => new BitacoraDto
                {
                    Id = b.Id,
                    Fecha = b.Fecha,
                    ActividadesRealizadas = b.ActividadesRealizadas,
                    EvidenciaUrl = b.EvidenciaUrl
                }).ToList()
            };

            return Ok(monitoreoDto);
        }

        [HttpGet("actividades/{actividadId}/entregas")]
        public async Task<IActionResult> ObtenerEntregasPorActividad(int actividadId)
        {
            var actividad = await _context.Actividades.FindAsync(actividadId);
            if (actividad == null) return NotFound("Actividad no encontrada.");

            var entregas = await _context.EstudianteActividadesRealizadas
                .Where(e => e.ActividadId == actividadId)
                .Include(e => e.Estudiante)
                    .ThenInclude(u => u.Persona)
                .Select(e => new
                {
                    e.Id,
                    e.EstudianteId,
                    NombreEstudiante = e.Estudiante.Persona.Nombre + " " + e.Estudiante.Persona.Apellido,
                    e.ArchivoUrl,
                    e.FechaRealizada,
                    e.Completada,
                    e.Calificacion,
                    e.Retroalimentacion
                })
                .ToListAsync();

            return Ok(entregas);
        }

        [HttpPost("actividades/calificar")]
        public async Task<IActionResult> CalificarEntrega([FromBody] CalificarEntregaDto dto)
        {
            if (dto == null) return BadRequest("Datos necesarios.");

            var entrega = await _context.EstudianteActividadesRealizadas
                .FirstOrDefaultAsync(e => e.Id == dto.EntregaId);

            if (entrega == null) return NotFound("Entrega no encontrada.");

            entrega.Calificacion = dto.Calificacion;
            entrega.Retroalimentacion = dto.Retroalimentacion ?? string.Empty;
            entrega.Completada = true;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Calificación registrada correctamente.", entregaId = entrega.Id, calificacion = entrega.Calificacion });
        }
    }
}
