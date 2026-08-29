using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using back.Data;
using back.DTOs;
using back.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly AppDbContext _context;
        public RolesController(AppDbContext context)
        {
            _context = context;
        }

        // PUT /api/roles/{userId} -> asigna/actualiza el rol de un usuario
        // Reglas:
        // - Solo usuarios autenticados pueden llamar
        // - Solo Administrador puede asignar el rol "Administrador"
        // - Además se valida que el rol objetivo esté dentro de los permitidos para el rol del llamador
        [HttpPut("{userId}")]
        public async Task<IActionResult> SetUserRole(int userId, [FromBody] SetUserRoleDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto?.Rol)) return BadRequest("Rol requerido.");

            var callerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var callerRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;
            if (string.IsNullOrEmpty(callerId)) return Unauthorized();

            // Si se intenta establecer Administrador, sólo Administrador puede hacerlo
            if (string.Equals(dto.Rol, "Administrador", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(callerRole, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid("Solo Administrador puede asignar el rol Administrador.");
            }

            // Definir roles permitidos según rol del llamador
            string[] allowedTargets = callerRole switch
            {
                "Administrador" => new[] { "Administrador", "Decano", "Coordinador", "Docente", "Estudiante" },
                "Decano" => new[] { "Coordinador", "Docente", "Estudiante" },
                "Coordinador" => new[] { "Docente", "Estudiante" },
                "Docente" => new[] { "Estudiante" },
                _ => Array.Empty<string>()
            };

            if (!allowedTargets.Any(r => string.Equals(r, dto.Rol, StringComparison.OrdinalIgnoreCase)))
            {
                return Forbid($"El rol '{callerRole}' no está autorizado para asignar el rol '{dto.Rol}'.");
            }

            var user = await _context.Users.Include(u => u.Persona).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound("Usuario no encontrado.");

            if (user.Persona == null)
            {
                return BadRequest("El usuario no tiene una entidad Persona asociada; no se puede asignar rol.");
            }

            user.Persona.Rol = dto.Rol;
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Rol del usuario {userId} actualizado a {dto.Rol}." });
        }
    }
}