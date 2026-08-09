using System;

namespace back.DTOs
{
    public class IndicadorCualitativoDto
    {
        public int Id { get; set; }
        public int EstudianteId { get; set; }
        public int CatedraId { get; set; }
        public string Indicador { get; set; }
        public string Observacion { get; set; }
        public DateTime Fecha { get; set; }
    }
}
