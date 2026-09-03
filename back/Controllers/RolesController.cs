using System;
using System.Collections.Generic;
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
            if (dto == null) return BadRequest("Datos requeridos.");

            var requestedRoles = dto.Roles?.Any() == true ? dto.Roles : new List<string> { dto.Rol };
            requestedRoles = requestedRoles
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (!requestedRoles.Any()) return BadRequest("Rol requerido.");

            var callerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var callerRoles = User.Claims.Where(c => c.Type == ClaimTypes.Role).Select(c => c.Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var callerRole = callerRoles.FirstOrDefault() ?? string.Empty;
            if (string.IsNullOrEmpty(callerId)) return Unauthorized();

            if (requestedRoles.Any(r => string.Equals(r, "Administrador", StringComparison.OrdinalIgnoreCase))
                && !string.Equals(callerRole, "Administrador", StringComparison.OrdinalIgnoreCase))
            {
                return Forbid("Solo Administrador puede asignar el rol Administrador.");
            }

            string[] allowedTargets = callerRole switch
            {
                "Administrador" => new[] { "Administrador", "Decano", "Coordinador", "Docente", "Estudiante" },
                "Decano" => new[] { "Coordinador", "Docente", "Estudiante" },
                "Coordinador" => new[] { "Docente", "Estudiante" },
                "Docente" => new[] { "Estudiante" },
                _ => Array.Empty<string>()
            };

            var invalidRole = requestedRoles.FirstOrDefault(r => !allowedTargets.Any(a => string.Equals(a, r, StringComparison.OrdinalIgnoreCase)));
            if (invalidRole != null)
            {
                return Forbid($"El rol '{callerRole}' no está autorizado para asignar el rol '{invalidRole}'.");
            }

            var user = await _context.Users.Include(u => u.Persona).FirstOrDefaultAsync(u => u.Id == userId);
            if (user == null) return NotFound("Usuario no encontrado.");

            if (user.Persona == null)
            {
                return BadRequest("El usuario no tiene una entidad Persona asociada; no se puede asignar rol.");
            }

            user.Persona.SetRoles(requestedRoles);
            await _context.SaveChangesAsync();

            return Ok(new { message = $"Roles del usuario {userId} actualizados: {string.Join(", ", requestedRoles)}." });
        }
    }
}