using back.Data;
using back.DTOs;
using back.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text.Json;
using System.Threading.Tasks;

namespace back.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    [Route("api/materias")]
    public class MateriaController : ControllerBase
    {
        private readonly AppDbContext _context;

        private readonly IConfiguration _config;

        public MateriaController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // Propiedad para obtener de forma segura el ID del usuario autenticado
        private int? UserId
        {
            get
            {
                var value = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                return int.TryParse(value, out var id) ? id : (int?)null;
            }
        }

        private async Task<User> GetDefaultDocenteAsync(long? requestedDocenteId = null)
        {
            if (requestedDocenteId.HasValue && requestedDocenteId.Value > 0 && requestedDocenteId.Value <= int.MaxValue)
            {
                var doc = await _context.Users
                    .Include(u => u.Persona)
                    .FirstOrDefaultAsync(u => u.Id == (int)requestedDocenteId.Value);
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

        private CreateMateriaDto ParseMateriaDto(JsonElement element)
        {
            var dto = new CreateMateriaDto();
            if (element.ValueKind != JsonValueKind.Object) return dto;

            foreach (var prop in element.EnumerateObject())
            {
                var name = prop.Name.ToLowerInvariant();
                if (name == "nombre") dto.Nombre = prop.Value.GetString();
                else if (name == "codigo") dto.Codigo = prop.Value.GetString();
                else if (name == "descripcion") dto.Descripcion = prop.Value.GetString();
                else if (name == "semestre") dto.Semestre = prop.Value.GetString();
                else if (name == "claseid")
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt32(out var cId))
                        dto.ClaseId = cId;
                    else if (prop.Value.ValueKind == JsonValueKind.String && int.TryParse(prop.Value.GetString(), out var sClaseId))
                        dto.ClaseId = sClaseId;
                }
                else if (name == "docenteid" || name == "docenteresponsableid")
                {
                    if (prop.Value.ValueKind == JsonValueKind.Number && prop.Value.TryGetInt64(out var dId))
                        dto.DocenteId = dId;
                    else if (prop.Value.ValueKind == JsonValueKind.String && long.TryParse(prop.Value.GetString(), out var sId))
                        dto.DocenteId = sId;
                }
            }
            return dto;
        }

        // GET /api/Materia: Retorna todas las materias con su docente asignado y lista de clases asociadas (HTTP 200)
        [HttpGet]
        public async Task<IActionResult> GetAllMaterias()
        {
            var materias = await _context.Materias
                .Include(m => m.DocenteResponsable)
                    .ThenInclude(d => d.Persona)
                .Include(m => m.Clase)
                .Include(m => m.Clases)
                .ToListAsync();

            var catedras = await _context.Catedras
                .Include(c => c.Docente)
                    .ThenInclude(d => d.Persona)
                .ToListAsync();

            bool huboSync = false;
            foreach (var cat in catedras)
            {
                if (!materias.Any(m => m.Nombre.Equals(cat.Nombre, StringComparison.OrdinalIgnoreCase)))
                {
                    var nuevaMateria = new Materia
                    {
                        Nombre = cat.Nombre,
                        Descripcion = "Cátedra Universitaria",
                        Codigo = $"CAT-{cat.Id}",
                        DocenteResponsableId = cat.DocenteId
                    };
                    _context.Materias.Add(nuevaMateria);
                    huboSync = true;
                    materias.Add(nuevaMateria);
                }
            }

            if (huboSync)
            {
                await _context.SaveChangesAsync();
            }

            var result = materias.Select(m =>
            {
                var catedraRelacionada = catedras.FirstOrDefault(c =>
                    string.Equals(c.Nombre?.Trim(), m.Nombre?.Trim(), StringComparison.OrdinalIgnoreCase));

                return new
                {
                    id = m.Id,
                    catedraId = catedraRelacionada?.Id,
                    nombre = m.Nombre,
                    codigo = m.Codigo,
                    descripcion = m.Descripcion,
                    claseId = m.ClaseId,
                    claseNombre = m.Clase != null ? m.Clase.Nombre : null,
                    docenteId = m.DocenteResponsableId,
                    docenteResponsableId = m.DocenteResponsableId,
                    nombreDocenteResponsable = m.DocenteResponsable?.Persona != null
                        ? $"{m.DocenteResponsable.Persona.Nombre} {m.DocenteResponsable.Persona.Apellido}".Trim()
                        : (m.DocenteResponsable != null ? m.DocenteResponsable.Username : "Docente"),
                    docente = m.DocenteResponsable != null && m.DocenteResponsable.Persona != null ? new
                    {
                        id = m.DocenteResponsable.Id,
                        username = m.DocenteResponsable.Username,
                        nombre = $"{m.DocenteResponsable.Persona.Nombre} {m.DocenteResponsable.Persona.Apellido}".Trim(),
                        correo = m.DocenteResponsable.Persona.Correo,
                        cedula = m.DocenteResponsable.Persona.Cedula
                    } : null,
                    clases = (m.Clases ?? new List<Clase>()).Select(c => new
                    {
                        id = c.Id,
                        claseId = c.Id,
                        nombre = c.Nombre,
                        materiaId = c.MateriaId,
                        docenteId = c.DocenteId
                    }).ToList()
                };
            }).ToList();

            return Ok(result);
        }

        // GET /api/Materia/{id}: Retorna una materia por ID con su lista de clases (soporta long id)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMateriaById(long id)
        {
            if (id > int.MaxValue) return NotFound(new { message = "Materia no encontrada." });
            int mId = (int)id;

            var materia = await _context.Materias
                .Include(m => m.DocenteResponsable)
                    .ThenInclude(d => d.Persona)
                .Include(m => m.Clase)
                .Include(m => m.Clases)
                .FirstOrDefaultAsync(m => m.Id == mId);

            if (materia == null)
            {
                var catedra = await _context.Catedras
                    .Include(c => c.Docente)
                        .ThenInclude(d => d.Persona)
                    .FirstOrDefaultAsync(c => c.Id == mId);

                if (catedra != null)
                {
                    materia = new Materia
                    {
                        Nombre = catedra.Nombre,
                        Descripcion = "Cátedra Universitaria",
                        Codigo = $"CAT-{catedra.Id}",
                        DocenteResponsableId = catedra.DocenteId
                    };
                    _context.Materias.Add(materia);
                    await _context.SaveChangesAsync();
                }
                else
                {
                    return NotFound(new { message = "Materia no encontrada." });
                }
            }

            var catedraRelacionada = await _context.Catedras
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Nombre.ToLower() == materia.Nombre.ToLower());

            var result = new
            {
                id = materia.Id,
                catedraId = catedraRelacionada != null ? (int?)catedraRelacionada.Id : null,
                nombre = materia.Nombre,
                codigo = materia.Codigo,
                descripcion = materia.Descripcion,
                claseId = materia.ClaseId,
                claseNombre = materia.Clase != null ? materia.Clase.Nombre : null,
                docenteId = materia.DocenteResponsableId,
                docenteResponsableId = materia.DocenteResponsableId,
                nombreDocenteResponsable = materia.DocenteResponsable?.Persona != null
                    ? $"{materia.DocenteResponsable.Persona.Nombre} {materia.DocenteResponsable.Persona.Apellido}".Trim()
                    : (materia.DocenteResponsable != null ? materia.DocenteResponsable.Username : "Docente"),
                docente = materia.DocenteResponsable != null && materia.DocenteResponsable.Persona != null ? new
                {
                    id = materia.DocenteResponsable.Id,
                    username = materia.DocenteResponsable.Username,
                    nombre = $"{materia.DocenteResponsable.Persona.Nombre} {materia.DocenteResponsable.Persona.Apellido}".Trim(),
                    correo = materia.DocenteResponsable.Persona.Correo,
                    cedula = materia.DocenteResponsable.Persona.Cedula
                } : null,
                clases = (materia.Clases ?? new List<Clase>()).Select(c => new
                {
                    id = c.Id,
                    claseId = c.Id,
                    nombre = c.Nombre,
                    materiaId = c.MateriaId,
                    docenteId = c.DocenteId
                }).ToList()
            };

            return Ok(result);
        }

        // POST /api/Materia: Recibe DTO { nombre, codigo, descripcion, docenteId, claseId, ... }, persiste y sincroniza Catedra sin crear clases automáticamente
        [HttpPost]
        public async Task<IActionResult> CreateMateria([FromBody] JsonElement rawBody)
        {
            var dto = ParseMateriaDto(rawBody);

            var doc = await GetDefaultDocenteAsync(dto.DocenteId ?? dto.DocenteResponsableId);
            if (doc == null)
            {
                return BadRequest(new { message = "No se pudo asociar un docente a la materia." });
            }

            var cleanNombre = !string.IsNullOrWhiteSpace(dto.Nombre) ? dto.Nombre.Trim() : "Nueva Materia";
            var cleanCodigo = !string.IsNullOrWhiteSpace(dto.Codigo) ? dto.Codigo.Trim() : $"MAT-{new Random().Next(100, 999)}";
            var cleanDesc = dto.Descripcion?.Trim() ?? string.Empty;

            // Persistir Materia
            var materia = new Materia
            {
                Nombre = cleanNombre,
                Descripcion = cleanDesc,
                Codigo = cleanCodigo,
                DocenteResponsableId = doc.Id
            };

            if (dto.ClaseId.HasValue && dto.ClaseId.Value > 0)
            {
                materia.ClaseId = dto.ClaseId.Value;
            }

            _context.Materias.Add(materia);
            await _context.SaveChangesAsync();

            // Sincronizar Catedra
            var catedra = await _context.Catedras.FirstOrDefaultAsync(c => c.Nombre.ToLower() == cleanNombre.ToLower());
            if (catedra == null)
            {
                catedra = new Catedra
                {
                    Nombre = cleanNombre,
                    Semestre = !string.IsNullOrWhiteSpace(dto.Semestre) ? dto.Semestre.Trim() : "2026-1",
                    DocenteId = doc.Id,
                    MinimoNota = 70.0
                };
                _context.Catedras.Add(catedra);
                await _context.SaveChangesAsync();
            }
            else
            {
                catedra.DocenteId = doc.Id;
                await _context.SaveChangesAsync();
            }

            // Si se asignó claseId, cargar la clase para devolver su nombre
            if (materia.ClaseId.HasValue)
            {
                await _context.Entry(materia).Reference(m => m.Clase).LoadAsync();
            }

            var response = new
            {
                id = materia.Id,
                nombre = materia.Nombre,
                codigo = materia.Codigo,
                descripcion = materia.Descripcion,
                claseId = materia.ClaseId,
                claseNombre = materia.Clase != null ? materia.Clase.Nombre : null,
                docenteId = materia.DocenteResponsableId,
                docenteResponsableId = materia.DocenteResponsableId,
                nombreDocenteResponsable = doc.Persona != null ? $"{doc.Persona.Nombre} {doc.Persona.Apellido}".Trim() : doc.Username,
                docente = doc.Persona != null ? new
                {
                    id = doc.Id,
                    username = doc.Username,
                    nombre = $"{doc.Persona.Nombre} {doc.Persona.Apellido}".Trim(),
                    correo = doc.Persona.Correo,
                    cedula = doc.Persona.Cedula
                } : null,
                catedraId = catedra.Id,
                clases = new List<object>()
            };

            return CreatedAtAction(nameof(GetMateriaById), new { id = materia.Id }, response);
        }

        // PUT /api/Materia/{id}: Actualiza la materia
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateMateria(long id, [FromBody] JsonElement rawBody)
        {
            if (id > int.MaxValue) return NotFound(new { message = "Materia no encontrada." });
            int mId = (int)id;

            var materia = await _context.Materias
                .Include(m => m.DocenteResponsable)
                    .ThenInclude(d => d.Persona)
                .Include(m => m.Clase)
                .Include(m => m.Clases)
                .FirstOrDefaultAsync(m => m.Id == mId);

            var dto = ParseMateriaDto(rawBody);

            if (materia == null)
            {
                var catedraFound = await _context.Catedras.FindAsync(mId);
                if (catedraFound == null) return NotFound(new { message = "Materia no encontrada." });

                materia = new Materia
                {
                    Nombre = catedraFound.Nombre,
                    Descripcion = "Cátedra Universitaria",
                    Codigo = $"CAT-{mId}",
                    DocenteResponsableId = catedraFound.DocenteId
                };
                _context.Materias.Add(materia);
            }

            if (!string.IsNullOrWhiteSpace(dto.Nombre))
            {
                materia.Nombre = dto.Nombre.Trim();
            }
            if (dto.Descripcion != null)
            {
                materia.Descripcion = dto.Descripcion.Trim();
            }
            if (!string.IsNullOrWhiteSpace(dto.Codigo))
            {
                materia.Codigo = dto.Codigo.Trim();
            }

            if (dto.ClaseId.HasValue)
            {
                materia.ClaseId = dto.ClaseId.Value > 0 ? dto.ClaseId.Value : null;
            }

            if (dto.DocenteId.HasValue && dto.DocenteId.Value > 0)
            {
                var doc = await GetDefaultDocenteAsync(dto.DocenteId.Value);
                if (doc != null)
                {
                    materia.DocenteResponsableId = doc.Id;
                }
            }

            // Sincronizar Catedra
            var catedra = await _context.Catedras.FirstOrDefaultAsync(c => c.Id == mId || c.Nombre.ToLower() == materia.Nombre.ToLower());
            if (catedra != null)
            {
                catedra.Nombre = materia.Nombre;
                catedra.DocenteId = materia.DocenteResponsableId;
            }

            await _context.SaveChangesAsync();

            var updatedDoc = await _context.Users.Include(u => u.Persona).FirstOrDefaultAsync(u => u.Id == materia.DocenteResponsableId);
            if (materia.ClaseId.HasValue && materia.Clase == null)
            {
                await _context.Entry(materia).Reference(m => m.Clase).LoadAsync();
            }

            return Ok(new
            {
                id = materia.Id,
                nombre = materia.Nombre,
                codigo = materia.Codigo,
                descripcion = materia.Descripcion,
                claseId = materia.ClaseId,
                claseNombre = materia.Clase != null ? materia.Clase.Nombre : null,
                docenteId = materia.DocenteResponsableId,
                docenteResponsableId = materia.DocenteResponsableId,
                nombreDocenteResponsable = updatedDoc?.Persona != null ? $"{updatedDoc.Persona.Nombre} {updatedDoc.Persona.Apellido}".Trim() : (updatedDoc?.Username ?? "Docente"),
                docente = updatedDoc?.Persona != null ? new
                {
                    id = updatedDoc.Id,
                    username = updatedDoc.Username,
                    nombre = $"{updatedDoc.Persona.Nombre} {updatedDoc.Persona.Apellido}".Trim(),
                    correo = updatedDoc.Persona.Correo,
                    cedula = updatedDoc.Persona.Cedula
                } : null
            });
        }

        // DELETE /api/Materia/{id}: Elimina la materia (soporta long id) y responde 200 OK
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteMateria(long id)
        {
            if (id > int.MaxValue) return Ok(new { success = true, message = "Materia eliminada exitosamente." });
            int mId = (int)id;

            var materia = await _context.Materias
                .Include(m => m.Clases)
                .Include(m => m.Recursos)
                .Include(m => m.Actividades)
                .Include(m => m.ClasesSesiones)
                .FirstOrDefaultAsync(m => m.Id == mId);

            var catedra = await _context.Catedras
                .Include(c => c.Inscripciones)
                .Include(c => c.Evaluaciones)
                .FirstOrDefaultAsync(c => c.Id == mId || (materia != null && c.Nombre.ToLower() == materia.Nombre.ToLower()));

            if (materia == null && catedra == null)
            {
                return NotFound(new { message = "Materia no encontrada." });
            }

            if (materia != null)
            {
                if (materia.Clases != null && materia.Clases.Any())
                    _context.Clases.RemoveRange(materia.Clases);
                if (materia.Recursos != null && materia.Recursos.Any())
                    _context.Recursos.RemoveRange(materia.Recursos);
                if (materia.Actividades != null && materia.Actividades.Any())
                    _context.Actividades.RemoveRange(materia.Actividades);
                if (materia.ClasesSesiones != null && materia.ClasesSesiones.Any())
                    _context.ClasesSesiones.RemoveRange(materia.ClasesSesiones);

                _context.Materias.Remove(materia);
            }

            if (catedra != null)
            {
                if (catedra.Inscripciones != null && catedra.Inscripciones.Any())
                    _context.Inscripciones.RemoveRange(catedra.Inscripciones);
                if (catedra.Evaluaciones != null && catedra.Evaluaciones.Any())
                    _context.Evaluaciones.RemoveRange(catedra.Evaluaciones);

                _context.Catedras.Remove(catedra);
            }

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Materia eliminada exitosamente." });
        }

        // Helper para verificar si el usuario autenticado es el docente responsable de la Materia real.
        // IMPORTANTE: MateriaId y CatedraId no son necesariamente iguales.
        private async Task<bool> IsDocenteOfMateria(int materiaId)
        {
            if (UserId == null) return false;

            return await _context.Materias
                .AsNoTracking()
                .AnyAsync(m =>
                    m.Id == materiaId &&
                    m.DocenteResponsableId == UserId.Value);
        }

        // Resuelve la cátedra asociada a una Materia sin asumir que ambos IDs coinciden.
        private async Task<int?> GetCatedraIdByMateriaAsync(int materiaId)
        {
             if (UserId == null) return Unauthorized();
            if (!await IsDocenteOfMateria(materiaId)) return StatusCode(403, new { message = "Solo el docente responsable puede añadir recursos a esta materia." });
             var materia = await _context.Materias
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == materiaId);

            if (materia == null)
                return null;

            var nombreMateria = materia.Nombre?.Trim().ToLower();

            var catedraId = await _context.Catedras
                .AsNoTracking()
                .Where(c =>
                    c.DocenteId == materia.DocenteResponsableId &&
                    c.Nombre != null &&
                    c.Nombre.Trim().ToLower() == nombreMateria)
                .Select(c => (int?)c.Id)
                .FirstOrDefaultAsync();

            return catedraId;
        }

        // Verifica que el estudiante esté relacionado con la materia mediante su cátedra o clase.
        private async Task<bool> IsEstudianteOfMateria(int materiaId, int estudianteId)
        {
            var catedraId = await GetCatedraIdByMateriaAsync(materiaId);

            var claseIds = await _context.Clases
                .AsNoTracking()
                .Where(c => c.MateriaId == materiaId)
                .Select(c => c.Id)
                .ToListAsync();

            return await _context.Inscripciones
                .AsNoTracking()
                .AnyAsync(i =>
                    i.EstudianteId == estudianteId &&
                    ((catedraId.HasValue && i.CatedraId == catedraId.Value) ||
                     (i.ClaseId.HasValue && claseIds.Contains(i.ClaseId.Value))));
        }

        // Endpoint para añadir un recurso a una materia (solo docente responsable)
        [HttpPost("{materiaId}/recursos")]
        public async Task<IActionResult> AddRecurso(
            int materiaId,
            [FromBody] CreateRecursoDto createRecursoDto)
        {
            if (UserId == null)
                return Unauthorized();

            if (createRecursoDto == null)
                return BadRequest(new { message = "Los datos del recurso son obligatorios." });

            if (!await IsDocenteOfMateria(materiaId))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "Solo el docente responsable puede añadir recursos a esta materia." });
            }

            var materia = await _context.Materias.FindAsync(materiaId);
            if (materia == null)
                return NotFound(new { message = "Materia no encontrada." });

            if (string.IsNullOrWhiteSpace(createRecursoDto.Titulo))
                return BadRequest(new { message = "El título del recurso es obligatorio." });

            if (string.IsNullOrWhiteSpace(createRecursoDto.Url))
                return BadRequest(new { message = "Debe proporcionar el archivo o enlace del recurso." });

            var recurso = new Recurso
            {
                Titulo = createRecursoDto.Titulo.Trim(),
                Descripcion = createRecursoDto.Descripcion?.Trim() ?? string.Empty,
                Url = createRecursoDto.Url.Trim(),
                EsEsencial = createRecursoDto.EsEsencial,
                MateriaId = materiaId,
                Links = createRecursoDto.Links ?? new List<string>()
            };

            _context.Recursos.Add(recurso);
            await _context.SaveChangesAsync();

            return StatusCode(
                StatusCodes.Status201Created,
                new RecursoDto
                {
                    Id = recurso.Id,
                    Titulo = recurso.Titulo,
                    Descripcion = recurso.Descripcion,
                    Url = recurso.Url,
                    EsEsencial = recurso.EsEsencial,
                    MateriaId = recurso.MateriaId,
                    Links = recurso.Links ?? new List<string>()
                });
        }

        // PUT /api/Materia/{materiaId}/recursos/{recursoId}
        // Modifica un recurso existente de una materia del docente autenticado.
        [HttpPut("{materiaId}/recursos/{recursoId}")]
        public async Task<IActionResult> UpdateRecurso(
            int materiaId,
            int recursoId,
            [FromBody] CreateRecursoDto dto)
        {
            if (UserId == null)
                return Unauthorized();

            if (dto == null)
                return BadRequest(new { message = "Los datos del recurso son obligatorios." });

            if (!await IsDocenteOfMateria(materiaId))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "Solo el docente responsable puede modificar recursos de esta materia." });
            }

            var recurso = await _context.Recursos
                .FirstOrDefaultAsync(r =>
                    r.Id == recursoId &&
                    r.MateriaId == materiaId);

            if (recurso == null)
            {
                return NotFound(new
                {
                    message = "Recurso no encontrado en la materia indicada."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.Titulo))
                return BadRequest(new { message = "El título del recurso es obligatorio." });

            if (string.IsNullOrWhiteSpace(dto.Url))
                return BadRequest(new { message = "Debe proporcionar el archivo o enlace del recurso." });

            recurso.Titulo = dto.Titulo.Trim();
            recurso.Descripcion = dto.Descripcion?.Trim() ?? string.Empty;
            recurso.Url = dto.Url.Trim();
            recurso.EsEsencial = dto.EsEsencial;
            recurso.Links = dto.Links ?? new List<string>();

            await _context.SaveChangesAsync();

            return Ok(new RecursoDto
            {
                Id = recurso.Id,
                Titulo = recurso.Titulo,
                Descripcion = recurso.Descripcion,
                Url = recurso.Url,
                EsEsencial = recurso.EsEsencial,
                MateriaId = recurso.MateriaId,
                Links = recurso.Links ?? new List<string>()
            });
        }

        // Upload file to Supabase Storage and return public URL (does not create recurso record)
        [HttpPost("{materiaId}/recursos/upload")]
        public async Task<IActionResult> UploadRecursoFile(int materiaId, IFormFile archivo)
        {
            if (UserId == null) return Unauthorized();
             if (!await IsDocenteOfMateria(materiaId)) return StatusCode(403, new { message = "Solo el docente responsable puede subir archivos a esta materia." });
             if (!await IsDocenteOfMateria(materiaId))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "Solo el docente responsable puede subir archivos a esta materia." });
            }

            if (archivo == null || archivo.Length == 0)
                return BadRequest(new { message = "Archivo requerido." });

            var supabaseUrl = _config["Supabase:Url"];
            var supabaseKey = _config["Supabase:ServiceKey"];
            var bucket = _config["Supabase:StorageBucket"] ?? "public";

            if (string.IsNullOrWhiteSpace(supabaseUrl) || string.IsNullOrWhiteSpace(supabaseKey))
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    new
                    {
                        message = "Supabase storage no está configurado. Configure Supabase:Url y Supabase:ServiceKey en la configuración."
                    });
            }

            var fileName = $"{Guid.NewGuid():N}_{System.IO.Path.GetFileName(archivo.FileName)}";

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Add("apikey", supabaseKey);
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", supabaseKey);

            using var content = new MultipartFormDataContent();
            using var stream = archivo.OpenReadStream();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType =
                new System.Net.Http.Headers.MediaTypeHeaderValue(
                    archivo.ContentType ?? "application/octet-stream");
            content.Add(fileContent, "file", fileName);

            var uploadUrl =
                $"{supabaseUrl.TrimEnd('/')}/storage/v1/object/{bucket}?name={Uri.EscapeDataString(fileName)}";

            var resp = await client.PostAsync(uploadUrl, content);
            var respBody = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                return StatusCode(
                    (int)resp.StatusCode,
                    new { message = "Upload failed", detail = respBody });
            }

            var publicUrl =
                $"{supabaseUrl.TrimEnd('/')}/storage/v1/object/public/{bucket}/{Uri.EscapeDataString(fileName)}";

            return Ok(new { url = publicUrl, key = fileName, raw = respBody });
        }

        // Endpoint para obtener todos los recursos de una materia.
        [HttpGet("{materiaId}/recursos")]
        public async Task<ActionResult<IEnumerable<RecursoDto>>> GetRecursosByMateria(int materiaId)
        {
            if (UserId == null)
                return Unauthorized();

            var materiaExiste = await _context.Materias
                .AsNoTracking()
                .AnyAsync(m => m.Id == materiaId);

            if (!materiaExiste)
                return NotFound(new { message = "Materia no encontrada." });

            var recursos = await _context.Recursos
                .AsNoTracking()
                .Where(r => r.MateriaId == materiaId)
                .OrderBy(r => r.Id)
                .Select(r => new RecursoDto
                {
                    Id = r.Id,
                    Titulo = r.Titulo,
                    Descripcion = r.Descripcion,
                    Url = r.Url,
                    EsEsencial = r.EsEsencial,
                    MateriaId = r.MateriaId
                })
                .ToListAsync();

            return Ok(recursos);
        }

        // GET /api/Materia/{materiaId}/temas
        [HttpGet("{materiaId}/temas")]
        public async Task<IActionResult> GetTemasByMateria(int materiaId)
        {
            if (UserId == null) return Unauthorized();

            var temas = await _context.Temas
                .Where(t => t.MateriaId == materiaId)
                .OrderBy(t => t.Orden)
                .Select(t => new DTOs.TemaDto
                {
                    Id = t.Id,
                    MateriaId = t.MateriaId,
                    Titulo = t.Titulo,
                    Descripcion = t.Descripcion,
                    Orden = t.Orden
                })
                .ToListAsync();

            return Ok(temas);
        }

        // POST /api/Materia/{materiaId}/temas
        [HttpPost("{materiaId}/temas")]
        public async Task<IActionResult> AddTemaToMateria(int materiaId, [FromBody] DTOs.TemaDto temaDto)
        {
            if (UserId == null) return Unauthorized();

             // Solo el docente responsable puede añadir temas
            if (!await IsDocenteOfMateria(materiaId)) return StatusCode(403, new { message = "Solo el docente responsable puede añadir temas a esta materia." });
             if (!await IsDocenteOfMateria(materiaId))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "Solo el docente responsable puede añadir temas a esta materia." });
            }

            var materia = await _context.Materias.FindAsync(materiaId);
            if (materia == null)
                return NotFound(new { message = "Materia no encontrada." });

            var tema = new Entities.Tema
            {
                MateriaId = materiaId,
                Titulo = temaDto.Titulo,
                Descripcion = temaDto.Descripcion,
                Orden = temaDto.Orden
            };

            _context.Temas.Add(tema);
            await _context.SaveChangesAsync();

            temaDto.Id = tema.Id;
            temaDto.MateriaId = tema.MateriaId;

            return CreatedAtAction(
                nameof(GetTemasByMateria),
                new { materiaId },
                temaDto);
        }

        // Normaliza fechas recibidas desde Swagger/frontend para PostgreSQL timestamp with time zone.
        // Si la fecha llega sin zona horaria (Kind.Unspecified), se conserva la hora enviada
        // y se marca como UTC para evitar errores de Npgsql.
        private static System.DateTime NormalizarFechaUtc(System.DateTime fecha)
        {
             if (UserId == null) return Unauthorized();
            if (!await IsDocenteOfMateria(materiaId)) return StatusCode(403, new { message = "Solo el docente responsable puede añadir actividades a esta materia." });
             return fecha.Kind switch
            {
                System.DateTimeKind.Utc => fecha,
                System.DateTimeKind.Local => fecha.ToUniversalTime(),
                _ => System.DateTime.SpecifyKind(fecha, System.DateTimeKind.Utc)
            };
        }

        // Endpoint para añadir una actividad a una materia (solo docente responsable)
        [HttpPost("{materiaId}/actividades")]
        public async Task<IActionResult> AddActividad(
            int materiaId,
            [FromBody] CreateActividadDto createActividadDto)
        {
            if (UserId == null)
                return Unauthorized();

            if (createActividadDto == null)
                return BadRequest(new { message = "Los datos de la actividad son obligatorios." });

            if (!await IsDocenteOfMateria(materiaId))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "Solo el docente responsable puede añadir actividades a esta materia." });
            }

            var materia = await _context.Materias.FindAsync(materiaId);
            if (materia == null)
                return NotFound(new { message = "Materia no encontrada." });

            if (string.IsNullOrWhiteSpace(createActividadDto.Titulo))
                return BadRequest(new { message = "El título de la actividad es obligatorio." });

            if (string.IsNullOrWhiteSpace(createActividadDto.Tipo))
                return BadRequest(new { message = "El tipo de actividad es obligatorio." });

            if (createActividadDto.FechaEntrega == default)
                return BadRequest(new { message = "La fecha límite de entrega es obligatoria." });

            var actividad = new Actividad
            {
                Titulo = createActividadDto.Titulo.Trim(),
                Descripcion = createActividadDto.Descripcion?.Trim() ?? string.Empty,
                FechaEntrega = NormalizarFechaUtc(createActividadDto.FechaEntrega),
                Tipo = createActividadDto.Tipo.Trim(),
                Estado = "Pendiente",
                MateriaId = materiaId
            };

            _context.Actividades.Add(actividad);
            await _context.SaveChangesAsync();

            return StatusCode(
                StatusCodes.Status201Created,
                new ActividadDto
                {
                    Id = actividad.Id,
                    Titulo = actividad.Titulo,
                    Descripcion = actividad.Descripcion,
                    FechaEntrega = actividad.FechaEntrega,
                    Tipo = actividad.Tipo,
                    Estado = actividad.Estado,
                    MateriaId = actividad.MateriaId
                });
        }

        // PUT /api/Materia/{materiaId}/actividades/{actividadId}
        // Modifica una actividad existente de la materia del docente autenticado.
        [HttpPut("{materiaId}/actividades/{actividadId}")]
        public async Task<IActionResult> UpdateActividad(
            int materiaId,
            int actividadId,
            [FromBody] CreateActividadDto dto)
        {
            if (UserId == null)
                return Unauthorized();

            if (dto == null)
                return BadRequest(new { message = "Los datos de la actividad son obligatorios." });

            if (!await IsDocenteOfMateria(materiaId))
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "Solo el docente responsable puede modificar actividades de esta materia." });
            }

            var actividad = await _context.Actividades
                .FirstOrDefaultAsync(a =>
                    a.Id == actividadId &&
                    a.MateriaId == materiaId);

            if (actividad == null)
            {
                return NotFound(new
                {
                    message = "Actividad no encontrada en la materia indicada."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.Titulo))
                return BadRequest(new { message = "El título de la actividad es obligatorio." });

            if (string.IsNullOrWhiteSpace(dto.Tipo))
                return BadRequest(new { message = "El tipo de actividad es obligatorio." });

            if (dto.FechaEntrega == default)
                return BadRequest(new { message = "La fecha límite de entrega es obligatoria." });

            actividad.Titulo = dto.Titulo.Trim();
            actividad.Descripcion = dto.Descripcion?.Trim() ?? string.Empty;
            actividad.FechaEntrega = NormalizarFechaUtc(dto.FechaEntrega);
            actividad.Tipo = dto.Tipo.Trim();

            await _context.SaveChangesAsync();

            return Ok(new ActividadDto
            {
                Id = actividad.Id,
                Titulo = actividad.Titulo,
                Descripcion = actividad.Descripcion,
                FechaEntrega = actividad.FechaEntrega,
                Tipo = actividad.Tipo,
                Estado = actividad.Estado,
                MateriaId = actividad.MateriaId
            });
        }

        // Endpoint para obtener todas las actividades de una materia.
        [HttpGet("{materiaId}/actividades")]
        public async Task<ActionResult<IEnumerable<ActividadDto>>> GetActividadesByMateria(int materiaId)
        {
            if (UserId == null)
                return Unauthorized();

            var materiaExiste = await _context.Materias
                .AsNoTracking()
                .AnyAsync(m => m.Id == materiaId);

            if (!materiaExiste)
                return NotFound(new { message = "Materia no encontrada." });

            var actividades = await _context.Actividades
                .AsNoTracking()
                .Where(a => a.MateriaId == materiaId)
                .OrderBy(a => a.FechaEntrega)
                .Select(a => new ActividadDto
                {
                    Id = a.Id,
                    Titulo = a.Titulo,
                    Descripcion = a.Descripcion,
                    FechaEntrega = a.FechaEntrega,
                    Tipo = a.Tipo,
                    Estado = a.Estado,
                    MateriaId = a.MateriaId
                })
                .ToListAsync();

            return Ok(actividades);
        }

        // Nuevo endpoint para que un estudiante marque un recurso como visto
        [HttpPost("recursos/marcar-visto")]
        public async Task<IActionResult> MarkRecursoAsSeen([FromBody] MarkRecursoAsSeenDto markRecursoAsSeenDto)
        {
            if (UserId == null) return Unauthorized();

            // Verificar que el recurso existe
            var recurso = await _context.Recursos.FindAsync(markRecursoAsSeenDto.RecursoId);
            if (recurso == null) return NotFound(new { message = "Recurso no encontrado." });

            // Verificar inscripción usando la relación real Materia -> Cátedra/Clase.
            var isStudentInMateria = await IsEstudianteOfMateria(recurso.MateriaId, UserId.Value);
            if (!isStudentInMateria)
            {
                 return StatusCode(403, new { message = "No tienes permiso para marcar este recurso como visto, ya que no estás inscrito en la materia." });
                 return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "No tienes permiso para marcar este recurso como visto, ya que no estás inscrito en la materia." });
             }

            // Verificar si ya está marcado como visto
            var existingEntry = await _context.RecursosVistosPorEstudiante
                                            .FirstOrDefaultAsync(rv => rv.RecursoId == markRecursoAsSeenDto.RecursoId && rv.EstudianteId == UserId.Value);

            if (existingEntry != null)
            {
                return Conflict(new { message = "Este recurso ya ha sido marcado como visto por este estudiante." });
            }

            var recursoVisto = new RecursoVistoPorEstudiante
            {
                RecursoId = markRecursoAsSeenDto.RecursoId,
                EstudianteId = UserId.Value,
                FechaVisto = DateTime.UtcNow
            };

            _context.RecursosVistosPorEstudiante.Add(recursoVisto);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Recurso marcado como visto exitosamente." });
        }

        // Nuevo endpoint para obtener el estado de los recursos de una materia (visto/no visto por el estudiante)
        [HttpGet("{materiaId}/recursos/estado")]
        public async Task<ActionResult<IEnumerable<RecursoConEstadoDto>>> GetRecursosConEstadoByMateria(int materiaId)
        {
            if (UserId == null) return Unauthorized();

            // Verificar inscripción usando la relación real Materia -> Cátedra/Clase.
            var isStudentInMateria = await IsEstudianteOfMateria(materiaId, UserId.Value);
            if (!isStudentInMateria)
            {
                 return StatusCode(403, new { message = "No tienes permiso para ver el estado de los recursos de esta materia." });
                 return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "No tienes permiso para ver el estado de los recursos de esta materia." });
             }

            var recursos = await _context.Recursos
                                .Where(r => r.MateriaId == materiaId)
                                .Select(r => new RecursoConEstadoDto
                                {
                                    Id = r.Id,
                                    Titulo = r.Titulo,
                                    Descripcion = r.Descripcion,
                                    Url = r.Url,
                                    EsEsencial = r.EsEsencial,
                                    MateriaId = r.MateriaId,
                                    Visto = _context.RecursosVistosPorEstudiante
                                                    .Any(rv => rv.RecursoId == r.Id && rv.EstudianteId == UserId.Value)
                                }).ToListAsync();

            return Ok(recursos);
        }
    }
}

