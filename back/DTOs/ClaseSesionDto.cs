using System;

namespace back.DTOs
{
    public class ClaseSesionDto
    {
        public int Id { get; set; }
        public int MateriaId { get; set; }
        public int? ClaseId { get; set; }
        public int DocenteId { get; set; }
        public DateTime Fecha { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public string TipoClase { get; set; }
        public string LinkVirtual { get; set; }
        public string AplicacionVirtual { get; set; }
        public string EdificioPresencial { get; set; }
        public string AulaPresencial { get; set; }
        public string PisoPresencial { get; set; }
    }
}