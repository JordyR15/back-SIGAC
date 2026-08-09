using back.Data;
using back.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace back.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class PersonaController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PersonaController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetPersona()
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var persona = await _context.Personas
                .Where(p => p.UserId == userId)
                .Select(p => new PersonaDto
                {
                    Nombre = p.Nombre,
                    Apellido = p.Apellido,
                    Correo = p.Correo
                })
                .FirstOrDefaultAsync();

            if (persona == null) return NotFound("Datos personales no encontrados.");

            return Ok(persona);
        }

        [HttpPut]
        public async Task<IActionResult> UpdatePersona([FromBody] PersonaDto personaDto)
        {
            var userId = int.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
            var persona = await _context.Personas.FirstOrDefaultAsync(p => p.UserId == userId);

            if (persona == null) return NotFound("Datos personales no encontrados.");

            persona.Nombre = personaDto.Nombre;
            persona.Apellido = personaDto.Apellido;
            persona.Correo = personaDto.Correo;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Datos personales actualizados correctamente." });
        }
    }
}
