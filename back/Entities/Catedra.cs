using System.Collections.Generic;

namespace back.Entities
{
    public class Catedra
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Semestre { get; set; } // Añadido: e.g., "2023-1", "2023-2"
        public int DocenteId { get; set; }
        public User Docente { get; set; }
        public ICollection<Inscripcion> Inscripciones { get; set; } = new List<Inscripcion>();
        public ICollection<Evaluacion> Evaluaciones { get; set; } = new List<Evaluacion>();

        // Nota mínima requerida para postulaciones/aceptación. Null = sin restricción
        public double? MinimoNota { get; set; }

        // Propiedad añadida para la relación con Ayudantia
        public ICollection<Ayudantia> Ayudantias { get; set; } = new List<Ayudantia>();
    }
}
