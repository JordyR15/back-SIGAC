namespace back.Entities
{
    public class Asistencia
    {
        public int Id { get; set; }
        public int ClaseSesionId { get; set; }
        public int EstudianteId { get; set; } // FK to User (Estudiante)
        public bool Presente { get; set; }

        // Navigation properties
        public ClaseSesion ClaseSesion { get; set; }
        public User Estudiante { get; set; }
    }
}