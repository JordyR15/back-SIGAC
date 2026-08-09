using System.Collections.Generic;

namespace back.DTOs
{
    public class ExpedienteDto
    {
        public int EstudianteId { get; set; }
        public string NombreEstudiante { get; set; }
        public List<HistorialAcademicoDto> Historial { get; set; } = new List<HistorialAcademicoDto>();
        public List<IndicadorCualitativoDto> Indicadores { get; set; } = new List<IndicadorCualitativoDto>();
    }

    public class HistorialAcademicoDto
    {
        public string NombreCatedra { get; set; }
        public double CalificacionFinal { get; set; }
        public string Periodo { get; set; }
    }
}
