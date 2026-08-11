using System.Collections.Generic;

namespace back.Entities
{
    public class Materia
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string Codigo { get; set; } // e.g., "MSI301"
        public int DocenteResponsableId { get; set; } // FK to User (Docente)

        // Navigation properties
        public User DocenteResponsable { get; set; }
        public ICollection<Recurso> Recursos { get; set; }
        public ICollection<Actividad> Actividades { get; set; }
        public ICollection<Clase> Clases { get; set; } // Instances of this Materia
        public ICollection<ClaseSesion> ClasesSesiones { get; set; } // All sessions for this materia
    }
}
