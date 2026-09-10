using back.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System.Threading.Tasks;

namespace back.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DebugController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public DebugController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // GET /api/Debug/db-status?username=ryu
        // Endpoint de diagnóstico: no devuelve secrets, solo estado de conexión y existencia de usuario.
        [HttpGet("db-status")]
        public async Task<IActionResult> GetDbStatus([FromQuery] string? username = null)
        {
            var provider = _context.Database.ProviderName ?? string.Empty;
            var isRelational = _context.Database.IsRelational();
            var defaultConn = _config.GetConnectionString("DefaultConnection");
            var hasDefaultConnection = !string.IsNullOrWhiteSpace(defaultConn);

            bool userExists = false;
            int? userId = null;
            if (!string.IsNullOrWhiteSpace(username))
            {
                var normalized = username.Trim().ToLowerInvariant();
                var user = await _context.Users.Include(u => u.Persona)
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized || (u.Persona != null && u.Persona.Correo.ToLower() == normalized));
                if (user != null)
                {
                    userExists = true;
                    userId = user.Id;
                }
            }

            return Ok(new
            {
                provider,
                isRelational,
                hasDefaultConnection,
                queriedUsername = username,
                userExists,
                userId
            });
        }
    }
}
