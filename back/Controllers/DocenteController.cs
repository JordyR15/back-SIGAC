using back.Data;
using back.DTOs;
using back.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;

namespace back.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class DocenteController : ControllerBase
    {
        private readonly AppDbContext _context;

        public DocenteController(AppDbContext context)
        {
            _context = context;
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
