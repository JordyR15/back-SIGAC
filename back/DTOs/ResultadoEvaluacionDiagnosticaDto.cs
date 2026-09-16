using System;
using System.Collections.Generic;

namespace back.DTOs
{
    public class ResultadoEvaluacionDiagnosticaDto
    {
        public int Id { get; set; }

        public int EvaluacionId { get; set; }

        public int EstudianteId { get; set; }

        public string NombreEstudiante { get; set; } = string.Empty;

        // Puede ser null mientras el docente no haya calificado
        public double? Calificacion { get; set; }

        public string Observacion { get; set; } = string.Empty;

        // Archivo entregado por el estudiante
        public string? ArchivoEntregaUrl { get; set; }

        // Fecha en la que el estudiante entregó
        public DateTime? FechaEntrega { get; set; }

        // Pendiente | Entregado | Calificado
        public string Estado { get; set; } = "Pendiente";

        // Para diagnósticas de tipo Cuestionario
        public string? RespuestasCuestionario { get; set; }

        public DateTime FechaRegistro { get; set; }
    }


    public class RegistrarResultadosDiagnosticosDto
    {
        public List<ResultadoDiagnosticoEntradaDto> Resultados { get; set; }
            = new();
    }


    public class ResultadoDiagnosticoEntradaDto
    {
        public int EstudianteId { get; set; }

        // Nullable porque puede existir una entrega todavía sin calificar
        public double? Calificacion { get; set; }

        public string Observacion { get; set; } = string.Empty;
    }
}