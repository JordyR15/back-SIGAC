using System.ComponentModel.DataAnnotations;

namespace back.Entities
{
    public class Persona
    {
        public int Id { get; set; }
        [Required]
        public string Nombre { get; set; }
        [Required]
        public string Apellido { get; set; }
        [Required]
        public string Correo { get; set; }
        [Required]
        public string Rol { get; set; } // "Estudiante", "Docente", "Coordinador"

        // Relación uno a uno con User
        public int UserId { get; set; }
        public User User { get; set; }
    }
}
