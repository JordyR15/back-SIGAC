using System;

namespace back.Entities
{
    public class EstudianteRecursoVisto
    {
        public int EstudianteId { get; set; }
        public int RecursoId { get; set; }
        public DateTime FechaVisto { get; set; }

        // Navigation properties
        public User Estudiante { get; set; } // Assuming User entity represents Estudiante
        public Recurso Recurso { get; set; }
    }
}
