using System.Collections.Generic;

namespace back.Entities
{
    public class Catedra
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public int DocenteId { get; set; }
        public User Docente { get; set; }
        public ICollection<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();
        public ICollection<Evaluacion> Evaluaciones { get; set; } = new List<Evaluacion>();

        // Propiedad añadida para la relación con Ayudantia
        public ICollection<Ayudantia> Ayudantias { get; set; } = new List<Ayudantia>();
    }
}
