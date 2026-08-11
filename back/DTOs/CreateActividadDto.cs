using System;

namespace back.DTOs
{
    public class CreateActividadDto
    {
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public DateTime FechaEntrega { get; set; }
        public string Tipo { get; set; }
        // El estado inicial podría ser "Pendiente" por defecto en el controlador
        public int MateriaId { get; set; }
    }
}
