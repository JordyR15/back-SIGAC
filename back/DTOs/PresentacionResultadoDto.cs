using System.Collections.Generic;

namespace back.DTOs
{
    public class PresentacionResultadoDto
    {
        public double PromedioEvaluaciones { get; set; }
        public double PromedioFinalPonderado { get; set; }
        public double NotaFinal { get; set; }
        public double PromedioEstudiante { get; set; }
        public double PromedioCatedra { get; set; }
        public bool EstudianteSuperiorPromedioCatedra { get; set; }
        public bool EstudianteSuperiorPromedioPresentacion { get; set; }
        public int EvaluacionesCount { get; set; }
        public bool Aprobado { get; set; }
        public string EstadoFinal { get; set; } = string.Empty;
        public List<string> ObservacionesJurado { get; set; } = new List<string>();
        public string Observaciones { get; set; } = string.Empty;
        public double? DominioCientificoPromedio { get; set; }
        public double? DestrezaPedagogicaPromedio { get; set; }
        public double? DesenvolvimientoPromedio { get; set; }
    }
}
