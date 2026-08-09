using System;

namespace back.Entities
{
    public class ActividadAyudantia
    {
        public int Id { get; set; }
        public int AyudantiaId { get; set; }
        public Ayudantia Ayudantia { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaPlanificada { get; set; }
        public bool Completada { get; set; }
    }
}
