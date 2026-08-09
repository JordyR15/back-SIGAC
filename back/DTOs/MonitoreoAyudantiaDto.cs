using System.Collections.Generic;

namespace back.DTOs
{
    public class MonitoreoAyudantiaDto
    {
        public int AyudantiaId { get; set; }
        public string NombreAyudante { get; set; }
        public List<ActividadAyudantiaDto> Planificacion { get; set; } = new List<ActividadAyudantiaDto>();
        public List<BitacoraDto> Bitacoras { get; set; } = new List<BitacoraDto>();
    }

    public class BitacoraDto
    {
        public int Id { get; set; }
        public DateTime Fecha { get; set; }
        public string ActividadesRealizadas { get; set; }
        public string EvidenciaUrl { get; set; }
    }
}
