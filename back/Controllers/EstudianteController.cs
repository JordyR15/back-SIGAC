using back.Data;
using back.DTOs;
using back.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic; // Añadido
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
                return Forbid("Debes estar inscrito en la cátedra para postular a ayudantía.");
            }

            var catedra = await _context.Catedras.FindAsync(postulacionDto.CatedraId);
            if (catedra != null && catedra.MinimoNota.HasValue && inscripcion.PromedioActual < catedra.MinimoNota.Value)
            {
                return Forbid("Tu promedio actual es menor que la nota mínima para postular a esta ayudantía.");
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
                    .ThenInclude(c => c.Docente) // Para obtener el nombre del docente
                .Select(a => new HistorialAyudantiaDto
                {
                    AyudantiaId = a.Id,
                    EstadoAyudantia = a.Estado,
                    CatedraId = a.CatedraId,
                    NombreCatedra = a.Catedra.Nombre,
                    SemestreCatedra = a.Catedra.Semestre,
                    DocenteCatedra = a.Catedra.Docente.Persona.Nombre + " " + a.Catedra.Docente.Persona.Apellido // Corregido: Nombres -> Nombre, Apellidos -> Apellido
                })
                .ToListAsync();

            if (!historial.Any())
            {
                return NotFound(new { message = "No se encontró historial de ayudantías para este estudiante." });
            }

            return Ok(historial);
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
                return Forbid("No tienes permiso para realizar acciones sobre esta ayudantía.");
            }

            return null; // Null indica que la validación fue exitosa
        }
    }
}
