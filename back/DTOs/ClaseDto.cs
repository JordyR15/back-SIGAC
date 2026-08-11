using System.Collections.Generic;

namespace back.DTOs
{
    public class ClaseDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public int MateriaId { get; set; }
        public int DocenteId { get; set; }
        public ICollection<int> EstudianteIds { get; set; } // Solo IDs de estudiantes
    }
}
