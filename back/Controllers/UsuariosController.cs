using back.Data;
using back.DTOs;
using back.Entities;
using back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace back.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class UsuariosController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UsuariosController(AppDbContext context)
        {
            _context = context;
        }

        // GET /api/Usuarios
        [HttpGet]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> GetUsuarios([FromQuery] string? rol = null)
        {
            var query = _context.Users.Include(u => u.Persona).AsQueryable();
            if (!string.IsNullOrWhiteSpace(rol))
            {
                var r = rol.Trim().ToLower();
                query = query.Where(u => u.Persona != null && u.Persona.Rol.ToLower().Contains(r));
            }

            var users = await query.Select(u => new
            {
                id = u.Id,
                username = u.Username,
                nombre = u.Persona != null ? u.Persona.Nombre : string.Empty,
                apellido = u.Persona != null ? u.Persona.Apellido : string.Empty,
                correo = u.Persona != null ? u.Persona.Correo : string.Empty,
                roles = u.Persona != null ? u.Persona.GetRoles() : new List<string>()
            }).ToListAsync();

            return Ok(users);
        }

        // POST /api/Usuarios
        [HttpPost]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> CreateUsuario([FromBody] RegisterDto dto)
        {
            if (dto == null) return BadRequest("Datos requeridos");

            if (await _context.Users.AnyAsync(u => u.Username == dto.Username.ToLower()))
                return BadRequest(new { message = "Username ya existe" });

            using var hmac = new HMACSHA512();
            var user = new User
            {
                Username = dto.Username.ToLower(),
                PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(dto.Password)),
                PasswordSalt = hmac.Key
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var persona = new Persona
            {
                Nombre = dto.Nombre,
                Apellido = dto.Apellido,
                Correo = dto.Correo,
                UserId = user.Id
            };
            persona.SetRoles(dto.Roles ?? new List<string>());

            _context.Personas.Add(persona);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetUsuarios), new { id = user.Id }, new { id = user.Id, username = user.Username });
        }

        // PUT /api/Usuarios/{id}
        [HttpPut("{id}")]
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> UpdateUsuario(int id, [FromBody] PersonaDto personaDto)
        {
            var user = await _context.Users.Include(u => u.Persona).FirstOrDefaultAsync(u => u.Id == id);
            if (user == null) return NotFound("Usuario no encontrado.");

            if (user.Persona == null)
            {
                user.Persona = new Persona { UserId = user.Id };
                _context.Personas.Add(user.Persona);
            }

            user.Persona.Nombre = personaDto.Nombre;
            user.Persona.Apellido = personaDto.Apellido;
            user.Persona.Correo = personaDto.Correo;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Usuario actualizado." });
        }
    }
}
