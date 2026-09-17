using System;

namespace back.Entities
{
    public class HistorialCronograma
    {
        public int Id { get; set; }

        public int CronogramaActividadId { get; set; }

        public CronogramaActividad CronogramaActividad { get; set; } = null!;

        public DateTime FechaAnterior { get; set; }

        public DateTime FechaNueva { get; set; }

        public string DescripcionAnterior { get; set; } = string.Empty;

        public string DescripcionNueva { get; set; } = string.Empty;

        public string ObservacionCambio { get; set; } = string.Empty;

        public DateTime FechaModificacion { get; set; }
            = DateTime.UtcNow;
    }
}