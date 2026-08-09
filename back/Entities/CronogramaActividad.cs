using System;

namespace back.Entities
{
    public class CronogramaActividad
    {
        public int Id { get; set; }
        public int CatedraId { get; set; }
        public Catedra Catedra { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaPrevista { get; set; }
        public DateTime? FechaReal { get; set; } // Para reprogramaciones
    }
}
