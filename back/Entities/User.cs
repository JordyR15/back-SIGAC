using System.Collections.Generic;

namespace back.Entities
{
    public class User
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public byte[] PasswordHash { get; set; }
        public byte[] PasswordSalt { get; set; }

        // Propiedad de navegación a Persona
        public Persona Persona { get; set; }

        // Colecciones para las relaciones
        public ICollection<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();
        public ICollection<Catedra> CatedrasDocente { get; set; } = new List<Catedra>();
        public ICollection<Ayudantia> AyudantiasEstudiante { get; set; } = new List<Ayudantia>();
    }
}
