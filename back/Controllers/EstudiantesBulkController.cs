using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Mail;
using System.Security.Claims;
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

        // GET /api/estudiantes/template
        [HttpGet("template")]
        public IActionResult DownloadTemplate()
        {
            var csv =
                "nombres,apellidos,cedula,correo,username\n" +
                "Jordy Fabian,Rivas Bodero,1207751023,jordy@uteq.edu.ec,\n";

            var bytes = Encoding.UTF8.GetBytes(csv);
            return File(bytes, "text/csv", "estudiantes_plantilla.csv");
        }

        // Endpoint para listar presentaciones convocadas.
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
                    Jurados = p.Jurados
                        .Select(j => j.Persona.Nombre + " " + j.Persona.Apellido)
                        .ToList()
                })
                 .ToListAsync();

            var result = presentaciones.Select(p =>
            {
                var juradoNombres = p.Jurados.Select(j => j.Persona != null ? $"{j.Persona.Nombre} {j.Persona.Apellido}".Trim() : j.Username).ToList();
                var estadoActual = p.Ayudantia != null && !string.IsNullOrEmpty(p.Ayudantia.Estado) ? p.Ayudantia.Estado : "Convocada";

                return new
                {
                    p.Id,
                    id = p.Id,
                    presentacionId = p.Id,
                    p.AyudantiaId,
                    ayudantiaId = p.AyudantiaId,
                    p.Fecha,
                    fecha = p.Fecha,
                    fechaPresentacion = p.Fecha,
                    Catedra = p.Ayudantia?.Catedra?.Nombre ?? "Cátedra General",
                    catedra = p.Ayudantia?.Catedra?.Nombre ?? "Cátedra General",
                    catedraNombre = p.Ayudantia?.Catedra?.Nombre ?? "Cátedra General",
                    Postulante = p.Ayudantia?.Estudiante?.Persona != null ? $"{p.Ayudantia.Estudiante.Persona.Nombre} {p.Ayudantia.Estudiante.Persona.Apellido}".Trim() : (p.Ayudantia?.Estudiante?.Username ?? "Estudiante"),
                    estudianteNombre = p.Ayudantia?.Estudiante?.Persona != null ? $"{p.Ayudantia.Estudiante.Persona.Nombre} {p.Ayudantia.Estudiante.Persona.Apellido}".Trim() : (p.Ayudantia?.Estudiante?.Username ?? "Estudiante"),
                    Jurados = juradoNombres,
                    jurados = juradoNombres,
                    profesoresAsignados = juradoNombres,
                    estado = estadoActual,
                    reunionPlanificada = true,
                    estadoTribunal = "Tribunal Convocado - Reunión Planificada",
                    mensajeTribunal = $"Tribunal convocado y reunión planificada para el {p.Fecha:dd/MM/yyyy HH:mm}. Jurados: {string.Join(", ", juradoNombres)}"
                };
            });

            return Ok(result);
        }

        // GET /api/estudiantes/imports/{jobId}/result
        [HttpGet("imports/{jobId}/result")]
        public async Task<IActionResult> DownloadImportResult(int jobId)
        {
            var job = await _context.ImportJobs
                .Include(j => j.Entries)
                .FirstOrDefaultAsync(j => j.Id == jobId);

            if (job == null)
                return NotFound(new { message = "Importación no encontrada." });

            var callerId = GetAuthenticatedUserId();
            if (!callerId.HasValue)
                return Unauthorized(new { message = "Usuario no autenticado." });

            var esAdministrador = await HasAnyRoleAsync(
                callerId.Value,
                "Administrador",
                "Coordinador",
                "Decano");

            if (!esAdministrador && job.CreatedByUserId != callerId.Value)
            {
                  return StatusCode(403, new { message = "No autorizado para descargar este resultado." });
                 return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "No autorizado para descargar este resultado." });
             }

            if (string.IsNullOrWhiteSpace(job.ResultFileName))
                return NotFound(new { message = "No hay archivo de resultado disponible." });

            var path = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                job.ResultFileName.Replace('\\', Path.DirectorySeparatorChar));

            if (!System.IO.File.Exists(path))
                return NotFound(new { message = "Archivo de resultado no encontrado en el servidor." });

            var bytes = await System.IO.File.ReadAllBytesAsync(path);
            return File(bytes, "text/csv", Path.GetFileName(path));
        }

         // POST /api/estudiantes/bulk-upload (multipart/form-data file)
         // POST /api/estudiantes/bulk-upload?claseId=39
        // multipart/form-data: file
         [HttpPost("bulk-upload")]
        public async Task<ActionResult<BulkUploadResultDto>> BulkUpload(
            IFormFile file,
            [FromQuery] long? claseId = null)
        {
            var callerId = GetAuthenticatedUserId();
            if (!callerId.HasValue)
                return Unauthorized(new { message = "Usuario no autenticado." });

            var esAdministrador = await HasAnyRoleAsync(
                callerId.Value,
                "Administrador",
                "Coordinador",
                "Decano");

            var esDocente = await HasAnyRoleAsync(
                callerId.Value,
                "Docente",
                "Profesor");

            if (!esAdministrador && !esDocente)
            {
                 return StatusCode(403, new { message = "No autorizado para subir estudiantes en bloque." });
                 return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "No autorizado para incorporar estudiantes en bloque." });
             }

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "Archivo requerido." });

            long? targetClaseId = claseId;
            if (!targetClaseId.HasValue &&
                Request.HasFormContentType &&
                Request.Form.TryGetValue("claseId", out var formClaseIdVal) &&
                long.TryParse(formClaseIdVal, out var parsedClaseId))
            {
                targetClaseId = parsedClaseId;
            }

            if (!targetClaseId.HasValue ||
                targetClaseId.Value <= 0 ||
                targetClaseId.Value > int.MaxValue)
            {
                return BadRequest(new
                {
                    message = "Debe seleccionar una clase válida para realizar la importación."
                });
            }

            var targetClase = await _context.Clases
                .Include(c => c.Estudiantes)
                .Include(c => c.Materia)
                .FirstOrDefaultAsync(c => c.Id == (int)targetClaseId.Value);

            if (targetClase == null)
                return NotFound(new { message = "Clase no encontrada." });

            if (!esAdministrador && targetClase.DocenteId != callerId.Value)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "El docente autenticado no es responsable de la clase seleccionada." });
            }

            if (!targetClase.CatedraId.HasValue || targetClase.CatedraId.Value <= 0)
            {
                return BadRequest(new
                {
                    message = "La clase no tiene una cátedra válida asociada. Corrija la asociación antes de importar estudiantes."
                });
            }

            var catedra = await _context.Catedras
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == targetClase.CatedraId.Value);

            if (catedra == null)
            {
                return BadRequest(new
                {
                    message = "La cátedra asociada a la clase no existe."
                });
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension != ".csv" && extension != ".xlsx")
            {
                return BadRequest(new
                {
                    message = "Formato no permitido. Solo se aceptan archivos CSV o XLSX."
                });
            }

            var result = new BulkUploadResultDto();

            try
            {
                var rows = new List<(string Nombres, string Apellidos, string Cedula, string Correo, string? Username)>();
                string? estructuraError = null;

                using (var stream = file.OpenReadStream())
                {
                    if (extension == ".csv")
                    {
                        using var reader = new StreamReader(stream, Encoding.UTF8);
                        var header = await reader.ReadLineAsync();

                        if (!HeaderCsvValido(header))
                        {
                            estructuraError =
                                "Estructura CSV inválida. Las primeras columnas deben ser: nombres, apellidos, cedula, correo. Username es opcional.";
                        }
                        else
                        {
                            string? line;
                            var numeroLinea = 1;
                            while ((line = await reader.ReadLineAsync()) != null)
                            {
                                numeroLinea++;
                                if (string.IsNullOrWhiteSpace(line))
                                    continue;

                                var parts = line.Split(',');
                                if (parts.Length < 4)
                                {
                                    result.Errors.Add($"Línea {numeroLinea}: estructura inválida.");
                                    continue;
                                }

                                var username = parts.Length > 4
                                    ? parts[4].Trim()
                                    : null;

                                rows.Add((
                                    parts[0].Trim(),
                                    parts[1].Trim(),
                                    parts[2].Trim(),
                                    parts[3].Trim(),
                                    string.IsNullOrWhiteSpace(username) ? null : username));
                            }
                        }
                    }
                    else
                    {
                        using var workbook = new XLWorkbook(stream);
                        var ws = workbook.Worksheets.FirstOrDefault();

                         for (int r = firstRowUsed + 1; r <= lastRow; r++)
                         if (ws == null || ws.FirstRowUsed() == null)
                         {
                            estructuraError = "El archivo XLSX está vacío.";
                        }
                        else
                        {
                            var firstRow = ws.FirstRowUsed()!.RowNumber();

                            if (!HeaderExcelValido(ws, firstRow))
                            {
                                estructuraError =
                                    "Estructura XLSX inválida. Las primeras columnas deben ser: nombres, apellidos, cedula, correo. Username es opcional.";
                            }
                            else
                            {
                                var lastRowUsed = ws.LastRowUsed();
                                var lastRow = lastRowUsed?.RowNumber() ?? firstRow;

                                for (var r = firstRow + 1; r <= lastRow; r++)
                                {
                                    var nombres = ws.Cell(r, 1).GetString().Trim();
                                    var apellidos = ws.Cell(r, 2).GetString().Trim();
                                    var cedula = ws.Cell(r, 3).GetString().Trim();
                                    var correo = ws.Cell(r, 4).GetString().Trim();
                                    var username = ws.Cell(r, 5).GetString().Trim();

                                    if (string.IsNullOrWhiteSpace(nombres) &&
                                        string.IsNullOrWhiteSpace(apellidos) &&
                                        string.IsNullOrWhiteSpace(cedula) &&
                                        string.IsNullOrWhiteSpace(correo))
                                    {
                                        continue;
                                    }

                                    rows.Add((
                                        nombres,
                                        apellidos,
                                        cedula,
                                        correo,
                                        string.IsNullOrWhiteSpace(username) ? null : username));
                                }
                            }
                        }
                    }
                }


                var maxRows = int.TryParse(_config["BulkUpload:MaxRows"], out var m) ? m : 500;

                if (!string.IsNullOrWhiteSpace(estructuraError))
                    return BadRequest(new { message = estructuraError });

                var maxRows = int.TryParse(_config["BulkUpload:MaxRows"], out var configuredMax)
                    ? configuredMax
                    : 500;

                if (rows.Count == 0)
                    return BadRequest(new { message = "El archivo no contiene estudiantes para importar." });

                 if (rows.Count > maxRows)
                {
                    return BadRequest(new
                    {
                        message = $"El archivo contiene {rows.Count} filas, el máximo permitido es {maxRows}."
                    });
                }


                var allowedDomainsConfig = _config["BulkUpload:AllowedEmailDomains"] ?? string.Empty;
                var allowedDomains = allowedDomainsConfig.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(d => d.Trim().ToLowerInvariant()).ToArray();

                var callerIdStr = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                int.TryParse(callerIdStr, out var callerUserId);

                var job = new Entities.ImportJob

                var allowedDomainsConfig = _config["BulkUpload:AllowedEmailDomains"];
                var allowedDomains = string.IsNullOrWhiteSpace(allowedDomainsConfig)
                    ? new[] { "uteq.edu.ec" }
                    : allowedDomainsConfig
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(d => d.Trim().ToLowerInvariant())
                        .Where(d => !string.IsNullOrWhiteSpace(d))
                        .Distinct()
                        .ToArray();

                var job = new ImportJob
                 {
                    CreatedAt = DateTime.UtcNow,
                    CreatedByUserId = callerId.Value,
                    FileName = file.FileName,
                    ResultFileName = string.Empty,
                    CreatedCount = 0,
                    ErrorCount = 0
                };

                _context.ImportJobs.Add(job);
                await _context.SaveChangesAsync();

                result.ImportJobId = job.Id;

                var vistosCedula = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var vistosCorreo = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                foreach (var row in rows)
                {
                    var correoNormalizado = row.Correo.Trim().ToLowerInvariant();
                    var cedulaNormalizada = new string(row.Cedula.Where(char.IsDigit).ToArray());

                    var initialUsername = !string.IsNullOrWhiteSpace(row.Username)
                        ? row.Username!.Trim().ToLowerInvariant()
                        : (correoNormalizado.Contains('@')
                            ? correoNormalizado.Split('@')[0]
                            : string.Empty);

                    var entry = new ImportJobEntry
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

                        var cedulaNormalized = new string(row.Cedula.Where(char.IsDigit).ToArray());
                        if (string.IsNullOrWhiteSpace(cedulaNormalized) || cedulaNormalized.Length < 6)

                        var validationError = ValidarFila(
                            row.Nombres,
                            row.Apellidos,
                            cedulaNormalizada,
                            correoNormalizado,
                            allowedDomains);

                        if (!string.IsNullOrWhiteSpace(validationError))
                         {
                            await RegistrarErrorAsync(job, entry, validationError);
                            continue;
                        }

                         if (string.IsNullOrWhiteSpace(row.Correo))
                         if (!vistosCedula.Add(cedulaNormalizada) ||
                            !vistosCorreo.Add(correoNormalizado))
                         {
                            await RegistrarErrorAsync(
                                job,
                                entry,
                                "Registro duplicado dentro del archivo.");
                            continue;
                        }

                         try
                         User? estudiante = null;

                        var existingPersona = await _context.Personas
                            .Include(p => p.User)
                            .FirstOrDefaultAsync(p =>
                                p.Cedula == cedulaNormalizada ||
                                p.Correo.ToLower() == correoNormalizado);

                        if (existingPersona?.User != null)
                         {
                            estudiante = existingPersona.User;
                            estudiante.Persona = existingPersona;
                        }
                        else
                        {
                            estudiante = await _context.Users
                                .Include(u => u.Persona)
                                .FirstOrDefaultAsync(u =>
                                    u.Username.ToLower() == correoNormalizado ||
                                    (u.Persona != null &&
                                     u.Persona.Correo.ToLower() == correoNormalizado));
                        }

                        if (estudiante?.Persona != null)
                        {
                            var roles = estudiante.Persona.GetRoles();
                            if (!roles.Any(r =>
                                    r.Equals("Estudiante", StringComparison.OrdinalIgnoreCase)))
                            {
                                await RegistrarErrorAsync(
                                    job,
                                    entry,
                                    "El correo o cédula corresponde a un usuario que no tiene rol de Estudiante.");
                                continue;
                            }
                        }

                         User? estudiante = null;
                        var existingPersona = await _context.Personas
                            .Include(p => p.User)
                            .FirstOrDefaultAsync(p => p.Cedula == cedulaNormalized || p.Correo.ToLower() == row.Correo.ToLower());
                         if (estudiante != null)
                        {
                            var yaInscrito = await _context.Inscripciones.AnyAsync(i =>
                                    i.EstudianteId == estudiante.Id &&
                                    i.ClaseId == targetClase.Id)
                                || targetClase.Estudiantes.Any(e => e.Id == estudiante.Id);

                            if (yaInscrito)
                            {
                                entry.Username = estudiante.Username;
                                await RegistrarErrorAsync(
                                    job,
                                    entry,
                                    "Estudiante duplicado: ya está inscrito en la clase seleccionada.");
                                continue;
                            }
                        }

                        var estudianteEsNuevo = false;
                        string? tempPassword = null;

                        if (estudiante == null)
                        {
                            estudianteEsNuevo = true;

                             var tempPassword = $"Uteq.{RandomNumberGenerator.GetInt32(100000, 999999)}!";
                             var usernameBase = !string.IsNullOrWhiteSpace(row.Username)
                                ? row.Username!.Trim().ToLowerInvariant()
                                : correoNormalizado.Split('@')[0].ToLowerInvariant();

                            if (string.IsNullOrWhiteSpace(usernameBase))
                                usernameBase = GenerateUsername(row.Nombres, row.Apellidos);

                            var username = await MakeUniqueUsernameAsync(usernameBase);
                            tempPassword = $"Uteq.{RandomNumberGenerator.GetInt32(100000, 999999)}!";

                            using var hmac = new HMACSHA512();

                            estudiante = new User
                            {
                                Username = username,
                                PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(tempPassword)),
                                PasswordSalt = hmac.Key,
                                Cedula = cedulaNormalizada,
                                Email = correoNormalizado,
                                Correo = correoNormalizado
                            };

                             _context.Users.Add(user);
                             _context.Users.Add(estudiante);
                             await _context.SaveChangesAsync();

                            var persona = new Persona
                            {
                                Nombre = row.Nombres.Trim(),
                                Apellido = row.Apellidos.Trim(),
                                Cedula = cedulaNormalizada,
                                Correo = correoNormalizado,
                                Rol = "Estudiante",
                                UserId = estudiante.Id,
                                User = estudiante
                            };

                            _context.Personas.Add(persona);
                            estudiante.Persona = persona;
                            await _context.SaveChangesAsync();

                             estudiante = user;

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
                             entry.Username = estudiante.Username;
                         }
                        else
                        {
                            entry.Username = estudiante.Username;

                            if (estudiante.Persona == null)
                            {
                                var persona = new Persona
                                {
                                    Nombre = row.Nombres.Trim(),
                                    Apellido = row.Apellidos.Trim(),
                                    Cedula = cedulaNormalizada,
                                    Correo = correoNormalizado,
                                    Rol = "Estudiante",
                                    UserId = estudiante.Id,
                                    User = estudiante
                                };

                                _context.Personas.Add(persona);
                                estudiante.Persona = persona;
                                estudiante.Cedula = cedulaNormalizada;
                                estudiante.Email = correoNormalizado;
                                estudiante.Correo = correoNormalizado;
                                await _context.SaveChangesAsync();
                            }
                        }


                        if (targetClaseId.HasValue && targetClaseId.Value > 0)
                         var inscripcion = new Inscripcion
                         {
                            EstudianteId = estudiante.Id,
                            ClaseId = targetClase.Id,
                            CatedraId = targetClase.CatedraId.Value,
                            PromedioActual = 0,
                            AlertaRendimiento = false
                        };

                        _context.Inscripciones.Add(inscripcion);

                        if (!targetClase.Estudiantes.Any(e => e.Id == estudiante.Id))
                            targetClase.Estudiantes.Add(estudiante);

                        await _context.SaveChangesAsync();

                        if (estudianteEsNuevo && !string.IsNullOrWhiteSpace(tempPassword))
                        {
                            try
                            {
                                await _emailService.SendCredentialsAsync(
                                    correoNormalizado,
                                    $"{row.Nombres} {row.Apellidos}".Trim(),
                                    estudiante.Username,
                                    tempPassword,
                                    "Estudiante");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogWarning(
                                    ex,
                                    "No se pudo enviar el correo de credenciales a {Correo}",
                                    correoNormalizado);
                            }
                        }

                        try
                        {
                            var subject = $"Inscripción a clase: {targetClase.Nombre}";
                            var body =
                                $"<p>Estimado/a <strong>{row.Nombres} {row.Apellidos}</strong>,</p>" +
                                $"<p>Has sido incorporado/a a la clase <strong>{targetClase.Nombre}</strong>.</p>" +
                                $"<p>Materia: <strong>{targetClase.Materia?.Nombre ?? catedra.Nombre}</strong></p>" +
                                $"<p>Periodo académico: <strong>{catedra.Semestre}</strong></p>";

                            await _emailService.SendEmailAsync(
                                correoNormalizado,
                                subject,
                                body);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(
                                ex,
                                "No se pudo enviar la notificación de incorporación a {Correo}",
                                correoNormalizado);
                        }

                        entry.Success = true;
                        entry.ErrorMessage = string.Empty;
                        job.Entries.Add(entry);
                        _context.ImportJobEntries.Add(entry);
                        await _context.SaveChangesAsync();

                        result.CreatedUsernames.Add(estudiante.Username);
                    }
                    catch (Exception exRow)
                    {
                        await RegistrarErrorAsync(job, entry, exRow.Message);
                    }
                }

                job.CreatedCount = job.Entries.Count(e => e.Success);
                job.ErrorCount = job.Entries.Count(e => !e.Success);

                var outputDir = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "import-results");

                if (!Directory.Exists(outputDir))
                    Directory.CreateDirectory(outputDir);

                var resultFileName = $"import_{job.Id}.csv";
                var resultPath = Path.Combine(outputDir, resultFileName);

                using (var sw = new StreamWriter(resultPath, false, Encoding.UTF8))
                {
                    sw.WriteLine("nombres,apellidos,cedula,correo,username,success,error");

                    foreach (var e in job.Entries)
                    {
                        var line = string.Format(
                            "\"{0}\",\"{1}\",\"{2}\",\"{3}\",\"{4}\",{5},\"{6}\"",
                            EscapeCsv(e.Nombres),
                            EscapeCsv(e.Apellidos),
                            EscapeCsv(e.Cedula),
                            EscapeCsv(e.Correo),
                            EscapeCsv(e.Username),
                            e.Success ? "1" : "0",
                            EscapeCsv(e.ErrorMessage));

                        sw.WriteLine(line);
                    }
                }

                job.ResultFileName = Path.Combine("import-results", resultFileName);
                await _context.SaveChangesAsync();

                result.CreatedCount = job.CreatedCount;
                result.RejectedCount = job.ErrorCount;
                result.Errors = job.Entries
                    .Where(e => !e.Success)
                    .Select(e => $"{e.Nombres} {e.Apellidos}: {e.ErrorMessage}".Trim())
                    .ToList();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error procesando la carga masiva de estudiantes.");
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new { message = $"Error procesando el archivo: {ex.Message}" });
            }
        }

        private int? GetAuthenticatedUserId()
        {
            var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return int.TryParse(value, out var id) ? id : (int?)null;
        }

        private async Task<bool> HasAnyRoleAsync(int userId, params string[] rolesBuscados)
        {
            if (rolesBuscados.Any(User.IsInRole))
                return true;

            var roleClaim = User.FindFirst(ClaimTypes.Role)?.Value;
            if (!string.IsNullOrWhiteSpace(roleClaim) &&
                rolesBuscados.Any(r =>
                    r.Equals(roleClaim, StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }

            var user = await _context.Users
                .Include(u => u.Persona)
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            var roles = user?.Persona?.GetRoles() ?? new List<string>();

            return roles.Any(rol =>
                rolesBuscados.Any(buscado =>
                    buscado.Equals(rol, StringComparison.OrdinalIgnoreCase)));
        }

        private static bool HeaderCsvValido(string? header)
        {
            if (string.IsNullOrWhiteSpace(header))
                return false;

            var columns = header
                .Split(',')
                .Select(c => NormalizeHeader(c))
                .ToArray();

            return columns.Length >= 4 &&
                   columns[0] == "nombres" &&
                   columns[1] == "apellidos" &&
                   columns[2] == "cedula" &&
                   columns[3] == "correo";
        }

        private static bool HeaderExcelValido(IXLWorksheet ws, int headerRow)
        {
            return NormalizeHeader(ws.Cell(headerRow, 1).GetString()) == "nombres" &&
                   NormalizeHeader(ws.Cell(headerRow, 2).GetString()) == "apellidos" &&
                   NormalizeHeader(ws.Cell(headerRow, 3).GetString()) == "cedula" &&
                   NormalizeHeader(ws.Cell(headerRow, 4).GetString()) == "correo";
        }

        private static string NormalizeHeader(string value)
        {
            return (value ?? string.Empty)
                .Trim()
                .ToLowerInvariant()
                .Replace("é", "e")
                .Replace("á", "a")
                .Replace("í", "i")
                .Replace("ó", "o")
                .Replace("ú", "u");
        }

        private static string? ValidarFila(
            string nombres,
            string apellidos,
            string cedula,
            string correo,
            IReadOnlyCollection<string> allowedDomains)
        {
            if (string.IsNullOrWhiteSpace(nombres))
                return "Nombres obligatorios.";

            if (string.IsNullOrWhiteSpace(apellidos))
                return "Apellidos obligatorios.";

            if (string.IsNullOrWhiteSpace(cedula) || cedula.Length < 6)
                return "Cédula inválida o demasiado corta.";

            if (string.IsNullOrWhiteSpace(correo))
                return "Correo vacío.";

            try
            {
                var mail = new MailAddress(correo);
                var domain = mail.Host.ToLowerInvariant();

                if (allowedDomains.Count > 0 && !allowedDomains.Contains(domain))
                    return $"Dominio de correo '{domain}' no permitido.";
            }
            catch
            {
                return "Formato de correo inválido.";
            }

            return null;
        }

        private async Task RegistrarErrorAsync(
            ImportJob job,
            ImportJobEntry entry,
            string error)
        {
            entry.Success = false;
            entry.ErrorMessage = error;
            job.Entries.Add(entry);
            _context.ImportJobEntries.Add(entry);
            await _context.SaveChangesAsync();
        }

        private static string EscapeCsv(string? value)
        {
            return (value ?? string.Empty).Replace("\"", "\"\"");
        }

        private static string GenerateUsername(string nombres, string apellidos)
        {
             var nombreParts = nombres.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var apellidoParts = apellidos.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var firstInitial = nombreParts.Length > 0 && !string.IsNullOrEmpty(nombreParts[0]) ? nombreParts[0][0].ToString() : "x";
            var firstApellido = apellidoParts.Length > 0 ? apellidoParts[0] : "apellido";
            var secondApellidoInitial = apellidoParts.Length > 1 ? apellidoParts[1][0].ToString() : string.Empty;

            var raw = (firstInitial + firstApellido + secondApellidoInitial).ToLowerInvariant();
            var cleaned = new string(raw.Where(c => char.IsLetterOrDigit(c)).ToArray());
            return cleaned;

            var nombreParts = nombres.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            var apellidoParts = apellidos.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries);

            var firstInitial =
                nombreParts.Length > 0 && !string.IsNullOrEmpty(nombreParts[0])
                    ? nombreParts[0][0].ToString()
                    : "x";

            var firstApellido =
                apellidoParts.Length > 0
                    ? apellidoParts[0]
                    : "apellido";

            var secondApellidoInitial =
                apellidoParts.Length > 1
                    ? apellidoParts[1][0].ToString()
                    : string.Empty;

            var raw =
                (firstInitial + firstApellido + secondApellidoInitial)
                .ToLowerInvariant();

            return new string(raw.Where(char.IsLetterOrDigit).ToArray());

        }

        private async Task<string> MakeUniqueUsernameAsync(string baseUsername)
        {
            var username = baseUsername;
            var i = 1;

            while (await _context.Users.AnyAsync(u => u.Username == username))
            {
                i++;
                username = baseUsername + i;
            }

            return username;
        }
    }
}
