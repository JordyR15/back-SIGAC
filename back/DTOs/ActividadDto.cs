using System;

namespace back.DTOs
{
    public class ActividadDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaEntrega { get; set; }
        public string Tipo { get; set; }
        public string Estado { get; set; }
        public int MateriaId { get; set; }
    }
}