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
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace back.Controllers
{
    [ApiController]
    [Route("api/estudiantes")]
    [Authorize]
    public class EstudiantesBulkController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public EstudiantesBulkController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
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
        public async Task<ActionResult<BulkUploadResultDto>> BulkUpload(IFormFile file)
        {
            var callerRole = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;
            var allowed = new[] { "Administrador", "Decano", "Coordinador", "Docente" };
            if (!allowed.Any(r => string.Equals(r, callerRole, StringComparison.OrdinalIgnoreCase)))
                return Forbid("No autorizado para subir estudiantes en bloque.");

            if (file == null || file.Length == 0) return BadRequest("Archivo requerido.");

            var result = new BulkUploadResultDto();

            try
            {
                List<(string Nombres, string Apellidos, string Cedula, string Correo)> rows = new();

                using (var stream = file.OpenReadStream())
                {
                    if (file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    {
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        var header = await reader.ReadLineAsync();
                        while (!reader.EndOfStream)
                        {
                            var line = await reader.ReadLineAsync();
                            if (string.IsNullOrWhiteSpace(line)) continue;
                            var parts = line.Split(',');
                            if (parts.Length < 4) { result.Errors.Add($"Línea inválida: {line}"); continue; }
                            rows.Add((parts[0].Trim(), parts[1].Trim(), parts[2].Trim(), parts[3].Trim()));
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

                            if (string.IsNullOrWhiteSpace(nombres) || string.IsNullOrWhiteSpace(apellidos) || string.IsNullOrWhiteSpace(cedula))
                            {
                                result.Errors.Add($"Fila {r}: faltan datos obligatorios");
                                continue;
                            }

                            rows.Add((nombres.Trim(), apellidos.Trim(), cedula.Trim(), correo?.Trim() ?? string.Empty));
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

                var importJob = new Entities.ImportJob
                {
                    CreatedByUserId = callerUserId,
                    CreatedAt = DateTime.UtcNow,
                    FileName = file.FileName
                };

                _context.ImportJobs.Add(importJob);
                await _context.SaveChangesAsync(); // get job id


                foreach (var row in rows)
                {
                    var entry = new Entities.ImportJobEntry
                    {
                        ImportJobId = importJob.Id,
                        Nombres = row.Nombres,
                        Apellidos = row.Apellidos,
                        Cedula = row.Cedula,
                        Correo = row.Correo
                    };

                    try
                    {
                        // Normalize cedula: keep digits only
                        var cedulaNormalized = new string(row.Cedula.Where(char.IsDigit).ToArray());
                        if (string.IsNullOrWhiteSpace(cedulaNormalized) || cedulaNormalized.Length < 6)
                        {
                            entry.Success = false;
                            entry.ErrorMessage = "Cédula inválida o demasiado corta.";
                            importJob.Entries.Add(entry);
                            _context.ImportJobEntries.Add(entry);
                            await _context.SaveChangesAsync();
                            continue;
                        }

                        // Validate correo present
                        if (string.IsNullOrWhiteSpace(row.Correo))
                        {
                            entry.Success = false;
                            entry.ErrorMessage = "Correo vacío.";
                            importJob.Entries.Add(entry);
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
                                entry.ErrorMessage = $"Dominio de correo no permitido ({domain}).";
                                importJob.Entries.Add(entry);
                                _context.ImportJobEntries.Add(entry);
                                await _context.SaveChangesAsync();
                                continue;
                            }
                        }
                        catch
                        {
                            entry.Success = false;
                            entry.ErrorMessage = $"Correo inválido ({row.Correo}).";
                            importJob.Entries.Add(entry);
                            _context.ImportJobEntries.Add(entry);
                            await _context.SaveChangesAsync();
                            continue;
                        }

                        var usernameBase = GenerateUsername(row.Nombres, row.Apellidos);
                        var username = await MakeUniqueUsernameAsync(usernameBase);

                        // Password temporary = cedula normalized
                        var tempPassword = cedulaNormalized;

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
                            Correo = row.Correo,
                            Rol = "Estudiante",
                            UserId = user.Id
                        };

                        _context.Personas.Add(persona);
                        await _context.SaveChangesAsync();

                        // Send email if SMTP configured
                        var emailSent = false;
                        try
                        {
                            var smtpHost = _config["Smtp:Host"];
                            if (!string.IsNullOrWhiteSpace(smtpHost))
                            {
                                var smtpPort = int.TryParse(_config["Smtp:Port"], out var p) ? p : 25;
                                var smtpUser = _config["Smtp:User"];
                                var smtpPass = _config["Smtp:Pass"];
                                var from = _config["Smtp:From"] ?? "no-reply@example.com";

                                using var client = new SmtpClient(smtpHost, smtpPort)
                                {
                                    EnableSsl = bool.TryParse(_config["Smtp:EnableSsl"], out var s) && s
                                };

                                if (!string.IsNullOrEmpty(smtpUser)) client.Credentials = new System.Net.NetworkCredential(smtpUser, smtpPass);

                                var mail = new MailMessage(from, persona.Correo)
                                {
                                    Subject = "Cuenta creada - Credenciales",
                                    Body = $"Hola {persona.Nombre},\n\nSe ha creado tu cuenta.\nUsuario: {username}\nContraseña temporal: {tempPassword}\nPor favor cambia la contraseña en tu primer inicio de sesión.\n\nSaludos.",
                                    IsBodyHtml = false
                                };

                                await client.SendMailAsync(mail);
                                emailSent = true;
                            }
                        }
                        catch (Exception ex)
                        {
                            // don't fail entire process if email fails
                            entry.ErrorMessage = $"Fallo al enviar email: {ex.Message}";
                        }

                        entry.Success = true;
                        entry.Username = username;
                        importJob.Entries.Add(entry);
                        _context.ImportJobEntries.Add(entry);
                        await _context.SaveChangesAsync();

                        result.CreatedCount++;
                        result.CreatedUsernames.Add(username + (emailSent ? " (email sent)" : " (no email)"));
                    }
                    catch (Exception exRow)
                    {
                        entry.Success = false;
                        entry.ErrorMessage = exRow.Message;
                        importJob.Entries.Add(entry);
                        _context.ImportJobEntries.Add(entry);
                        await _context.SaveChangesAsync();
                    }
                }

                // finalize importJob counts and write CSV results
                importJob.CreatedCount = importJob.Entries.Count(e => e.Success);
                importJob.ErrorCount = importJob.Entries.Count(e => !e.Success);

                var outputDir = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "import-results");
                if (!Directory.Exists(outputDir)) Directory.CreateDirectory(outputDir);

                var resultFileName = $"import_{importJob.Id}.csv";
                var resultPath = Path.Combine(outputDir, resultFileName);

                using (var sw = new StreamWriter(resultPath, false, Encoding.UTF8))
                {
                    // headers
                    sw.WriteLine("nombres,apellidos,cedula,correo,username,success,error");
                    foreach (var e in importJob.Entries)
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

                importJob.ResultFileName = Path.Combine("import-results", resultFileName);
                await _context.SaveChangesAsync();

                result.CreatedCount = importJob.CreatedCount;
                result.Errors.AddRange(importJob.Entries.Where(e => !e.Success).Select(e => $"{e.Nombres} {e.Apellidos}: {e.ErrorMessage}"));

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