using System.Collections.Generic;

namespace back.DTOs
{
    public class CreateClaseDto
    {
        public string? Nombre { get; set; }
        public long? MateriaId { get; set; }
        public long? DocenteId { get; set; }
        public ICollection<int>? EstudianteIds { get; set; } // IDs de estudiantes a asignar a la clase
    }
}
