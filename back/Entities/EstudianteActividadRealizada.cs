using System;

namespace back.Entities
{
    public class EstudianteActividadRealizada
    {
        public int EstudianteId { get; set; }
        public int ActividadId { get; set; }
        public DateTime? FechaRealizada { get; set; } // Nullable if not yet completed
        public bool Completada { get; set; }

        // Navigation properties
        public User Estudiante { get; set; } // Assuming User entity represents Estudiante
        public Actividad Actividad { get; set; }
    }
}
