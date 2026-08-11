using System;

namespace back.DTOs
{
    public class CreateClaseSesionDto
    {
        public int MateriaId { get; set; }
        public int? ClaseId { get; set; } // Opcional, si la sesión pertenece a una instancia de Clase específica
        public int DocenteId { get; set; }
        public DateTime Fecha { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public string TipoClase { get; set; } // "Virtual", "Presencial"
        public string LinkVirtual { get; set; }
        public string AplicacionVirtual { get; set; }
        public string EdificioPresencial { get; set; }
        public string AulaPresencial { get; set; }
        public string PisoPresencial { get; set; }
    }
}
