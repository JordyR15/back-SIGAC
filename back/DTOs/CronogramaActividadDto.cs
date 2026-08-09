using System;

namespace back.DTOs
{
    public class CronogramaActividadDto
    {
        public int Id { get; set; }
        public int CatedraId { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaPrevista { get; set; }
        public DateTime? FechaReal { get; set; }
    }
}
