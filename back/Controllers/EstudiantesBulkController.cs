using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using back.Data;
using back.DTOs;
using back.Entities;
using back.Services;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace back.Controllers
{
    [ApiController]
    [Route("api/estudiantes")]
    [Authorize]
    public class EstudiantesBulkController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;
        private readonly ILogger<EstudiantesBulkController> _logger;

        public EstudiantesBulkController(
            AppDbContext context,
            IConfiguration config,
            IEmailService emailService,
            ILogger<EstudiantesBulkController> logger)
        {
            _context = context;
            _config = config;
            _emailService = emailService;
            _logger = logger;
        }

        // GET /api/estudiantes/template -> descarga CSV plantilla
        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            var csv = "nombres,apellidos,cedula,correo\nJordy Fabian,Rivas Bodero,1207751023,jordy@example.com\n";
            var bytes = Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", "estudiantes_plantilla.csv");
        }

        // Endpoint para listar presentaciones convocadas (evita el error 405)
        [HttpGet("presentaciones")]
        [Authorize]
        public async Task<IActionResult> GetPresentaciones()
        {
            var presentaciones = await _context.Presentaciones
                .Include(p => p.Ayudantia)
                    .ThenInclude(a => a.Catedra)
                .Include(p => p.Ayudantia)
                    .ThenInclude(a => a.Estudiante)
                        .ThenInclude(e => e.Persona)
                .Include(p => p.Jurados)
                    .ThenInclude(j => j.Persona)
                .Select(p => new
                {
                    p.Id,
                    p.AyudantiaId,
                    p.Fecha,
                    Catedra = p.Ayudantia.Catedra.Nombre,
                    Postulante = p.Ayudantia.Estudiante.Persona.Nombre + " " + p.Ayudantia.Estudiante.Persona.Apellido,
                    Jurados = p.Jurados.Select(j => j.Persona.Nombre + " " + j.Persona.Apellido).ToList()
                })
                .ToListAsync();

            return Ok(presentaciones);
        }

        // GET /api/estudiantes/imports/{jobId}/result -> descarga CSV resultado (solo creador o Administrador)
        [HttpGet("imports/{jobId}/result")]
        public async Task<IActionResult> DownloadImportResult(int jobId)
        {
            var job = await _context.ImportJobs.Include(j => j.Entries).FirstOrDefaultAsync(j => j.Id == jobId);
            if (job == null) return NotFound("Import job not found.");

            var callerIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            int.TryParse(callerIdStr, out var callerUserId);
            var callerRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

            if (!string.Equals(callerRole, "Administrador", StringComparison.OrdinalIgnoreCase) && job.CreatedByUserId != callerUserId)
            {
                return Forbid("No autorizado para descargar este resultado.");
            }

            if (string.IsNullOrEmpty(job.ResultFileName)) return NotFound("No hay archivo de resultado disponible.");

            var path = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", job.ResultFileName.Replace('\\', Path.DirectorySeparatorChar));
            if (!System.IO.File.Exists(path)) return NotFound("Archivo de resultado no encontrado en el servidor.");

            var bytes = await System.IO.File.ReadAllBytesAsync(path);
            return File(bytes, "text/csv", Path.GetFileName(path));
        }

        // POST /api/estudiantes/bulk-upload (multipart/form-data file)
        // Allowed caller roles: Administrador, Decano, Coordinador, Docente
        [HttpPost("bulk-upload")]
        public async Task<ActionResult<BulkUploadResultDto>> BulkUpload(IFormFile file, [FromQuery] long? claseId = null)
        {
            var callerRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
            var allowed = new[] { "Administrador", "Decano", "Coordinador", "Docente" };
            if (!allowed.Any(r => string.Equals(r, callerRole, StringComparison.OrdinalIgnoreCase)))
            {
                return Forbid("No autorizado para subir estudiantes en bloque.");
            }

            if (file == null || file.Length == 0) return BadRequest("Archivo requerido.");

            long? targetClaseId = claseId;
            if (!targetClaseId.HasValue && Request.HasFormContentType && Request.Form.TryGetValue("claseId", out var formClaseIdVal))
            {
                if (long.TryParse(formClaseIdVal, out var parsedClaseId))
                {
                    targetClaseId = parsedClaseId;
                }
            }

            var result = new BulkUploadResultDto();

            try
            {
                List<(string Nombres, string Apellidos, string Cedula, string Correo, string? Username)> rows = new();

                using (var stream = file.OpenReadStream())
                {
                    if (file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        var header = await reader.ReadLineAsync();
                        string? line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            var parts = line.Split(',');
                            if (parts.Length < 4) { result.Errors.Add($"Línea inválida: {line}"); continue; }
                            var uVal = parts.Length > 4 ? parts[4].Trim() : null;
                            rows.Add((parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), parts[3].Trim(), string.IsNullOrWhiteSpace(uVal) ? null : uVal));
                        }
                    }
                    else // assume Excel (.xlsx)
                    {
                        using var workbook = new XLWorkbook(stream);
                        var ws = workbook.Worksheets.First();
                        var firstRowUsed = ws.FirstRowUsed().RowNumber();
                        var lastRow = ws.LastRowUsed().RowNumber();

                        // Assume header in first row; data starts at firstRowUsed+1
                        for (int r = firstRowUsed + 1; r <= lastRow; r++)
                        {
                            var nombres = ws.Cell(r, 1).GetString();
                            var apellidos = ws.Cell(r, 2).GetString();
                            var cedula = ws.Cell(r, 3).GetString();
                            var correo = ws.Cell(r, 4).GetString();
                            var uVal = ws.Cell(r, 5).GetString();

                            if (string.IsNullOrWhiteSpace(nombres) || string.IsNullOrWhiteSpace(apellidos) || string.IsNullOrWhiteSpace(cedula))
                            {
                                result.Errors.Add($"Fila {r}: faltan datos obligatorios");
                                continue;
                            }

                            rows.Add((nombres.Trim(), apellidos.Trim(), cedula.Trim(), correo?.Trim() ?? string.Empty, string.IsNullOrWhiteSpace(uVal) ? null : uVal.Trim()));
                        }
                    }
                }

                // Validate batch size
                var maxRows = int.TryParse(_config["BulkUpload:MaxRows"], out var m) ? m : 500;
                if (rows.Count > maxRows)
                {
                    return BadRequest($"El archivo contiene {rows.Count} filas, el máximo permitido es {maxRows}.");
                }

                // Optional allowed domains (comma separated)
                var allowedDomainsConfig = _config["BulkUpload:AllowedEmailDomains"] ?? string.Empty;
                var allowedDomains = allowedDomainsConfig.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim().ToLowerInvariant()).ToArray();

                // Create ImportJob
                var callerIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                int.TryParse(callerIdStr, out var callerUserId);

                var job = new Entities.ImportJob
                {
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = callerUserId,
                    FileName = file.FileName,
                    ResultFileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}_resultado.csv",
                    CreatedCount = 0,
                    ErrorCount = 0
                };

                _context.ImportJobs.Add(job);
                await _context.SaveChangesAsync(); // get job id

                Clase? targetClase = null;
                if (targetClaseId.HasValue && targetClaseId.Value > 0)
                {
                    targetClase = await _context.Clases
                        .Include(c => c.Estudiantes)
                        .FirstOrDefaultAsync(c => c.Id == (int)targetClaseId.Value);
                }

                foreach (var row in rows)
                {
                    var u = row.Username;
                    var correo = row.Correo;
                    var initialUsername = !string.IsNullOrEmpty(u) ? u : (correo ?? "").Split('@')[0];
                    if (string.IsNullOrWhiteSpace(initialUsername))
                    {
                        initialUsername = !string.IsNullOrWhiteSpace(row.Cedula) ? row.Cedula : $"estudiante_{Guid.NewGuid().ToString("N")[..6]}";
                    }

                    var entry = new Entities.ImportJobEntry
                    {
                        ImportJobId = job.Id,
                        Nombres = row.Nombres,
                        Apellidos = row.Apellidos,
                        Cedula = row.Cedula,
                        Correo = row.Correo,
                        Username = initialUsername
                    };

                    try
                    {
                        // Normalize cedula: keep digits only
                        var cedulaNormalized = new string(row.Cedula.Where(char.IsDigit).ToArray());
                        if (string.IsNullOrWhiteSpace(cedulaNormalized) || cedulaNormalized.Length < 6)
                        {
                            entry.Success = false;
                            entry.ErrorMessage = "Cédula inválida o demasiado corta.";
                            job.Entries.Add(entry);
                            _context.ImportJobEntries.Add(entry);
                            await _context.SaveChangesAsync();
                            continue;
                        }

                        // Validate correo present
                        if (string.IsNullOrWhiteSpace(row.Correo))
                        {
                            entry.Success = false;
                            entry.ErrorMessage = "Correo vacío.";
                            job.Entries.Add(entry);
                            _context.ImportJobEntries.Add(entry);
                            await _context.SaveChangesAsync();
                            continue;
                        }

                        // Validate correo format
                        try
                        {
                            var maddr = new MailAddress(row.Correo);
                            var domain = maddr.Host.ToLowerInvariant();
                            if (allowedDomains.Length > 0 && !allowedDomains.Contains(domain))
                            {
                                entry.Success = false;
                                entry.ErrorMessage = $"Dominio de correo '{domain}' no permitido.";
                                job.Entries.Add(entry);
                                _context.ImportJobEntries.Add(entry);
                                await _context.SaveChangesAsync();
                                continue;
                            }
                        }
                        catch
                        {
                            entry.Success = false;
                            entry.ErrorMessage = "Formato de correo inválido.";
                            job.Entries.Add(entry);
                            _context.ImportJobEntries.Add(entry);
                            await _context.SaveChangesAsync();
                            continue;
                        }

                        // Check if student already exists by cedula or email
                        User? estudiante = null;
                        var existingPersona = await _context.Personas
                            .Include(p => p.User)
                            .FirstOrDefaultAsync(p => p.Cedula == cedulaNormalized || p.Correo.ToLower() == row.Correo.ToLower());

                        if (existingPersona != null && existingPersona.User != null)
                        {
                            estudiante = existingPersona.User;
                        }
                        else
                        {
                            var existingUser = await _context.Users
                                .Include(u => u.Persona)
                                .FirstOrDefaultAsync(u => u.Username.ToLower() == row.Correo.ToLower());
                            if (existingUser != null)
                            {
                                estudiante = existingUser;
                            }
                        }

                        bool isNew = false;
                        var emailSent = false;
                        string username = string.Empty;

                        if (estudiante == null)
                        {
                            isNew = true;
                            var usernameBase = !string.IsNullOrWhiteSpace(u)
                                ? u.Trim().ToLowerInvariant()
                                : (!string.IsNullOrWhiteSpace(row.Correo) && row.Correo.Contains('@') ? row.Correo.Split('@')[0].ToLowerInvariant() : GenerateUsername(row.Nombres, row.Apellidos));
                            username = await MakeUniqueUsernameAsync(usernameBase);
                            initialUsername = username;

                            // Clave temporal institucional: Uteq.XXXXXX! con 6 dígitos aleatorios
                            var tempPassword = $"Uteq.{RandomNumberGenerator.GetInt32(100000, 999999)}!";

                            using var hmac = new HMACSHA512();
                            var user = new User
                            {
                                Username = username,
                                PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(tempPassword)),
                                PasswordSalt = hmac.Key
                            };

                            _context.Users.Add(user);
                            await _context.SaveChangesAsync(); // need Id for persona

                            var persona = new Persona
                            {
                                Nombre = row.Nombres,
                                Apellido = row.Apellidos,
                                Cedula = cedulaNormalized,
                                Correo = row.Correo,
                                Rol = "Estudiante",
                                UserId = user.Id,
                                User = user
                            };

                            _context.Personas.Add(persona);
                            user.Persona = persona;
                            user.Cedula = cedulaNormalized;
                            user.Email = row.Correo;
                            user.Correo = row.Correo;
                            await _context.SaveChangesAsync();

                            estudiante = user;

                            // Despacho de Correos SMTP en Carga Masiva
                            try
                            {
                                await _emailService.SendCredentialsAsync(
                                    row.Correo,
                                    $"{row.Nombres} {row.Apellidos}".Trim(),
                                    initialUsername,
                                    tempPassword,
                                    "Estudiante"
                                );
                                emailSent = true;
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error al enviar correo a {Correo}", row.Correo);
                            }
                        }
                        else
                        {
                            username = estudiante.Username;
                            initialUsername = username;

                            if (estudiante.Persona == null)
                            {
                                var persona = new Persona
                                {
                                    Nombre = row.Nombres,
                                    Apellido = row.Apellidos,
                                    Cedula = cedulaNormalized,
                                    Correo = row.Correo,
                                    Rol = "Estudiante",
                                    UserId = estudiante.Id,
                                    User = estudiante
                                };
                                estudiante.Persona = persona;
                                estudiante.Cedula = cedulaNormalized;
                                estudiante.Email = row.Correo;
                                estudiante.Correo = row.Correo;
                                _context.Personas.Add(persona);
                                await _context.SaveChangesAsync();
                            }
                            else
                            {
                                bool updated = false;
                                if (string.IsNullOrWhiteSpace(estudiante.Persona.Cedula) && !string.IsNullOrWhiteSpace(cedulaNormalized))
                                {
                                    estudiante.Persona.Cedula = cedulaNormalized;
                                    estudiante.Cedula = cedulaNormalized;
                                    updated = true;
                                }
                                if (string.IsNullOrWhiteSpace(estudiante.Persona.Correo) && !string.IsNullOrWhiteSpace(row.Correo))
                                {
                                    estudiante.Persona.Correo = row.Correo;
                                    estudiante.Email = row.Correo;
                                    estudiante.Correo = row.Correo;
                                    updated = true;
                                }
                                if (updated)
                                {
                                    await _context.SaveChangesAsync();
                                }
                            }
                        }

                        // Vincular Estudiantes a la Clase
                        if (targetClaseId.HasValue && targetClaseId.Value > 0)
                        {
                            var clase = targetClase;
                            if (!await _context.Inscripciones.AnyAsync(i => i.EstudianteId == estudiante.Id && i.ClaseId == (int)targetClaseId.Value))
                            {
                                int? catedraVal = null;
                                if (clase != null && clase.CatedraId.HasValue && await _context.Catedras.AnyAsync(c => c.Id == clase.CatedraId.Value))
                                {
                                    catedraVal = clase.CatedraId.Value;
                                }
                                else
                                {
                                    catedraVal = await _context.Catedras.Select(c => (int?)c.Id).FirstOrDefaultAsync();
                                }

                                var inscripcion = new Inscripcion
                                {
                                    EstudianteId = estudiante.Id,
                                    ClaseId = (int)targetClaseId.Value,
                                    CatedraId = catedraVal,
                                    PromedioActual = 0,
                                    AlertaRendimiento = false
                                };
                                _context.Inscripciones.Add(inscripcion);
                            }

                            if (targetClase != null && !targetClase.Estudiantes.Any(e => e.Id == estudiante.Id))
                            {
                                targetClase.Estudiantes.Add(estudiante);
                            }

                            await _context.SaveChangesAsync();
                        }

                        entry.Success = true;
                        entry.Username = initialUsername;
                        job.Entries.Add(entry);
                        _context.ImportJobEntries.Add(entry);
                        await _context.SaveChangesAsync();

                        result.CreatedCount++;
                        result.CreatedUsernames.Add(username + (isNew ? (emailSent ? " (email sent)" : " (no email)") : " (existente)"));
                    }
                    catch (Exception exRow)
                    {
                        entry.Success = false;
                        entry.ErrorMessage = exRow.Message;
                        job.Entries.Add(entry);
                        _context.ImportJobEntries.Add(entry);
                        await _context.SaveChangesAsync();
                    }
                }

                // finalize importJob counts and write CSV results
                job.CreatedCount = job.Entries.Count(e => e.Success);
                job.ErrorCount = job.Entries.Count(e => !e.Success);

                var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "import-results");
                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                var resultFileName = $"import_{job.Id}.csv";
                var resultPath = Path.Combine(outputDir, resultFileName);

                using (var sw = new StreamWriter(resultPath, false, Encoding.UTF8))
                {
                    // headers
                    sw.WriteLine("nombres,apellidos,cedula,correo,username,success,error");
                    foreach (var e in job.Entries)
                    {
                        var line = string.Format("\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",{5},\"{6}\"",
                            e.Nombres?.Replace("\"", "\"\"") ?? string.Empty,
                            e.Apellidos?.Replace("\"", "\"\"") ?? string.Empty,
                            e.Cedula?.Replace("\"", "\"\"") ?? string.Empty,
                            e.Correo?.Replace("\"", "\"\"") ?? string.Empty,
                            e.Username ?? string.Empty,
                            e.Success ? "1" : "0",
                            e.ErrorMessage?.Replace("\"", "\"\"") ?? string.Empty);
                        sw.WriteLine(line);
                    }
                }

                job.ResultFileName = Path.Combine("import-results", resultFileName);
                await _context.SaveChangesAsync();

                result.CreatedCount = job.CreatedCount;
                result.Errors.AddRange(job.Entries.Where(e => !e.Success).Select(e => $"{e.Nombres} {e.Apellidos}: {e.ErrorMessage}"));

                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error procesando el archivo: {ex.Message}");
            }
        }

        private static string GenerateUsername(string nombres, string apellidos)
        {
            // Nombres: take first token's first char
            var nombreParts = nombres.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var apellidoParts = apellidos.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstInitial = nombreParts.Length > 0 && !string.IsNullOrEmpty(nombreParts[0]) ? nombreParts[0][0].ToString() : "x";
            var firstApellido = apellidoParts.Length > 0 ? apellidoParts[0] : "apellido";
            var secondApellidoInitial = apellidoParts.Length > 1 ? apellidoParts[1][0].ToString() : string.Empty;

            var raw = (firstInitial + firstApellido + secondApellidoInitial).ToLowerInvariant();
            // Remove spaces and non-alphanumerics
            var cleaned = new string(raw.Where(c => char.IsLetterOrDigit(c)).ToArray());
            return cleaned;
        }

        private async Task<string> MakeUniqueUsernameAsync(string baseUsername)
        {
            var username = baseUsername;
            var i = 1;
            while (await _context.Users.AnyAsync(u => u.Username == username))
            {
                i++;
                username = baseUsername + i.ToString();
            }

            return username;
        }
    }
}
