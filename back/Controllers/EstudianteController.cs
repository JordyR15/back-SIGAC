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
    public class EstudianteController : ControllerBase
    {
        private readonly AppDbContext _context;

        public EstudianteController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("ayudantias/postulaciones")]
        public async Task<IActionResult> PostularAyudantia([FromBody] PostulacionAyudantiaDto postulacionDto)
        {
            var estudianteId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

            var ayudantia = new Ayudantia
            {
                CatedraId = postulacionDto.CatedraId,
                EstudianteId = estudianteId,
                Estado = "Pendiente"
            };

            _context.Ayudantias.Add(ayudantia);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Postulación enviada exitosamente." });
        }

        [HttpPost("ayudantias/bitacora")]
        public async Task<IActionResult> RegistrarEnBitacora([FromBody] RegistroBitacoraDto registroDto)
        {
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

        [HttpGet("ayudantias/informe-mensual")]
        public async Task<IActionResult> GenerarInformeMensual([FromQuery] int ayudantiaId, [FromQuery] int mes, [FromQuery] int anio)
        {
            var bitacoras = await _context.Bitacoras
                .Where(b => b.AyudantiaId == ayudantiaId && b.Fecha.Month == mes && b.Fecha.Year == anio)
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
    }
}
