using System;

namespace back.DTOs
{
    public class ActividadAyudantiaDto
    {
        public int Id { get; set; }
        public int AyudantiaId { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaPlanificada { get; set; }
        public bool Completada { get; set; }
    }
}
