using System;

namespace back.Entities
{
    public class Convocatoria
    {
        public int Id { get; set; }
        public int CatedraId { get; set; }
        public Catedra Catedra { get; set; }
        public string Descripcion { get; set; }
        public int Plazas { get; set; }
        public string Estado { get; set; } // PendienteAprobacion, Publicada, Cerrada
        public int CreatedByUserId { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}
