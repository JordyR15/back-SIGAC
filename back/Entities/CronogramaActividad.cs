using System;
using System.Collections.Generic;

namespace back.Entities
{
    public class CronogramaActividad
    {
        public int Id { get; set; }

        public int CatedraId { get; set; }
        public Catedra Catedra { get; set; } = null!;

        public string Descripcion { get; set; } = string.Empty;

        public DateTime FechaPrevista { get; set; }

        public DateTime? FechaReal { get; set; }

        // RF-005
        // Cada reprogramación queda guardada en el historial.
        public ICollection<HistorialCronograma> Historial { get; set; }
            = new List<HistorialCronograma>();
    }
}