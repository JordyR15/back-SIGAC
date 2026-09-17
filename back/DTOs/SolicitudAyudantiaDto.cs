using System;
using System.Collections.Generic;

namespace back.DTOs
{
    public class SolicitudAyudantiaDto
    {
        public int AyudantiaId { get; set; }
        public int EstudianteId { get; set; }
        public string NombreEstudiante { get; set; } = string.Empty;
        public string CorreoEstudiante { get; set; } = string.Empty;
        public string EmailEstudiante { get; set; } = string.Empty;
        public string CedulaEstudiante { get; set; } = string.Empty;
        public int CatedraId { get; set; }
        public string NombreCatedra { get; set; } = string.Empty;
        public string Estado { get; set; } = string.Empty;

        // Información detallada del Tribunal y Planificación de Reunión
        public bool TieneTribunal { get; set; }
        public int? PresentacionId { get; set; }
        public DateTime? FechaPresentacion { get; set; }
        public bool ReunionPlanificada { get; set; }
        public List<string> Jurados { get; set; } = new List<string>();
        public string EstadoTribunal { get; set; } = string.Empty;
        public string MensajeTribunal { get; set; } = string.Empty;
    }
}
