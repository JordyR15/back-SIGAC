using System.Collections.Generic;

namespace back.DTOs
{
    public class ClaseDto
    {
        public int Id { get; set; }
        public int ClaseId { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public int MateriaId { get; set; }
        public int? CatedraId { get; set; }
        public string PeriodoAcademico { get; set; } = string.Empty;
        public string MateriaNombre { get; set; } = string.Empty;
        public string MateriaCodigo { get; set; } = string.Empty;
        public int DocenteId { get; set; }
        public string DocenteNombre { get; set; } = string.Empty;
        public string DocenteEmail { get; set; } = string.Empty;
        public ICollection<int> EstudianteIds { get; set; } = new List<int>();
    }
}
