using back.Data;
using back.DTOs;
using back.Entities;
using back.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace back.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Route("api/clases")]
    public class ClaseController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly ILogger<ClaseController> _logger;

        public ClaseController(AppDbContext context, IEmailService emailService, ILogger<ClaseController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        // POST /api/Clase/{claseId}/ayudante : Asignar directamente un ayudante a la cátedra/clase
        [HttpPost("{claseId}/ayudante")]
        public async Task<IActionResult> AssignAyudanteToClase(long claseId, [FromBody] JsonElement payload)
        {
            if (UserId == null) return Unauthorized();

            // Solo el docente de la clase o administrador puede asignar un ayudante
            var userRol = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRol != "Administrador" && !await IsDocenteOfClase(claseId))
            {
                return Forbid("Solo el docente de esta clase o un administrador puede asignar un ayudante.");
            }

            if (claseId > int.MaxValue) return BadRequest("Identificador de clase inválido.");
            int cId = (int)claseId;

            var clase = await _context.Clases.Include(c => c.Materia).FirstOrDefaultAsync(c => c.Id == cId);
            if (clase == null) return NotFound(new { message = "Clase no encontrada." });

            int? ayudanteId = null;
            string? ayudanteEmail = null;
            if (payload.ValueKind == JsonValueKind.Object)
            {
                if (payload.TryGetProperty("ayudanteId", out var pId) && pId.ValueKind == JsonValueKind.Number && pId.TryGetInt32(out var val)) ayudanteId = val;
                if (payload.TryGetProperty("ayudanteEmail", out var pEmail) && pEmail.ValueKind == JsonValueKind.String) ayudanteEmail = pEmail.GetString();
            }

            User ayudante = null;
            if (ayudanteId.HasValue)
            {
                ayudante = await _context.Users.Include(u => u.Persona).FirstOrDefaultAsync(u => u.Id == ayudanteId.Value);
            }
            if (ayudante == null && !string.IsNullOrWhiteSpace(ayudanteEmail))
            {
                var norm = ayudanteEmail!.Trim().ToLowerInvariant();
                ayudante = await _context.Users.Include(u => u.Persona).FirstOrDefaultAsync(u => (u.Persona != null && u.Persona.Correo.ToLower() == norm) || u.Username.ToLower() == norm);
            }

            if (ayudante == null) return NotFound(new { message = "Usuario ayudante no encontrado." });

            // Asegurar que el persona tenga el rol Ayudante
            if (ayudante.Persona != null)
            {
                var roles = ayudante.Persona.GetRoles();
                if (!roles.Contains("Ayudante"))
                {
                    roles.Add("Ayudante");
                    ayudante.Persona.SetRoles(roles);
                }
            }

            // Determinar cátedra asociada: preferir MateriaId si existe, sino usar clase.Id
            var catedraId = clase.MateriaId > 0 ? clase.MateriaId : clase.Id;

            // Verificar si ya existe una ayudantía activa para este estudiante en la cátedra
            var existe = await _context.Ayudantias.AnyAsync(a => a.CatedraId == catedraId && a.EstudianteId == ayudante.Id && (a.Estado == "Activa" || a.Estado == "Aprobada"));
            if (!existe)
            {
                var ayudantia = new Ayudantia
                {
                    CatedraId = catedraId,
                    EstudianteId = ayudante.Id,
                    Estado = "Activa",
                    HorasAsignadas = 0
                };
                _context.Ayudantias.Add(ayudantia);
                await _context.SaveChangesAsync();
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Ayudante asignado a la cátedra con estado Activa." });
        }

        private int? UserId
        {
            get
            {
                var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return int.TryParse(value, out var id) ? id : (int?)null;
            }
        }

        // Helper para verificar si el usuario es docente de la materia o de la clase
        private async Task<bool> IsDocenteOfMateria(long materiaId)
        {
            if (UserId == null) return false;
            if (materiaId > int.MaxValue) return true;
            int mId = (int)materiaId;
            return await _context.Catedras.AnyAsync(m => m.Id == mId && m.DocenteId == UserId.Value);
        }

        private async Task<bool> IsDocenteOfClase(long claseId)
        {
            if (UserId == null) return false;
            if (claseId > int.MaxValue) return true;
            int cId = (int)claseId;
            return await _context.Clases.AnyAsync(c => c.Id == cId && c.DocenteId == UserId.Value);
        }

        private async Task<User> GetDefaultDocenteAsync(long? requestedDocenteId = null)
        {
            if (requestedDocenteId.HasValue && requestedDocenteId.Value > 0 && requestedDocenteId.Value <= int.MaxValue)
            {
                var doc = await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u => u.Id == (int)requestedDocenteId.Value || (u.Persona != null && (u.Persona.Id == (int)requestedDocenteId.Value || u.Persona.UserId == (int)requestedDocenteId.Value)));
                if (doc != null) return doc;
            }

            // Buscar por correo docente@uteq.edu.ec
            var defaultDoc = await _context.Users
                .Include(u => u.Persona)
                .FirstOrDefaultAsync(u => (u.Persona != null && u.Persona.Correo.ToLower() == "docente@uteq.edu.ec") 
                                       || u.Username.ToLower() == "docente@uteq.edu.ec"
                                       || u.Username.ToLower() == "docente");
            if (defaultDoc != null) return defaultDoc;

            // Buscar cualquier usuario con rol Docente
            var anyDoc = await _context.Users
                .Include(u => u.Persona)
                .FirstOrDefaultAsync(u => u.Persona != null && (u.Persona.Rol == "Docente" || u.Persona.Rol.Contains("Docente")));
            if (anyDoc != null) return anyDoc;

            return await _context.Users.Include(u => u.Persona).FirstOrDefaultAsync();
        }

        // Endpoint para obtener todas las clases
        [HttpGet]
        public async Task<IActionResult> GetAllClases()
        {
            var clases = await _context.Clases
                .Include(c => c.Materia)
                .Include(c => c.Docente)
                    .ThenInclude(d => d.Persona)
                .Include(c => c.Estudiantes)
                    .ThenInclude(e => e.Persona)
                .ToListAsync();

            var result = clases.Select(c => new
            {
                id = c.Id,
                claseId = c.Id,
                nombre = c.Nombre,
                materiaId = c.MateriaId,
                materiaNombre = c.Materia != null ? c.Materia.Nombre : string.Empty,
                materiaCodigo = c.Materia != null ? c.Materia.Codigo : string.Empty,
                docenteNombre = c.Docente != null ? c.Docente.NombreCompleto : "Docente",
                docenteId = c.DocenteId,
                docenteEmail = c.Docente != null && c.Docente.Persona != null ? c.Docente.Persona.Correo : (c.Docente != null ? c.Docente.Username : string.Empty),
                materia = c.Materia != null ? c.Materia.Nombre : string.Empty,
                nombreMateria = c.Materia != null ? c.Materia.Nombre : string.Empty,
                codigoMateria = c.Materia != null ? c.Materia.Codigo : string.Empty,
                docente = c.Docente != null ? c.Docente.NombreCompleto : "Docente",
                aula = "Aula Principal",
                horario = "Horario Regular",
                paralelo = "A",
                estudiantesCount = c.Estudiantes.Count,
                estudianteIds = c.Estudiantes.Select(e => e.Id).ToList(),
                estudiantes = c.Estudiantes.Select(e => new
                {
                    id = e.Id,
                    username = e.Username,
                    nombre = e.Persona != null ? $"{e.Persona.Nombre} {e.Persona.Apellido}".Trim() : e.Username,
                    correo = e.Persona != null ? e.Persona.Correo : e.Username
                }).ToList()
            })
            .DistinctBy(c => c.id)
            .ToList();

            return Ok(result);
        }

        // Endpoint para obtener clases de un docente: GET /api/Clase/docente/{docenteId} o /api/clases/docente/{docenteId}
        [HttpGet("docente/{docenteId}")]
        public async Task<IActionResult> GetClasesByDocente(long docenteId)
        {
            var doc = await GetDefaultDocenteAsync(docenteId);
            int targetDocId = doc != null ? doc.Id : (int)docenteId;

            var clases = await _context.Clases
                .Include(c => c.Materia)
                .Include(c => c.Docente)
                    .ThenInclude(d => d.Persona)
                .Include(c => c.Estudiantes)
                    .ThenInclude(e => e.Persona)
                .Where(c => c.DocenteId == targetDocId)
                .ToListAsync();

            var result = clases.Select(c => new
            {
                id = c.Id,
                claseId = c.Id,
                nombre = c.Nombre,
                materiaId = c.MateriaId,
                materiaNombre = c.Materia != null ? c.Materia.Nombre : string.Empty,
                materiaCodigo = c.Materia != null ? c.Materia.Codigo : string.Empty,
                docenteNombre = c.Docente != null ? c.Docente.NombreCompleto : "Docente",
                docenteId = c.DocenteId,
                docenteEmail = c.Docente != null && c.Docente.Persona != null ? c.Docente.Persona.Correo : (c.Docente != null ? c.Docente.Username : string.Empty),
                materia = c.Materia != null ? c.Materia.Nombre : string.Empty,
                nombreMateria = c.Materia != null ? c.Materia.Nombre : string.Empty,
                codigoMateria = c.Materia != null ? c.Materia.Codigo : string.Empty,
                docente = c.Docente != null ? c.Docente.NombreCompleto : "Docente",
                aula = "Aula Principal",
                horario = "Horario Regular",
                paralelo = "A",
                estudiantesCount = c.Estudiantes.Count,
                estudianteIds = c.Estudiantes.Select(e => e.Id).ToList(),
                estudiantes = c.Estudiantes.Select(e => new
                {
                    id = e.Id,
                    username = e.Username,
                    nombreCompleto = e.Persona != null ? $"{e.Persona.Nombre} {e.Persona.Apellido}".Trim() : string.Empty,
                    nombre = e.Persona != null ? $"{e.Persona.Nombre} {e.Persona.Apellido}".Trim() : e.Username,
                    correo = e.Persona != null ? e.Persona.Correo : string.Empty,
                    cedula = e.Persona != null ? (e.Persona.Cedula ?? string.Empty) : string.Empty
                }).ToList()
            })
            .DistinctBy(c => c.id)
            .ToList();

            return Ok(result);
        }

        // Endpoint para crear una nueva instancia de Clase
        [HttpPost]
        public async Task<IActionResult> CreateClase([FromBody] CreateClaseDto dto)
        {
            if (dto == null) return BadRequest(new { message = "Datos de clase requeridos." });

            if ((!dto.MateriaId.HasValue || dto.MateriaId.Value == 0) && dto.CatedraId.HasValue)
            {
                dto.MateriaId = dto.CatedraId.Value;
            }

            // 1. Asociar por defecto al Docente titular si no se envía un docenteId válido
            var docente = await GetDefaultDocenteAsync(dto.DocenteId);
            if (docente == null)
            {
                return BadRequest(new { message = "No se encontró ningún docente disponible para asignar la clase." });
            }

            // 2. Resolver Materia de forma segura
            Materia materia = null;
            if (dto.MateriaId.HasValue && dto.MateriaId.Value > 0)
            {
                materia = await _context.Materias.FirstOrDefaultAsync(m => m.Id == dto.MateriaId.Value);
                if (materia == null)
                {
                    var catedra = await _context.Catedras.FirstOrDefaultAsync(c => c.Id == dto.MateriaId.Value);
                    if (catedra != null)
                    {
                        materia = new Materia
                        {
                            Nombre = catedra.Nombre,
                            Descripcion = "Cátedra Universitaria",
                            Codigo = $"CAT-{catedra.Id}",
                            DocenteResponsableId = docente.Id
                        };
                        _context.Materias.Add(materia);
                        await _context.SaveChangesAsync();
                        dto.MateriaId = materia.Id;
                    }
                }
            }

            if (materia == null && !string.IsNullOrWhiteSpace(dto.Nombre))
            {
                materia = await _context.Materias.FirstOrDefaultAsync(m => m.Nombre.ToLower() == dto.Nombre.Trim().ToLower());
                if (materia != null)
                {
                    dto.MateriaId = materia.Id;
                }
            }

            if (materia == null)
            {
                materia = await _context.Materias.FirstOrDefaultAsync();
                if (materia != null)
                {
                    dto.MateriaId = materia.Id;
                }
            }

            // 3. Asignar estrictamente clase.MateriaId = dto.MateriaId
            var nombreClase = !string.IsNullOrWhiteSpace(dto.Nombre)
                ? dto.Nombre.Trim()
                : (materia != null ? $"{materia.Nombre} - Paralelo A" : "Clase");

            var clase = new Clase
            {
                Nombre = nombreClase,
                MateriaId = dto.MateriaId ?? 0,
                DocenteId = docente.Id
            };

            // Añadir estudiantes si se proporcionan
            if (dto.EstudianteIds != null && dto.EstudianteIds.Any())
            {
                var estudiantes = await _context.Users
                                                .Where(u => dto.EstudianteIds.Contains(u.Id))
                                                .ToListAsync();
                foreach (var estudiante in estudiantes)
                {
                    clase.Estudiantes.Add(estudiante);
                }
            }

            _context.Clases.Add(clase);
            await _context.SaveChangesAsync();

            var responseObj = new
            {
                id = clase.Id,
                claseId = clase.Id,
                nombre = clase.Nombre,
                materiaId = clase.MateriaId,
                materiaNombre = materia != null ? materia.Nombre : string.Empty,
                materiaCodigo = materia != null ? materia.Codigo : string.Empty,
                docenteId = clase.DocenteId,
                docenteNombre = docente.NombreCompleto,
                docenteEmail = docente.Persona?.Correo ?? docente.Username,
                estudianteIds = clase.Estudiantes.Select(e => e.Id).ToList(),
                estudiantes = clase.Estudiantes.Select(e => new { id = e.Id, username = e.Username }).ToList()
            };

            return CreatedAtAction(nameof(GetClaseById), new { id = clase.Id }, responseObj);
        }

        // Endpoint para obtener una clase por ID
        [HttpGet("{id}")]
        public async Task<IActionResult> GetClaseById(long id)
        {
            if (UserId == null) return Unauthorized();

            if (id > int.MaxValue) return NotFound(new { message = "Clase no encontrada." });
            int cId = (int)id;

            var c = await _context.Clases
                .Include(c => c.Materia)
                .Include(c => c.Docente)
                    .ThenInclude(d => d.Persona)
                .Include(c => c.Estudiantes)
                    .ThenInclude(e => e.Persona)
                .AsNoTracking()
                .FirstOrDefaultAsync(cl => cl.Id == cId);

            if (c == null) return NotFound(new { message = "Clase no encontrada." });

            // Autorización: Docente de la clase o estudiante inscrito en la clase
            var isDocente = c.DocenteId == UserId.Value;
            var isStudent = c.Estudiantes.Any(e => e.Id == UserId.Value);

            if (!isDocente && !isStudent)
            {
                return Forbid("No tienes permiso para ver esta clase.");
            }

            var dto = new ClaseDto
            {
                Id = c.Id,
                ClaseId = c.Id,
                Nombre = c.Nombre,
                MateriaId = c.MateriaId,
                MateriaNombre = c.Materia != null ? c.Materia.Nombre : string.Empty,
                MateriaCodigo = c.Materia != null ? c.Materia.Codigo : string.Empty,
                DocenteId = c.DocenteId,
                DocenteNombre = c.Docente != null ? c.Docente.NombreCompleto : "Docente",
                DocenteEmail = c.Docente != null && c.Docente.Persona != null ? c.Docente.Persona.Correo : (c.Docente != null ? c.Docente.Username : string.Empty),
                EstudianteIds = c.Estudiantes.Select(e => e.Id).ToList()
            };

            return Ok(dto);
        }

        // Endpoint PUT para actualizar Clase (Soluciona Error 405 en updateClase)
        [HttpPut("{id}")]
        [HttpPut("/api/clases/{id}")]
        public async Task<IActionResult> UpdateClase(long id, [FromBody] CreateClaseDto dto)
        {
            if (id > int.MaxValue) return NotFound(new { message = "Clase no encontrada." });
            int cId = (int)id;

            var clase = await _context.Clases.FindAsync(cId);
            if (clase == null) return NotFound(new { message = "Clase no encontrada." });

            if (dto.MateriaId.HasValue && dto.MateriaId.Value > 0)
                clase.MateriaId = dto.MateriaId.Value;
            else if (dto.CatedraId.HasValue && dto.CatedraId.Value > 0)
                clase.MateriaId = dto.CatedraId.Value;

            if (!string.IsNullOrEmpty(dto.Nombre))
                clase.Nombre = dto.Nombre;

            if (dto.DocenteId.HasValue && dto.DocenteId.Value > 0)
                clase.DocenteId = dto.DocenteId.Value;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Clase actualizada exitosamente.", data = clase });
        }

        // Endpoint DELETE para Clases (Error 405)
        [HttpDelete("{id}")]
        [HttpDelete("/api/clases/{id}")]
        public async Task<IActionResult> DeleteClase(long id)
        {
            if (id > int.MaxValue) return NotFound(new { message = "Clase no encontrada." });
            int cId = (int)id;

            var clase = await _context.Clases.FindAsync(cId);
            if (clase == null) return NotFound(new { message = "Clase no encontrada." });

            var inscripciones = await _context.Inscripciones.Where(i => i.ClaseId == cId).ToListAsync();
            foreach (var ins in inscripciones)
            {
                ins.ClaseId = null;
            }

            _context.Clases.Remove(clase);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "Clase eliminada exitosamente." });
        }

        // Endpoint para añadir estudiantes a una clase existente (solo docentes de la clase)
        // Soporta tanto /api/Clase/{claseId}/estudiantes como /api/Docente/clases/{claseId}/estudiantes
        [HttpPost("{claseId}/estudiantes")]
        [HttpPost("/api/Docente/clases/{claseId}/estudiantes")]
        public async Task<IActionResult> AddEstudiantesToClase(long claseId, [FromBody] JsonElement payload)
        {
            if (UserId == null) return Unauthorized();
            if (!await IsDocenteOfClase(claseId)) return Forbid("Solo el docente de esta clase puede añadir estudiantes.");

            int cId = claseId <= int.MaxValue ? (int)claseId : 1;
            var clase = await _context.Clases
                                .Include(c => c.Estudiantes)
                                .Include(c => c.Materia)
                                .FirstOrDefaultAsync(c => c.Id == cId);

            if (clase == null) return NotFound(new { message = "Clase no encontrada." });

            var itemsToProcess = new List<InscribirEstudianteClaseDto>();

            if (payload.ValueKind == JsonValueKind.Array)
            {
                foreach (var el in payload.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var idNum))
                    {
                        itemsToProcess.Add(new InscribirEstudianteClaseDto { EstudianteId = idNum });
                    }
                    else if (el.ValueKind == JsonValueKind.Object)
                    {
                        itemsToProcess.Add(ParseDtoFromJsonElement(el));
                    }
                }
            }
            else if (payload.ValueKind == JsonValueKind.Number && payload.TryGetInt64(out var singleId))
            {
                itemsToProcess.Add(new InscribirEstudianteClaseDto { EstudianteId = singleId });
            }
            else if (payload.ValueKind == JsonValueKind.Object)
            {
                var dto = ParseDtoFromJsonElement(payload);
                if (dto.EstudianteIds != null && dto.EstudianteIds.Any())
                {
                    foreach (var id in dto.EstudianteIds)
                    {
                        itemsToProcess.Add(new InscribirEstudianteClaseDto { EstudianteId = id });
                    }
                }
                else
                {
                    itemsToProcess.Add(dto);
                }
            }

            if (!itemsToProcess.Any())
            {
                return BadRequest("No se proporcionaron datos de estudiantes a añadir.");
            }

            // Nombre de la cátedra para las notificaciones por correo
            string nombreCatedra = clase.Materia?.Nombre ?? clase.Nombre ?? "Cátedra Universitaria";
            var catedra = await _context.Catedras.FirstOrDefaultAsync(c => c.Id == clase.MateriaId || c.Nombre == nombreCatedra);
            if (catedra != null && !string.IsNullOrWhiteSpace(catedra.Nombre))
            {
                nombreCatedra = catedra.Nombre;
            }

            var procesados = new List<EstudianteProcesadoItem>();

            foreach (var item in itemsToProcess)
            {
                var (user, isNewOrWithoutCreds, tempPassword) = await ResolveOrCreateEstudianteAsync(item);
                if (user == null) continue;

                var yaEnEstaClase = await _context.Inscripciones.AnyAsync(i => i.EstudianteId == user.Id && i.ClaseId == cId)
                                    || clase.Estudiantes.Any(e => e.Id == user.Id);

                if (yaEnEstaClase)
                {
                    if (itemsToProcess.Count == 1)
                    {
                        return BadRequest(new { message = "El estudiante ya está inscrito en esta clase." });
                    }
                    continue;
                }

                // Añadir a la clase
                if (!clase.Estudiantes.Any(e => e.Id == user.Id))
                {
                    clase.Estudiantes.Add(user);
                }

                var nuevaInscripcion = new Inscripcion
                {
                    EstudianteId = user.Id,
                    ClaseId = cId,
                    CatedraId = catedra?.Id ?? (clase.MateriaId > 0 ? clase.MateriaId : cId),
                    PromedioActual = 75.0,
                    AlertaRendimiento = false
                };
                _context.Inscripciones.Add(nuevaInscripcion);

                var correoDestino = user.Persona?.Correo ?? user.Username;
                var nombreEstudiante = user.Persona != null ? $"{user.Persona.Nombre} {user.Persona.Apellido}".Trim() : user.Username;

                if (isNewOrWithoutCreds && !string.IsNullOrWhiteSpace(tempPassword))
                {
                    try
                    {
                        await _emailService.SendCredentialsAsync(correoDestino, nombreEstudiante, user.Username, tempPassword, "Estudiante");
                        _logger.LogInformation("Credenciales despachadas por correo a {Email}", correoDestino);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "No se pudo enviar el correo de credenciales a {Email}", correoDestino);
                    }
                }

                try
                {
                    var subject = $"Inscripción a cátedra: {nombreCatedra}";
                    var body = $"<p>Estimado/a <strong>{nombreEstudiante}</strong>,</p>" +
                               $"<p>Has sido inscrito a la cátedra: <strong>{nombreCatedra}</strong>.</p>" +
                               $"<p>Clase asignada: {clase.Nombre}</p>" +
                               $"<p>Ya puedes ingresar a la plataforma SIGAC para consultar el contenido y las sesiones de clase programadas.</p>";
                    await _emailService.SendEmailAsync(correoDestino, subject, body);
                    _logger.LogInformation("Notificación de inscripción a cátedra {Catedra} enviada a {Email}", nombreCatedra, correoDestino);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "No se pudo enviar la notificación de inscripción por correo a {Email}", correoDestino);
                }

                procesados.Add(new EstudianteProcesadoItem
                {
                    id = user.Id,
                    userId = user.Id,
                    username = user.Username,
                    nombre = user.Persona?.Nombre ?? string.Empty,
                    apellido = user.Persona?.Apellido ?? string.Empty,
                    correo = user.Persona?.Correo ?? string.Empty,
                    credencialesEnviadas = isNewOrWithoutCreds,
                    tempPassword = tempPassword,
                    temporalPassword = tempPassword
                });
            }

            if (!procesados.Any())
            {
                return BadRequest(new { message = "El estudiante ya está inscrito en esta clase." });
            }

            await _context.SaveChangesAsync();

            var firstWithCreds = procesados.FirstOrDefault(p => !string.IsNullOrEmpty(p.tempPassword));
            var fallbackUser = procesados.FirstOrDefault();

            return Ok(new
            {
                success = true,
                message = "Estudiantes añadidos exitosamente a la clase.",
                username = firstWithCreds?.username ?? fallbackUser?.username ?? string.Empty,
                tempPassword = firstWithCreds?.tempPassword ?? fallbackUser?.tempPassword ?? string.Empty,
                password = firstWithCreds?.tempPassword ?? fallbackUser?.tempPassword ?? string.Empty,
                totalProcesados = procesados.Count,
                estudiantes = procesados
            });
        }

        private InscribirEstudianteClaseDto ParseDtoFromJsonElement(JsonElement el)
        {
            var dto = new InscribirEstudianteClaseDto();
            if (el.TryGetProperty("estudianteId", out var pEstId) && pEstId.TryGetInt64(out var estId)) dto.EstudianteId = estId;
            else if (el.TryGetProperty("id", out var pId) && pId.TryGetInt64(out var idVal)) dto.EstudianteId = idVal;

            if (el.TryGetProperty("estudianteIds", out var pIds) && pIds.ValueKind == JsonValueKind.Array)
            {
                dto.EstudianteIds = new List<long>();
                foreach (var idEl in pIds.EnumerateArray())
                {
                    if (idEl.TryGetInt64(out var val)) dto.EstudianteIds.Add(val);
                }
            }

            if (el.TryGetProperty("nombre", out var pNom)) dto.Nombre = pNom.GetString();
            else if (el.TryGetProperty("nombres", out var pNoms)) dto.Nombre = pNoms.GetString();

            if (el.TryGetProperty("apellido", out var pApe)) dto.Apellido = pApe.GetString();
            else if (el.TryGetProperty("apellidos", out var pApes)) dto.Apellido = pApes.GetString();

            if (el.TryGetProperty("correo", out var pCor)) dto.Correo = pCor.GetString();
            else if (el.TryGetProperty("email", out var pMail)) dto.Correo = pMail.GetString();

            if (el.TryGetProperty("cedula", out var pCed)) dto.Cedula = pCed.GetString();
            if (el.TryGetProperty("username", out var pUser)) dto.Username = pUser.GetString();

            return dto;
        }

        private async Task<(User? user, bool isNewOrWithoutCreds, string? tempPassword)> ResolveOrCreateEstudianteAsync(InscribirEstudianteClaseDto dto)
        {
            User? user = null;
            bool isNewOrWithoutCreds = false;
            string? tempPassword = null;

            // 1. Buscar por ID
            if (dto.EstudianteId.HasValue && dto.EstudianteId.Value > 0)
            {
                if (dto.EstudianteId.Value <= int.MaxValue)
                {
                    int sId = (int)dto.EstudianteId.Value;
                    user = await _context.Users
                        .Include(u => u.Persona)
                        .FirstOrDefaultAsync(u => u.Id == sId || (u.Persona != null && (u.Persona.Id == sId || u.Persona.UserId == sId)));
                }
            }

            // 2. Buscar por Correo o Username
            if (user == null && !string.IsNullOrWhiteSpace(dto.Correo))
            {
                var normEmail = dto.Correo.Trim().ToLowerInvariant();
                user = await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u => (u.Persona != null && u.Persona.Correo.ToLower() == normEmail) || u.Username.ToLower() == normEmail);
            }

            // 3. Buscar por Cédula
            if (user == null && !string.IsNullOrWhiteSpace(dto.Cedula))
            {
                var normCed = dto.Cedula.Trim();
                user = await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u => (u.Persona != null && u.Persona.Cedula == normCed) || u.Username == normCed);
            }

            // 4. Buscar por Username
            if (user == null && !string.IsNullOrWhiteSpace(dto.Username))
            {
                var normUser = dto.Username.Trim().ToLowerInvariant();
                user = await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u => u.Username.ToLower() == normUser || (u.Persona != null && u.Persona.Cedula == normUser));
            }

            // Si el usuario existe
            if (user != null)
            {
                if (user.Persona != null && string.IsNullOrWhiteSpace(user.Persona.Cedula) && !string.IsNullOrWhiteSpace(dto.Cedula))
                {
                    user.Persona.Cedula = dto.Cedula.Trim();
                }

                if (user.PasswordSalt == null || user.PasswordSalt.Length == 0 || user.PasswordHash == null || user.PasswordHash.Length == 0)
                {
                    isNewOrWithoutCreds = true;
                    tempPassword = $"Uteq.{RandomNumberGenerator.GetInt32(100000, 999999)}!";
                    using var hmac = new HMACSHA512();
                    user.PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(tempPassword));
                    user.PasswordSalt = hmac.Key;
                }
                return (user, isNewOrWithoutCreds, tempPassword);
            }

            // Si el usuario no existe, crearlo como nuevo estudiante si tenemos datos mínimos
            if (!string.IsNullOrWhiteSpace(dto.Correo) || !string.IsNullOrWhiteSpace(dto.Nombre))
            {
                isNewOrWithoutCreds = true;
                var cleanEmail = !string.IsNullOrWhiteSpace(dto.Correo) 
                    ? dto.Correo.Trim() 
                    : $"estudiante.{RandomNumberGenerator.GetInt32(1000, 9999)}@uteq.edu.ec";
                var cleanNombre = !string.IsNullOrWhiteSpace(dto.Nombre) ? dto.Nombre.Trim() : "Estudiante";
                var cleanApellido = !string.IsNullOrWhiteSpace(dto.Apellido) ? dto.Apellido.Trim() : "Nuevo";

                var baseUsername = !string.IsNullOrWhiteSpace(dto.Username)
                    ? dto.Username.Trim().ToLowerInvariant()
                    : cleanEmail.Contains("@") ? cleanEmail.Split('@')[0].ToLowerInvariant() : $"est.{cleanNombre.ToLowerInvariant()}";

                var candidateUsername = baseUsername;
                int counter = 1;
                while (await _context.Users.AnyAsync(u => u.Username == candidateUsername))
                {
                    candidateUsername = $"{baseUsername}{counter++}";
                }

                tempPassword = $"Uteq.{RandomNumberGenerator.GetInt32(100000, 999999)}!";
                using var hmac = new HMACSHA512();

                user = new User
                {
                    Username = candidateUsername,
                    PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(tempPassword)),
                    PasswordSalt = hmac.Key
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                var persona = new Persona
                {
                    Nombre = cleanNombre,
                    Apellido = cleanApellido,
                    Correo = cleanEmail,
                    Cedula = !string.IsNullOrWhiteSpace(dto.Cedula) ? dto.Cedula.Trim() : null,
                    Rol = "Estudiante",
                    UserId = user.Id
                };
                _context.Personas.Add(persona);
                await _context.SaveChangesAsync();
                user.Persona = persona;

                return (user, isNewOrWithoutCreds, tempPassword);
            }

            return (null, false, null);
        }

        // Endpoint para obtener los estudiantes de una clase (docentes de la clase o estudiantes de la clase)
        [HttpGet("{claseId}/estudiantes")]
        public async Task<ActionResult<IEnumerable<UserDto>>> GetEstudiantesFromClase(long claseId)
        {
            if (UserId == null) return Unauthorized();

            if (claseId > int.MaxValue) return Ok(new List<UserDto>());
            int cId = (int)claseId;

            var clase = await _context.Clases
                                .Include(c => c.Estudiantes)
                                .AsNoTracking()
                                .FirstOrDefaultAsync(c => c.Id == cId);

            if (clase == null) return NotFound(new { message = "Clase no encontrada." });

            var isDocente = clase.DocenteId == UserId.Value;
            var isStudent = clase.Estudiantes.Any(e => e.Id == UserId.Value) ||
                            await _context.Inscripciones.AnyAsync(i => i.EstudianteId == UserId.Value && i.ClaseId == cId);

            if (!isDocente && !isStudent)
            {
                return Forbid("No tienes permiso para ver los estudiantes de esta clase.");
            }

            var estudiantesFromClase = clase.Estudiantes.ToList();
            var estudiantesFromInscripciones = await _context.Inscripciones
                .Where(i => i.ClaseId == cId)
                .Include(i => i.Estudiante)
                .Select(i => i.Estudiante)
                .ToListAsync();

            var estudiantesDto = estudiantesFromClase
                .Concat(estudiantesFromInscripciones)
                .Where(e => e != null)
                .DistinctBy(e => e.Id)
                .Select(e => new UserDto
                {
                    Id = e.Id,
                    Username = e.Username
                }).ToList();

            return Ok(estudiantesDto);
        }

        // Endpoint para desvincular estudiante de una clase (docente de la clase o administrador)
        [HttpDelete("{claseId}/estudiantes/{estudianteId}")]
        [HttpDelete("/api/Docente/clases/{claseId}/estudiantes/{estudianteId}")]
        public async Task<IActionResult> RemoveEstudianteFromClase(long claseId, long estudianteId)
        {
            if (UserId == null) return Unauthorized();

            if (claseId > int.MaxValue)
            {
                return Ok(new { success = true, message = "Estudiante removido" });
            }

            int cId = (int)claseId;
            var clase = await _context.Clases
                .Include(c => c.Estudiantes)
                .FirstOrDefaultAsync(c => c.Id == cId);

            if (clase == null)
            {
                return Ok(new { success = true, message = "Estudiante removido" });
            }

            var userRol = User.FindFirst(ClaimTypes.Role)?.Value;
            if (userRol != "Administrador" && !await IsDocenteOfClase(claseId))
            {
                return Forbid("Solo el docente de esta clase o un administrador puede desvincular estudiantes.");
            }

            if (estudianteId > int.MaxValue)
            {
                return Ok(new { success = true, message = "Estudiante removido" });
            }

            int eId = (int)estudianteId;
            var estudiante = clase.Estudiantes.FirstOrDefault(e => e.Id == eId);
            if (estudiante == null)
            {
                var persona = await _context.Personas.FirstOrDefaultAsync(p => p.Id == eId);
                if (persona != null)
                {
                    estudiante = clase.Estudiantes.FirstOrDefault(e => e.Id == persona.UserId);
                }
            }

            if (estudiante == null)
            {
                return Ok(new { success = true, message = "Estudiante removido" });
            }

            clase.Estudiantes.Remove(estudiante);

            var inscripcion = await _context.Inscripciones
                .FirstOrDefaultAsync(i => i.EstudianteId == estudiante.Id && i.CatedraId == clase.MateriaId);
            if (inscripcion != null)
            {
                _context.Inscripciones.Remove(inscripcion);
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Estudiante removido" });
        }

        // Endpoint para obtener las clases en las que está registrado el estudiante
        [HttpGet("estudiante/{id}")]
        [HttpGet("/api/estudiante/{id}/clases")]
        public async Task<IActionResult> GetClasesEstudiante(long id)
        {
            int intId = id <= int.MaxValue ? (int)id : 0;
            var user = await _context.Users
                .Include(u => u.Persona)
                .FirstOrDefaultAsync(u => u.Id == intId || (u.Persona != null && (u.Persona.Id == intId || u.Persona.UserId == intId)));

            var targetId = user != null ? user.Id : intId;

            var clases = await _context.Clases
                .Include(c => c.Materia)
                .Include(c => c.Docente)
                    .ThenInclude(d => d.Persona)
                .Include(c => c.Estudiantes)
                .Where(c => c.Estudiantes.Any(e => e.Id == targetId) || _context.Inscripciones.Any(i => i.EstudianteId == targetId && (i.ClaseId == c.Id || i.CatedraId == c.MateriaId)))
                .Select(c => new
                {
                    id = c.Id,
                    claseId = c.Id,
                    nombre = c.Nombre,
                    materiaId = c.MateriaId,
                    materia = c.Materia != null ? c.Materia.Nombre : "",
                    nombreMateria = c.Materia != null ? c.Materia.Nombre : "",
                    docenteId = c.DocenteId,
                    docente = c.Docente != null && c.Docente.Persona != null
                        ? $"{c.Docente.Persona.Nombre} {c.Docente.Persona.Apellido}".Trim()
                        : "Docente asignado",
                    aula = "Aula Principal",
                    horario = "Horario Regular",
                    paralelo = "A"
                })
                .ToListAsync();

            var clasesDistinct = clases.DistinctBy(c => c.id).ToList();

            return Ok(clasesDistinct);
        }
    }

    public class EstudianteProcesadoItem
    {
        public long id { get; set; }
        public long userId { get; set; }
        public string username { get; set; } = string.Empty;
        public string nombre { get; set; } = string.Empty;
        public string apellido { get; set; } = string.Empty;
        public string correo { get; set; } = string.Empty;
        public bool credencialesEnviadas { get; set; }
        public string? tempPassword { get; set; }
        public string? temporalPassword { get; set; }
    }
}
