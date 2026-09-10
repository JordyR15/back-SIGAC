using back.Data;
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
    public class AyudanteController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AyudanteController(AppDbContext context)
        {
            _context = context;
        }

        private int? UserId => int.TryParse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out var id) ? id : (int?)null;

        // GET /api/Ayudante/clases : retorna las cátedras donde el usuario fue aprobado como ayudante
        [HttpGet("clases")]
        public async Task<IActionResult> GetClasesAyudante()
        {
            if (UserId == null) return Unauthorized();

            var ayudantias = await _context.Ayudantias
                .Where(a => a.EstudianteId == UserId.Value && (a.Estado == "Aprobada" || a.Estado == "Activa"))
                .Include(a => a.Catedra)
                    .ThenInclude(c => c.Docente)
                        .ThenInclude(d => d.Persona)
                .ToListAsync();

            var result = new List<object>();
            foreach (var a in ayudantias)
            {
                // Obtener estudiantes inscritos en la cátedra
                var estudiantes = await _context.Inscripciones
                    .Where(i => i.CatedraId == a.CatedraId)
                    .Include(i => i.Estudiante)
                        .ThenInclude(u => u.Persona)
                    .Select(i => new
                    {
                        id = i.Estudiante.Id,
                        username = i.Estudiante.Username,
                        nombreCompleto = i.Estudiante.Persona != null ? $"{i.Estudiante.Persona.Nombre} {i.Estudiante.Persona.Apellido}".Trim() : string.Empty,
                        correo = i.Estudiante.Persona != null ? i.Estudiante.Persona.Correo : string.Empty,
                        cedula = i.Estudiante.Persona != null ? (i.Estudiante.Persona.Cedula ?? string.Empty) : string.Empty
                    })
                    .ToListAsync();

                var horas = a.HorasAsignadas;

                result.Add(new
                {
                    ayudantiaId = a.Id,
                    catedraId = a.CatedraId,
                    nombreCatedra = a.Catedra?.Nombre ?? "Cátedra",
                    docente = a.Catedra?.Docente?.Persona != null ? $"{a.Catedra.Docente.Persona.Nombre} {a.Catedra.Docente.Persona.Apellido}" : "Docente",
                    estudiantes = estudiantes,
                    horasAsignadas = horas
                });
            }

            return Ok(result);
        }
    }
}
