using System.Collections.Generic;

namespace back.Entities
{
    public class Ayudantia
    {
        public int Id { get; set; }
        public int CatedraId { get; set; }
        public Catedra Catedra { get; set; }
        public int EstudianteId { get; set; } // Ayudante
        public User Estudiante { get; set; }
        public string Estado { get; set; } // Ej: "Activa", "Finalizada", "Pendiente"

        public ICollection<ActividadAyudantia> Planificacion { get; set; } = new List<ActividadAyudantia>();
        public ICollection<Bitacora> Bitacoras { get; set; } = new List<Bitacora>();

        // Presentaciones públicas/privadas que han ocurrido para esta postulacion/ayudantía
        public ICollection<Presentacion> Presentaciones { get; set; } = new List<Presentacion>();
    }
}
