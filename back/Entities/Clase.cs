using System.Collections.Generic;

namespace back.Entities
{
    public class Clase
    {
        public int Id { get; set; }
        public string Nombre { get; set; } // e.g., "Modelamiento 2023-2"
        public int MateriaId { get; set; }
        public int DocenteId { get; set; } // El docente específico a cargo de esta instancia de clase

        // Navigation properties
        public Materia Materia { get; set; }
        public User Docente { get; set; }

        // Relación muchos a muchos con estudiantes
        public ICollection<User> Estudiantes { get; set; } = new List<User>();
    }
}
