using System;

namespace back.Entities
{
    public class ResultadoEvaluacionDiagnostica
    {
        public int Id { get; set; }

        public int EvaluacionId { get; set; }

        public Evaluacion Evaluacion { get; set; } = null!;

        public int EstudianteId { get; set; }

        public User Estudiante { get; set; } = null!;

        // Puede estar vacío mientras el docente todavía no califica
        public double? Calificacion { get; set; }

        // Retroalimentación del docente
        public string Observacion { get; set; } = string.Empty;

        // =====================================================
        // RF-004 - ENTREGA DEL ESTUDIANTE
        // =====================================================

        // Archivo enviado por el estudiante:
        // PDF, DOC, DOCX, ZIP, PNG, JPG...
        public string? ArchivoEntregaUrl { get; set; }

        // Fecha exacta en que realizó la entrega
        public DateTime? FechaEntrega { get; set; }

        // Pendiente | Entregado | Calificado
        public string Estado { get; set; } = "Pendiente";

        // Para cuando implementemos el tipo Cuestionario
        public string? RespuestasCuestionario { get; set; }

        // Fecha en la que se registró/calificó el resultado
        public DateTime FechaRegistro { get; set; } = DateTime.UtcNow;
    }
}