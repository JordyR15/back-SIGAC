using back.Data;
using back.DTOs;
using back.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace back.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class CronogramaController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CronogramaController(AppDbContext context)
        {
            _context = context;
        }

        // =========================================================
        // OBTENER CRONOGRAMA POR CÁTEDRA
        // GET /api/Cronograma/{catedraId}
        // =========================================================
        [HttpGet("{catedraId:int}")]
        public async Task<IActionResult> GetCronogramaByCatedra(int catedraId)
        {
            var existeCatedra = await _context.Catedras
                .AnyAsync(c => c.Id == catedraId);

            if (!existeCatedra)
            {
                return NotFound(new
                {
                    message = "Cátedra no encontrada."
                });
            }

            var items = await _context.Cronogramas
                .Where(c => c.CatedraId == catedraId)
                .OrderBy(c => c.FechaPrevista)
                .Select(c => new CronogramaActividadDto
                {
                    Id = c.Id,
                    CatedraId = c.CatedraId,
                    Descripcion = c.Descripcion,
                    FechaPrevista = c.FechaPrevista,
                    FechaReal = c.FechaReal,
                    ObservacionCambio = string.Empty
                })
                .ToListAsync();

            return Ok(items);
        }

        // =========================================================
        // CREAR ACTIVIDAD DE CRONOGRAMA
        // POST /api/Cronograma
        // =========================================================
        [HttpPost]
        public async Task<IActionResult> CreateCronogramaActividad(
            [FromBody] CronogramaActividadDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new
                {
                    message = "Datos del cronograma requeridos."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.Descripcion))
            {
                return BadRequest(new
                {
                    message = "La descripción de la actividad es obligatoria."
                });
            }

            if (dto.FechaPrevista == default)
            {
                return BadRequest(new
                {
                    message = "Debe indicar una fecha válida."
                });
            }

            var catedra = await _context.Catedras.FindAsync(dto.CatedraId);

            if (catedra == null)
            {
                return NotFound(new
                {
                    message = "Cátedra no encontrada."
                });
            }

            var actividad = new CronogramaActividad
            {
                CatedraId = dto.CatedraId,
                Descripcion = dto.Descripcion.Trim(),
                FechaPrevista = dto.FechaPrevista,
                FechaReal = dto.FechaReal
            };

            _context.Cronogramas.Add(actividad);
            await _context.SaveChangesAsync();

            dto.Id = actividad.Id;
            dto.Descripcion = actividad.Descripcion;

            return CreatedAtAction(
                nameof(GetCronogramaByCatedra),
                new
                {
                    catedraId = dto.CatedraId
                },
                dto);
        }

        // =========================================================
        // RF-005 - REPROGRAMAR CRONOGRAMA
        // PUT /api/Cronograma/{catedraId}/reprogramar
        // =========================================================
        [HttpPut("{catedraId:int}/reprogramar")]
        public async Task<IActionResult> ReprogramarCronograma(
            int catedraId,
            [FromBody] CronogramaActividadDto dto)
        {
            if (dto == null)
            {
                return BadRequest(new
                {
                    message = "Datos de reprogramación requeridos."
                });
            }

            if (dto.Id <= 0)
            {
                return BadRequest(new
                {
                    message = "Debe indicar la actividad que desea reprogramar."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.Descripcion))
            {
                return BadRequest(new
                {
                    message = "La actividad es obligatoria."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.ObservacionCambio))
            {
                return BadRequest(new
                {
                    message = "Debe indicar la observación o motivo del cambio."
                });
            }

            if (dto.FechaPrevista == default)
            {
                return BadRequest(new
                {
                    message = "La nueva fecha no es válida."
                });
            }

            if (dto.FechaPrevista.Date < DateTime.UtcNow.Date)
            {
                return BadRequest(new
                {
                    message = "No se puede reprogramar una actividad para una fecha pasada."
                });
            }

            var actividad = await _context.Cronogramas
                .FirstOrDefaultAsync(c =>
                    c.Id == dto.Id &&
                    c.CatedraId == catedraId);

            if (actividad == null)
            {
                return NotFound(new
                {
                    message = "Actividad del cronograma no encontrada."
                });
            }

            var fechaAnterior = actividad.FechaPrevista;
            var descripcionAnterior = actividad.Descripcion;

            if (fechaAnterior == dto.FechaPrevista &&
                string.Equals(
                    descripcionAnterior.Trim(),
                    dto.Descripcion.Trim(),
                    StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    message = "No existen cambios para registrar."
                });
            }

            var historial = new HistorialCronograma
            {
                CronogramaActividadId = actividad.Id,
                FechaAnterior = fechaAnterior,
                FechaNueva = dto.FechaPrevista,
                DescripcionAnterior = descripcionAnterior,
                DescripcionNueva = dto.Descripcion.Trim(),
                ObservacionCambio = dto.ObservacionCambio.Trim(),
                FechaModificacion = DateTime.UtcNow
            };

            _context.Set<HistorialCronograma>().Add(historial);

            actividad.Descripcion = dto.Descripcion.Trim();
            actividad.FechaPrevista = dto.FechaPrevista;
            actividad.FechaReal = dto.FechaReal;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "Cronograma reprogramado correctamente.",

                actividad = new
                {
                    actividad.Id,
                    actividad.CatedraId,
                    actividad.Descripcion,
                    actividad.FechaPrevista,
                    actividad.FechaReal
                },

                cambio = new
                {
                    historial.Id,
                    historial.FechaAnterior,
                    historial.FechaNueva,
                    historial.DescripcionAnterior,
                    historial.DescripcionNueva,
                    historial.ObservacionCambio,
                    historial.FechaModificacion
                },

                notificacionEstudiantes = true
            });
        }

        // =========================================================
        // RF-005 - HISTORIAL DE MODIFICACIONES
        // GET /api/Cronograma/{catedraId}/historial
        // =========================================================
        [HttpGet("{catedraId:int}/historial")]
        public async Task<IActionResult> GetHistorialCronograma(int catedraId)
        {
            var historial = await _context
                .Set<HistorialCronograma>()
                .Where(h =>
                    h.CronogramaActividad.CatedraId == catedraId)
                .OrderByDescending(h => h.FechaModificacion)
                .Select(h => new
                {
                    h.Id,
                    actividadId = h.CronogramaActividadId,
                    actividadActual = h.CronogramaActividad.Descripcion,
                    h.FechaAnterior,
                    h.FechaNueva,
                    h.DescripcionAnterior,
                    h.DescripcionNueva,
                    h.ObservacionCambio,
                    h.FechaModificacion
                })
                .ToListAsync();

            return Ok(historial);
        }

        // =========================================================
        // RF-005 - ACTUALIZACIONES PARA EL ESTUDIANTE
        // GET /api/Cronograma/estudiante/actualizaciones
        //
        // SOLO devuelve reprogramaciones reales guardadas en BD.
        // Además resuelve el MateriaId correspondiente.
        // =========================================================
        [HttpGet("estudiante/actualizaciones")]
        public async Task<IActionResult> GetActualizacionesEstudiante()
        {
            var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(claim, out var estudianteId))
            {
                return Unauthorized(new
                {
                    message = "No se pudo identificar al usuario."
                });
            }

            var catedrasIds = await _context.Inscripciones
                .Where(i =>
                    i.EstudianteId == estudianteId &&
                    i.CatedraId.HasValue)
                .Select(i => i.CatedraId!.Value)
                .Distinct()
                .ToListAsync();

            if (catedrasIds.Count == 0)
            {
                return Ok(Array.Empty<object>());
            }

            var cambios = await _context
                .Set<HistorialCronograma>()
                .AsNoTracking()
                .Where(h =>
                    catedrasIds.Contains(
                        h.CronogramaActividad.CatedraId))
                .OrderByDescending(h => h.FechaModificacion)
                .Select(h => new
                {
                    id = h.Id,
                    cronogramaActividadId = h.CronogramaActividadId,
                    catedraId = h.CronogramaActividad.CatedraId,
                    catedraNombre = h.CronogramaActividad.Catedra.Nombre,
                    actividad = h.DescripcionNueva,
                    fechaAnterior = h.FechaAnterior,
                    fechaNueva = h.FechaNueva,
                    observacion = h.ObservacionCambio,
                    fechaNotificacion = h.FechaModificacion
                })
                .Take(30)
                .ToListAsync();

            var materias = await _context.Materias
                .AsNoTracking()
                .Select(m => new
                {
                    m.Id,
                    m.Nombre
                })
                .ToListAsync();

            var actualizaciones = cambios
                .Select(cambio =>
                {
                    var materia = materias.FirstOrDefault(m =>
                        string.Equals(
                            m.Nombre?.Trim(),
                            cambio.catedraNombre?.Trim(),
                            StringComparison.OrdinalIgnoreCase));

                    return new
                    {
                        cambio.id,
                        cambio.cronogramaActividadId,
                        cambio.catedraId,
                        cambio.catedraNombre,

                        materiaId = materia?.Id,

                        materiaNombre =
                            materia?.Nombre ??
                            cambio.catedraNombre,

                        cambio.actividad,
                        cambio.fechaAnterior,
                        cambio.fechaNueva,
                        cambio.observacion,
                        cambio.fechaNotificacion,

                        mensaje =
                            "El cronograma de tu cátedra fue actualizado."
                    };
                })
                .ToList();

            return Ok(actualizaciones);
        }
    }
}