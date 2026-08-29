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
            if (await UserExists(registerDto.Username))
            {
                return BadRequest("Username is already taken");
            }

            // If there are no users yet, allow creating the first Administrador
            var anyUsers = await _context.Users.AnyAsync();
            if (!anyUsers)
            {
                if (!string.Equals(registerDto.Rol, "Administrador", System.StringComparison.OrdinalIgnoreCase))
                    return BadRequest("El primer usuario debe ser Administrador.");
            }
            else
            {
                // Require authenticated caller for subsequent creations
                var callerId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(callerId)) return Unauthorized("Sólo usuarios autenticados pueden crear cuentas.");

                var callerRole = User.FindFirst(ClaimTypes.Role)?.Value ?? string.Empty;

                // Define allowed target roles per caller role
                string[] allowedTargets = callerRole switch
                {
                    "Administrador" => new[] { "Decano", "Coordinador", "Docente", "Estudiante" },
                    "Decano" => new[] { "Coordinador", "Docente", "Estudiante" },
                    "Coordinador" => new[] { "Docente", "Estudiante" },
                    "Docente" => new[] { "Estudiante" },
                    _ => new string[0]
                };

                if (!allowedTargets.Any(r => string.Equals(r, registerDto.Rol, System.StringComparison.OrdinalIgnoreCase)))
                {
                    return Forbid($"El rol '{callerRole}' no está autorizado para crear cuentas con rol '{registerDto.Rol}'.");
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
                Rol = registerDto.Rol,
                UserId = user.Id
            };

            _context.Personas.Add(persona);
            await _context.SaveChangesAsync();

            // Attach persona to user so the token includes role and the correct NameIdentifier claim (user id)
            user.Persona = persona;

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Token = _tokenService.CreateToken(user),
                Rol = persona.Rol,
                Nombre = persona.Nombre,
                Apellido = persona.Apellido,
                Correo = persona.Correo
            };
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
        {
            var user = await _context.Users
                .Include(u => u.Persona)
                .SingleOrDefaultAsync(x => x.Username == loginDto.Username);

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

            return new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Token = _tokenService.CreateToken(user),
                Rol = user.Persona?.Rol,
                Nombre = user.Persona?.Nombre,
                Apellido = user.Persona?.Apellido,
                Correo = user.Persona?.Correo
            };
        }

        private async Task<bool> UserExists(string username)
        {
            return await _context.Users.AnyAsync(x => x.Username == username.ToLower());
        }
    }
}
