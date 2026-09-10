using back.Data;
using back.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
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

        // Listar todas las personas registradas (Directorio General)
        [HttpGet]
        public async Task<IActionResult> GetPersonas([FromQuery] string? rol = null, [FromQuery] bool? me = false)
        {
            if (me == true)
            {
                return await GetMiPerfil();
            }

            var query = _context.Personas
                .Include(p => p.User)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(rol))
            {
                var rLower = rol.Trim().ToLower();
                query = query.Where(p => p.Rol.ToLower().Contains(rLower));
            }

            var personas = await query
                .Select(p => new
                {
                    id = p.Id,
                    personaId = p.Id,
                    userId = p.UserId,
                    nombre = p.Nombre,
                    apellido = p.Apellido,
                    nombreCompleto = $"{p.Nombre} {p.Apellido}".Trim(),
                    correo = p.Correo,
                    email = p.Correo,
                    rol = p.Rol,
                    roles = p.GetRoles(),
                    username = p.User != null ? p.User.Username : string.Empty
                })
                .ToListAsync();

            return Ok(personas);
        }

        [HttpGet("me")]
        [HttpGet("perfil")]
        public async Task<IActionResult> GetMiPerfil()
        {
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(userIdStr, out var userId)) return Unauthorized();

            var persona = await _context.Personas
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (persona == null) return NotFound("Datos personales no encontrados.");

            return Ok(new
            {
                id = persona.Id,
                personaId = persona.Id,
                userId = persona.UserId,
                nombre = persona.Nombre,
                apellido = persona.Apellido,
                nombreCompleto = $"{persona.Nombre} {persona.Apellido}".Trim(),
                correo = persona.Correo,
                email = persona.Correo,
                rol = persona.Rol,
                roles = persona.GetRoles(),
                username = persona.User != null ? persona.User.Username : string.Empty
            });
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetPersonaById(int id)
        {
            var p = await _context.Personas
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == id || p.UserId == id);

            if (p == null) return NotFound("Persona no encontrada.");

            return Ok(new
            {
                id = p.Id,
                personaId = p.Id,
                userId = p.UserId,
                nombre = p.Nombre,
                apellido = p.Apellido,
                nombreCompleto = $"{p.Nombre} {p.Apellido}".Trim(),
                correo = p.Correo,
                email = p.Correo,
                rol = p.Rol,
                roles = p.GetRoles(),
                username = p.User != null ? p.User.Username : string.Empty
            });
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

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePersona(long id)
        {
            if (id > int.MaxValue)
            {
                return Ok(new { success = true, message = "Registro eliminado del directorio" });
            }

            int intId = (int)id;
            var persona = await _context.Personas
                .Include(p => p.User)
                .FirstOrDefaultAsync(p => p.Id == intId || p.UserId == intId);

            var user = persona?.User ?? await _context.Users
                .Include(u => u.Inscripciones)
                .Include(u => u.AyudantiasEstudiante)
                    .ThenInclude(a => a.Bitacoras)
                .Include(u => u.AyudantiasEstudiante)
                    .ThenInclude(a => a.Presentaciones)
                .Include(u => u.ClasesEstudiante)
                .Include(u => u.AsistenciasEstudiante)
                .Include(u => u.RecursosVistos)
                .FirstOrDefaultAsync(u => u.Id == intId || (persona != null && u.Id == persona.UserId));

            if (persona == null && user == null)
            {
                return Ok(new { success = true, message = "Registro eliminado del directorio" });
            }

            if (user != null)
            {
                if (user.Inscripciones != null && user.Inscripciones.Any()) _context.Inscripciones.RemoveRange(user.Inscripciones);
                if (user.AsistenciasEstudiante != null && user.AsistenciasEstudiante.Any()) _context.Asistencias.RemoveRange(user.AsistenciasEstudiante);
                if (user.RecursosVistos != null && user.RecursosVistos.Any()) _context.RecursosVistosPorEstudiante.RemoveRange(user.RecursosVistos);
                if (user.AyudantiasEstudiante != null && user.AyudantiasEstudiante.Any())
                {
                    foreach (var a in user.AyudantiasEstudiante)
                    {
                        if (a.Bitacoras != null && a.Bitacoras.Any()) _context.Bitacoras.RemoveRange(a.Bitacoras);
                        if (a.Presentaciones != null && a.Presentaciones.Any()) _context.Presentaciones.RemoveRange(a.Presentaciones);
                    }
                    _context.Ayudantias.RemoveRange(user.AyudantiasEstudiante);
                }
                if (user.ClasesEstudiante != null && user.ClasesEstudiante.Any()) user.ClasesEstudiante.Clear();

                var actividadesRealizadas = await _context.EstudianteActividadesRealizadas.Where(e => e.EstudianteId == user.Id).ToListAsync();
                if (actividadesRealizadas.Any()) _context.EstudianteActividadesRealizadas.RemoveRange(actividadesRealizadas);

                if (persona != null) _context.Personas.Remove(persona);
                _context.Users.Remove(user);
            }
            else if (persona != null)
            {
                _context.Personas.Remove(persona);
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Registro eliminado del directorio" });
        }
    }
}
