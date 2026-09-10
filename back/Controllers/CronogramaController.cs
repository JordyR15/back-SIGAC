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
    public class CronogramaController : ControllerBase
    {
        private readonly AppDbContext _context;

        public CronogramaController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/Cronograma/{catedraId}
        [HttpGet("{catedraId}")]
        public async Task<IActionResult> GetCronogramaByCatedra(int catedraId)
        {
            var items = await _context.Cronogramas
                .Where(c => c.CatedraId == catedraId)
                .Select(c => new CronogramaActividadDto
                {
                    Id = c.Id,
                    CatedraId = c.CatedraId,
                    Descripcion = c.Descripcion,
                    FechaPrevista = c.FechaPrevista,
                    FechaReal = c.FechaReal
                })
                .ToListAsync();

            return Ok(items);
        }

        // POST /api/Cronograma
        [HttpPost]
        public async Task<IActionResult> CreateCronogramaActividad([FromBody] CronogramaActividadDto dto)
        {
            var catedra = await _context.Catedras.FindAsync(dto.CatedraId);
            if (catedra == null) return NotFound("Cátedra no encontrada.");

            var actividad = new CronogramaActividad
            {
                CatedraId = dto.CatedraId,
                Descripcion = dto.Descripcion,
                FechaPrevista = dto.FechaPrevista,
                FechaReal = dto.FechaReal
            };

            _context.Cronogramas.Add(actividad);
            await _context.SaveChangesAsync();

            dto.Id = actividad.Id;
            return CreatedAtAction(nameof(GetCronogramaByCatedra), new { catedraId = dto.CatedraId }, dto);
        }
    }
}
