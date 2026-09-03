using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using back.Data;
using back.DTOs;
using back.Entities;
using back.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace back.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class LoginController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ITokenService _tokenService;

        public LoginController(AppDbContext context, ITokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
        {
            if (registerDto == null)
            {
                return BadRequest("Datos de registro requeridos.");
            }

            var requestedRoles = GetRequestedRoles(registerDto.Roles, registerDto.Rol);
            if (!requestedRoles.Any())
            {
                return BadRequest("Debe indicar al menos un rol válido.");
            }

            if (await UserExists(registerDto.Username))
            {
                return BadRequest("Username is already taken");
            }

            var anyUsers = await _context.Users.AnyAsync();
            if (!anyUsers)
            {
                if (!requestedRoles.Any(r => string.Equals(r, "Administrador", System.StringComparison.OrdinalIgnoreCase)))
                    return BadRequest("El primer usuario debe ser Administrador.");
            }
            else
            {
                var callerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(callerId)) return Unauthorized("Sólo usuarios autenticados pueden crear cuentas.");

                var callerRoles = GetRolesFromClaims(User.Claims);
                var callerRole = callerRoles.FirstOrDefault() ?? string.Empty;

                string[] allowedTargets = callerRole switch
                {
                    "Administrador" => new[] { "Decano", "Coordinador", "Docente", "Estudiante" },
                    "Decano" => new[] { "Coordinador", "Docente", "Estudiante" },
                    "Coordinador" => new[] { "Docente", "Estudiante" },
                    "Docente" => new[] { "Estudiante" },
                    _ => new string[0]
                };

                var invalidRole = requestedRoles.FirstOrDefault(r => !allowedTargets.Any(a => string.Equals(a, r, System.StringComparison.OrdinalIgnoreCase)));
                if (invalidRole != null)
                {
                    return Forbid($"El rol '{callerRole}' no está autorizado para crear cuentas con rol '{invalidRole}'.");
                }
            }

            using var hmac = new HMACSHA512();

            var user = new User
            {
                Username = registerDto.Username.ToLower(),
                PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(registerDto.Password)),
                PasswordSalt = hmac.Key
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            var persona = new Persona
            {
                Nombre = registerDto.Nombre,
                Apellido = registerDto.Apellido,
                Correo = registerDto.Correo,
                UserId = user.Id
            };
            persona.SetRoles(requestedRoles);

            _context.Personas.Add(persona);
            await _context.SaveChangesAsync();

            user.Persona = persona;

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Token = _tokenService.CreateToken(user),
                Roles = persona.GetRoles(),
                Rol = persona.Rol,
                Nombre = persona.Nombre,
                Apellido = persona.Apellido,
                Correo = persona.Correo
            };
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
        {
            var username = loginDto.Username?.Trim();
            if (string.IsNullOrWhiteSpace(username))
            {
                return Unauthorized("Username requerido");
            }

            var normalizedUsername = username.ToLowerInvariant();

            var user = await _context.Users
                .Include(u => u.Persona)
                .SingleOrDefaultAsync(x => x.Username == normalizedUsername);

            if (user == null)
            {
                return Unauthorized("Invalid username");
            }

            using var hmac = new HMACSHA512(user.PasswordSalt);
            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(loginDto.Password));

            for (int i = 0; i < computedHash.Length; i++)
            {
                if (computedHash[i] != user.PasswordHash[i])
                {
                    return Unauthorized("Invalid password");
                }
            }

            var roles = user.Persona != null ? user.Persona.GetRoles() : new List<string>();

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Token = _tokenService.CreateToken(user),
                Roles = roles,
                Rol = user.Persona?.Rol ?? string.Empty,
                Nombre = user.Persona?.Nombre,
                Apellido = user.Persona?.Apellido,
                Correo = user.Persona?.Correo
            };
        }

        private static List<string> GetRequestedRoles(List<string> roles, string legacyRole)
        {
            var requested = new List<string>();
            if (roles != null)
            {
                requested.AddRange(roles.Where(r => !string.IsNullOrWhiteSpace(r)));
            }
            if (!string.IsNullOrWhiteSpace(legacyRole))
            {
                requested.AddRange(legacyRole.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
            }
            return requested
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> GetRolesFromClaims(IEnumerable<Claim> claims)
        {
            return claims
                .Where(claim => claim.Type == ClaimTypes.Role)
                .Select(claim => claim.Value)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private async Task<bool> UserExists(string username)
        {
            return await _context.Users.AnyAsync(x => x.Username == username.ToLower());
        }
    }
}
