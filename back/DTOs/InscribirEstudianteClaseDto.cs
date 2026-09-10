using System.Collections.Generic;

namespace back.DTOs
{
    public class InscribirEstudianteClaseDto
    {
        public long? EstudianteId { get; set; }
        public long? Id { get; set; }
        public List<long>? EstudianteIds { get; set; }
        public string? Nombre { get; set; }
        public string? Nombres { get; set; }
        public string? Apellido { get; set; }
        public string? Apellidos { get; set; }
        public string? Correo { get; set; }
        public string? Email { get; set; }
        public string? Cedula { get; set; }
        public string? Username { get; set; }
    }
}
