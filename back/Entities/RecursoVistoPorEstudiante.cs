using System;

namespace back.Entities
{
    public class RecursoVistoPorEstudiante
    {
        public int Id { get; set; }
        public int RecursoId { get; set; }
        public int EstudianteId { get; set; } // FK to User (Estudiante)
        public DateTime FechaVisto { get; set; }

        // Navigation properties
        public Recurso Recurso { get; set; }
        public User Estudiante { get; set; }
    }
}
