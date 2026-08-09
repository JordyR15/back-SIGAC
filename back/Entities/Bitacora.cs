using System;

namespace back.Entities
{
    public class Bitacora
    {
        public int Id { get; set; }
        public int AyudantiaId { get; set; }
        public Ayudantia Ayudantia { get; set; }
        public DateTime Fecha { get; set; }
        public string ActividadesRealizadas { get; set; }
        public string EvidenciaUrl { get; set; } // Opcional: enlace a un archivo
    }
}
