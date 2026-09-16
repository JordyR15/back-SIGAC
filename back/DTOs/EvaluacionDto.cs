using System;

namespace back.DTOs
{
    public class EvaluacionDto
    {
        public int Id { get; set; }

        public string Nombre { get; set; } = string.Empty;

        public int CatedraId { get; set; }

        public bool EsDiagnostica { get; set; }

        public DateTime? FechaInicio { get; set; }

        public DateTime? FechaFin { get; set; }

        public string TipoEvaluacion { get; set; } = "Archivo";

        public string Instrucciones { get; set; } = string.Empty;

        public string? ArchivoDocenteUrl { get; set; }

        public string? PreguntasCuestionario { get; set; }
    }
}