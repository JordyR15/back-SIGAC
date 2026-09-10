using back.Data;
using back.DTOs;
using back.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public MateriaController(AppDbContext context)
        {
            _context = context;
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

        // GET /api/Materia: Retorna todas las materias con su docente asignado (HTTP 200)
        [HttpGet]
        public async Task<IActionResult> GetAllMaterias()
        {
            var materias = await _context.Materias
                .Include(m => m.DocenteResponsable)
                    .ThenInclude(d => d.Persona)
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

            var result = materias.Select(m => new
            {
                id = m.Id,
                nombre = m.Nombre,
                codigo = m.Codigo,
                descripcion = m.Descripcion,
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
                } : null
            }).ToList();

            return Ok(result);
        }

        // GET /api/Materia/{id}: Retorna una materia por ID (soporta long id)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetMateriaById(long id)
        {
            if (id > int.MaxValue) return NotFound(new { message = "Materia no encontrada." });
            int mId = (int)id;

            var materia = await _context.Materias
                .Include(m => m.DocenteResponsable)
                    .ThenInclude(d => d.Persona)
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

            var result = new
            {
                id = materia.Id,
                nombre = materia.Nombre,
                codigo = materia.Codigo,
                descripcion = materia.Descripcion,
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
                } : null
            };

            return Ok(result);
        }

        // POST /api/Materia: Recibe DTO { nombre, codigo, descripcion, docenteId, ... }, persiste y sincroniza Catedra
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

            // Asegurar que exista al menos una Clase vinculada para visibilidad inmediata en el panel del Docente
            var claseExistente = await _context.Clases.FirstOrDefaultAsync(c => c.MateriaId == materia.Id && c.DocenteId == doc.Id);
            if (claseExistente == null)
            {
                var nuevaClase = new Clase
                {
                    Nombre = $"{materia.Nombre} - Paralelo A",
                    MateriaId = materia.Id,
                    DocenteId = doc.Id
                };
                _context.Clases.Add(nuevaClase);
                await _context.SaveChangesAsync();
            }

            var response = new
            {
                id = materia.Id,
                nombre = materia.Nombre,
                codigo = materia.Codigo,
                descripcion = materia.Descripcion,
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
                catedraId = catedra.Id
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

            return Ok(new
            {
                id = materia.Id,
                nombre = materia.Nombre,
                codigo = materia.Codigo,
                descripcion = materia.Descripcion,
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

        // Helper para verificar si el usuario es docente de la materia
        private async Task<bool> IsDocenteOfMateria(int materiaId)
        {
            if (UserId == null) return false;

            var materia = await _context.Catedras.AsNoTracking()
                                .FirstOrDefaultAsync(m => m.Id == materiaId && m.DocenteId == UserId.Value);
            return materia != null;
        }

        // Endpoint para añadir un recurso a una materia (solo docentes)
        [HttpPost("{materiaId}/recursos")]
        public async Task<IActionResult> AddRecurso(int materiaId, [FromBody] CreateRecursoDto createRecursoDto)
        {
            if (UserId == null) return Unauthorized();
            if (!await IsDocenteOfMateria(materiaId)) return Forbid("Solo el docente responsable puede añadir recursos a esta materia.");

            var materia = await _context.Catedras.FindAsync(materiaId);
            if (materia == null) return NotFound(new { message = "Materia no encontrada." });

            var recurso = new Recurso
            {
                Titulo = createRecursoDto.Titulo,
                Descripcion = createRecursoDto.Descripcion,
                Url = createRecursoDto.Url,
                EsEsencial = createRecursoDto.EsEsencial,
                MateriaId = materiaId
            };

            _context.Recursos.Add(recurso);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetRecursosByMateria), new { materiaId = materiaId }, new RecursoDto
            {
                Id = recurso.Id,
                Titulo = recurso.Titulo,
                Descripcion = recurso.Descripcion,
                Url = recurso.Url,
                EsEsencial = recurso.EsEsencial,
                MateriaId = recurso.MateriaId
            });
        }

        // Endpoint para obtener todos los recursos de una materia (todos los usuarios autorizados)
        [HttpGet("{materiaId}/recursos")]
        public async Task<ActionResult<IEnumerable<RecursoDto>>> GetRecursosByMateria(int materiaId)
        {
            if (UserId == null) return Unauthorized();

            var recursos = await _context.Recursos
                                .Where(r => r.MateriaId == materiaId)
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

            if (!recursos.Any()) return NotFound(new { message = "No se encontraron recursos para esta materia." });

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
            if (!await IsDocenteOfMateria(materiaId)) return Forbid("Solo el docente responsable puede añadir temas a esta materia.");

            var materia = await _context.Catedras.FindAsync(materiaId);
            if (materia == null) return NotFound(new { message = "Materia no encontrada." });

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

            return CreatedAtAction(nameof(GetTemasByMateria), new { materiaId = materiaId }, temaDto);
        }

        // Endpoint para añadir una actividad a una materia (solo docentes)
        [HttpPost("{materiaId}/actividades")]
        public async Task<IActionResult> AddActividad(int materiaId, [FromBody] CreateActividadDto createActividadDto)
        {
            if (UserId == null) return Unauthorized();
            if (!await IsDocenteOfMateria(materiaId)) return Forbid("Solo el docente responsable puede añadir actividades a esta materia.");

            var materia = await _context.Catedras.FindAsync(materiaId);
            if (materia == null) return NotFound(new { message = "Materia no encontrada." });

            var actividad = new Actividad
            {
                Titulo = createActividadDto.Titulo,
                Descripcion = createActividadDto.Descripcion,
                FechaEntrega = createActividadDto.FechaEntrega,
                Tipo = createActividadDto.Tipo,
                Estado = "Pendiente", // Estado inicial por defecto
                MateriaId = materiaId
            };

            _context.Actividades.Add(actividad);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetActividadesByMateria), new { materiaId = materiaId }, new ActividadDto
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

        // Endpoint para obtener todas las actividades de una materia (todos los usuarios autorizados)
        [HttpGet("{materiaId}/actividades")]
        public async Task<ActionResult<IEnumerable<ActividadDto>>> GetActividadesByMateria(int materiaId)
        {
            if (UserId == null) return Unauthorized();

            var actividades = await _context.Actividades
                                .Where(a => a.MateriaId == materiaId)
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

            if (!actividades.Any()) return NotFound(new { message = "No se encontraron actividades para esta materia." });

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

            // Verificar si el estudiante está inscrito en la materia del recurso
            var isStudentInMateria = await _context.Inscripciones
                                        .AnyAsync(i => i.EstudianteId == UserId.Value && i.CatedraId == recurso.MateriaId);
            if (!isStudentInMateria)
            {
                return Forbid("No tienes permiso para marcar este recurso como visto, ya que no estás inscrito en la materia.");
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

            // Verificar si el estudiante está inscrito en la materia
            var isStudentInMateria = await _context.Inscripciones
                                        .AnyAsync(i => i.EstudianteId == UserId.Value && i.CatedraId == materiaId);
            if (!isStudentInMateria)
            {
                return Forbid("No tienes permiso para ver el estado de los recursos de esta materia.");
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
                                })
                                .ToListAsync();

            if (!recursos.Any()) return NotFound(new { message = "No se encontraron recursos para esta materia." });

            return Ok(recursos);
        }
    }
}
