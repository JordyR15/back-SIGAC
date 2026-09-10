using back.Data;
using back.DTOs;
using back.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace back.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class EstudianteController : ControllerBase
    {
        private readonly AppDbContext _context;
        
        public EstudianteController(AppDbContext context)
        {
            _context = context;
        }

        // Propiedad para obtener de forma segura el ID del estudiante autenticado
        private int? EstudianteId
        {
            get
            {
                var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return int.TryParse(value, out var id) ? id : (int?)null;
            }
        }

        [HttpPost("ayudantias/postulaciones")]
        public async Task<IActionResult> PostularAyudantia([FromBody] PostulacionAyudantiaDto postulacionDto)
        {
            if (EstudianteId == null) return Unauthorized();

            var existePostulacion = await _context.Ayudantias
                .AnyAsync(a => a.EstudianteId == EstudianteId.Value && a.CatedraId == postulacionDto.CatedraId);

            if (existePostulacion)
            {
                return Conflict(new { message = "Ya te has postulado a esta ayudantía." });
            }

            // Verificar inscripción y promedio actual
            var inscripcion = await _context.Inscripciones
                .FirstOrDefaultAsync(i => i.EstudianteId == EstudianteId.Value && i.CatedraId == postulacionDto.CatedraId);

            if (inscripcion == null)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Debes estar inscrito en la cátedra para postular a ayudantía." });
            }

            var catedra = await _context.Catedras.FindAsync(postulacionDto.CatedraId);
            if (catedra != null && catedra.MinimoNota.HasValue && inscripcion.PromedioActual < catedra.MinimoNota.Value)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Tu promedio actual es menor que la nota mínima para postular a esta ayudantía." });
            }

            var ayudantia = new Ayudantia
            {
                CatedraId = postulacionDto.CatedraId,
                EstudianteId = EstudianteId.Value,
                Estado = "Pendiente"
            };

            _context.Ayudantias.Add(ayudantia);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Postulación enviada exitosamente." });
        }

        [HttpPost("ayudantias/bitacora")]
        public async Task<IActionResult> RegistrarEnBitacora([FromBody] RegistroBitacoraDto registroDto)
        {
            var authResult = await CheckAyudantiaOwnershipAsync(registroDto.AyudantiaId);
            if (authResult != null)
            {
                return authResult;
            }

            var bitacora = new Bitacora
            {
                AyudantiaId = registroDto.AyudantiaId,
                Fecha = DateTime.UtcNow,
                ActividadesRealizadas = registroDto.ActividadesRealizadas,
                EvidenciaUrl = registroDto.EvidenciaUrl
            };

            _context.Bitacoras.Add(bitacora);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Bitácora registrada exitosamente." });
        }

        [HttpPost("ayudantias/informe-mensual")]
        public async Task<IActionResult> GenerarInformeMensual([FromBody] InformeMensualRequestDto request)
        {
            var authResult = await CheckAyudantiaOwnershipAsync(request.AyudantiaId);
            if (authResult != null)
            {
                return authResult;
            }

            var bitacoras = await _context.Bitacoras
                .Where(b => b.AyudantiaId == request.AyudantiaId && b.Fecha.Month == request.Mes && b.Fecha.Year == request.Anio)
                .OrderBy(b => b.Fecha)
                .Select(b => new BitacoraDto
                {
                    Id = b.Id,
                    Fecha = b.Fecha,
                    ActividadesRealizadas = b.ActividadesRealizadas,
                    EvidenciaUrl = b.EvidenciaUrl
                })
                .ToListAsync();

            // Aquí se podría integrar una librería para generar un PDF, por ahora devuelvo los datos.
            return Ok(bitacoras);
        }

        // Nuevo endpoint para obtener el historial de ayudantías del estudiante
        [HttpGet("ayudantias/historial")]
        public async Task<ActionResult<IEnumerable<HistorialAyudantiaDto>>> GetHistorialAyudantias()
        {
            if (EstudianteId == null) return Unauthorized();

            var historial = await _context.Ayudantias
                .Where(a => a.EstudianteId == EstudianteId.Value)
                .Include(a => a.Catedra)
                    .ThenInclude(c => c.Docente)
                .Select(a => new HistorialAyudantiaDto
                {
                    AyudantiaId = a.Id,
                    EstadoAyudantia = a.Estado,
                    CatedraId = a.CatedraId,
                    NombreCatedra = a.Catedra.Nombre,
                    SemestreCatedra = a.Catedra.Semestre,
                    DocenteCatedra = a.Catedra.Docente.Persona.Nombre + " " + a.Catedra.Docente.Persona.Apellido
                })
                .ToListAsync();

            if (!historial.Any())
            {
                return NotFound(new { message = "No se encontró historial de ayudantías para este estudiante." });
            }

            return Ok(historial);
        }

        [HttpGet("{id}/validacion-malla")]
        public async Task<IActionResult> ValidacionMalla(int id, [FromQuery] int? catedraId = null)
        {
            var inscripciones = await _context.Inscripciones
                .Where(i => i.EstudianteId == id)
                .ToListAsync();

            if (!inscripciones.Any())
            {
                return NotFound(new { message = "Estudiante sin inscripciones registradas." });
            }

            var totalCursos = await _context.Catedras.CountAsync();
            var cursosAprobados = inscripciones.Count(i => i.PromedioActual >= 60.0);
            var porcentajeAvance = totalCursos > 0 ? (double)cursosAprobados / totalCursos * 100d : 0d;
            var promedioGeneral = inscripciones.Average(i => i.PromedioActual);
            var promedioCurso = catedraId.HasValue
                ? (double?)inscripciones
                    .Where(i => i.CatedraId == catedraId.Value)
                    .Select(i => i.PromedioActual)
                    .DefaultIfEmpty(0.0)
                    .Average()
                : (double?)null;

            return Ok(new
            {
                EstudianteId = id,
                PorcentajeAvanceMalla = Math.Round(porcentajeAvance, 2),
                CursosAprobados = cursosAprobados,
                TotalCursos = totalCursos,
                PromedioGeneral = Math.Round(promedioGeneral, 2),
                PromedioCurso = promedioCurso.HasValue ? (double?)Math.Round(promedioCurso.Value, 2) : (double?)null,
                CursoId = catedraId,
                CumpleMalla = porcentajeAvance >= 50,
                CumplePromedioGeneral = promedioGeneral >= 60.0,
                CumplePromedioCurso = !catedraId.HasValue || (promedioCurso.HasValue && promedioCurso.Value >= 60.0)
            });
        }

        [HttpPost("actividades/{actividadId}/entregar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> EntregarActividad(int actividadId, IFormFile archivo)
        {
            if (EstudianteId == null) return Unauthorized();
            if (archivo == null || archivo.Length == 0) return BadRequest("Debe adjuntar un archivo.");

            var actividad = await _context.Actividades.FindAsync(actividadId);
            if (actividad == null) return NotFound("Actividad no encontrada.");

            var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "entregas", $"actividad-{actividadId}");
            Directory.CreateDirectory(uploadsFolder);

            var safeName = Path.GetFileName(archivo.FileName);
            var fileName = $"{Guid.NewGuid():N}_{safeName}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await archivo.CopyToAsync(stream);
            }

            var entrega = await _context.EstudianteActividadesRealizadas
                .FirstOrDefaultAsync(e => e.EstudianteId == EstudianteId.Value && e.ActividadId == actividadId);

            if (entrega == null)
            {
                entrega = new EstudianteActividadRealizada
                {
                    EstudianteId = EstudianteId.Value,
                    ActividadId = actividadId,
                    FechaRealizada = DateTime.UtcNow,
                    Completada = true
                };
                _context.EstudianteActividadesRealizadas.Add(entrega);
            }
            else
            {
                entrega.FechaRealizada = DateTime.UtcNow;
                entrega.Completada = true;
            }

            entrega.ArchivoUrl = $"/uploads/entregas/actividad-{actividadId}/{fileName}";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Entrega registrada correctamente.", archivoUrl = entrega.ArchivoUrl });
        }

        [HttpGet("mis-materias")]
        public async Task<IActionResult> GetMisMaterias()
        {
            if (EstudianteId == null) return Unauthorized();

            var materias = await _context.Inscripciones
                .Where(i => i.EstudianteId == EstudianteId.Value)
                .Include(i => i.Catedra)
                    .ThenInclude(c => c.Docente)
                        .ThenInclude(d => d.Persona)
                .Select(i => new
                {
                    id = i.Catedra.Id,
                    codigo = $"CAT-{i.Catedra.Id:D3}",
                    nombre = i.Catedra.Nombre,
                    descripcion = $"Cátedra correspondiente al semestre {i.Catedra.Semestre}",
                    docente = i.Catedra.Docente != null && i.Catedra.Docente.Persona != null
                        ? $"{i.Catedra.Docente.Persona.Nombre} {i.Catedra.Docente.Persona.Apellido}"
                        : "Docente por asignar",
                    creditos = 4,
                    semana = 8,
                    totalSemanas = 16,
                    semestre = i.Catedra.Semestre,
                    grupo = "Grupo A"
                })
                .ToListAsync();

            return Ok(materias);
        }

        [HttpPost("ayudantias/{ayudantiaId}/bitacora-multipart")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> RegistrarBitacoraMultipart(int ayudantiaId, [FromForm] RegistrarBitacoraMultipartDto dto)
        {
            var authResult = await CheckAyudantiaOwnershipAsync(ayudantiaId);
            if (authResult != null) return authResult;

            var evidenciaUrl = string.Empty;
            if (dto.Archivo != null && dto.Archivo.Length > 0)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads", "bitacoras", $"ayudantia-{ayudantiaId}");
                Directory.CreateDirectory(uploadsFolder);
                var fileName = $"{Guid.NewGuid():N}_{Path.GetFileName(dto.Archivo.FileName)}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                await using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.Archivo.CopyToAsync(stream);
                }

                evidenciaUrl = $"/uploads/bitacoras/ayudantia-{ayudantiaId}/{fileName}";
            }

            var bitacora = new Bitacora
            {
                AyudantiaId = ayudantiaId,
                Fecha = DateTime.UtcNow,
                ActividadesRealizadas = dto.ActividadesRealizadas,
                EvidenciaUrl = evidenciaUrl
            };

            _context.Bitacoras.Add(bitacora);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Bitácora registrada con evidencia adjunta.", evidenciaUrl });
        }

        private async Task<IActionResult> CheckAyudantiaOwnershipAsync(int ayudantiaId)
        {
            if (EstudianteId == null)
            {
                return Unauthorized();
            }

            var isOwner = await _context.Ayudantias
                .AsNoTracking()
                .AnyAsync(a => a.Id == ayudantiaId && a.EstudianteId == EstudianteId.Value);

            if (!isOwner)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "No tienes permiso para realizar acciones sobre esta ayudantía." });
            }

            return null; // Null indica que la validación fue exitosa
        }
    }
}
