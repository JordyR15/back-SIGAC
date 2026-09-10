using System;
using System.Collections.Generic;

namespace back.DTOs
{
    public class HistorialAyudantiaDto
    {
        public int AyudantiaId { get; set; }
        public int Id { get; set; }
        public string EstadoAyudantia { get; set; }
        public string Estado { get; set; }
        public int CatedraId { get; set; }
        public string NombreCatedra { get; set; }
        public string Catedra { get; set; }
        public string SemestreCatedra { get; set; }
        public string Semestre { get; set; }
        public string DocenteCatedra { get; set; }
        public string Docente { get; set; }
        public int HorasAcumuladas { get; set; }
        public int Horas { get; set; }
        public int TotalHoras { get; set; } = 60;
        public List<BitacoraDto> Bitacoras { get; set; } = new List<BitacoraDto>();
    }
}
