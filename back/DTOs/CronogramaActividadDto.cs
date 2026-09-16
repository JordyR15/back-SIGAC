using System;

namespace back.DTOs
{
    public class CronogramaActividadDto
    {
        public int Id { get; set; }

        public int CatedraId { get; set; }

        public string Descripcion { get; set; } = string.Empty;

        public DateTime FechaPrevista { get; set; }

        public DateTime? FechaReal { get; set; }

        // RF-005
        // Obligatoria cuando se reprograma una actividad.
        public string ObservacionCambio { get; set; } = string.Empty;
    }
}