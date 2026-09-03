using System;

namespace back.Entities
{
    public class EstudianteActividadRealizada
    {
        public int Id { get; set; }
        public int EstudianteId { get; set; }
        public int ActividadId { get; set; }
        public DateTime? FechaRealizada { get; set; }
        public bool Completada { get; set; }
        public string ArchivoUrl { get; set; } = string.Empty;
        public decimal? Calificacion { get; set; }
        public string Retroalimentacion { get; set; } = string.Empty;

        public User Estudiante { get; set; }
        public Actividad Actividad { get; set; }
    }
}
