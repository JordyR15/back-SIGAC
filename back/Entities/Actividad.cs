using System;

namespace back.Entities
{
    public class Actividad
    {
        public int Id { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaEntrega { get; set; }
        public string Tipo { get; set; } // e.g., "Tarea", "Examen", "Proyecto"
        public string Estado { get; set; } // e.g., "Pendiente", "Completada", "Vencida"
        public int MateriaId { get; set; }

        // Navigation property
        public Materia Materia { get; set; }
    }
}