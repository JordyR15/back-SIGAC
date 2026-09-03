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
        public ICollection<ClaseSesion> ClasesSesionesDocente { get; set; } = new List<ClaseSesion>();
        public ICollection<Asistencia> AsistenciasEstudiante { get; set; } = new List<Asistencia>();
        public ICollection<Clase> ClasesDocente { get; set; } = new List<Clase>();
        public ICollection<Clase> ClasesEstudiante { get; set; } = new List<Clase>();
        public ICollection<RecursoVistoPorEstudiante> RecursosVistos { get; set; } = new List<RecursoVistoPorEstudiante>();
        public ICollection<Presentacion> PresentacionesJurado { get; set; } = new List<Presentacion>();
    }
}
