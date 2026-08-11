using System;

namespace back.DTOs
{
    public class HistorialAyudantiaDto
    {
        public int AyudantiaId { get; set; }
        public string EstadoAyudantia { get; set; }
        public int CatedraId { get; set; }
        public string NombreCatedra { get; set; }
        public string SemestreCatedra { get; set; } // Nuevo campo
        public string DocenteCatedra { get; set; } // Nombre del docente
        // Podríamos añadir más detalles si es necesario, como un resumen de bitácoras, etc.
    }
}
