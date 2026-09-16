using System;

namespace back.Entities
{
    public class Evaluacion
    {
        public int Id { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public int CatedraId { get; set; }

        public Catedra Catedra { get; set; } = null!;

        public bool EsDiagnostica { get; set; }

        public DateTime? FechaInicio { get; set; }

        public DateTime? FechaFin { get; set; }

        public string TipoEvaluacion { get; set; } = "Archivo";

        public string Instrucciones { get; set; } = string.Empty;

        public string? ArchivoDocenteUrl { get; set; }

        // Preguntas creadas por el docente cuando TipoEvaluacion = "Cuestionario"
        public string? PreguntasCuestionario { get; set; }
    }
}