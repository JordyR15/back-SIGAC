namespace back.DTOs
{
    public class PostulacionAyudantiaDto
    {
        public int? CatedraId { get; set; }
        public int? MateriaId { get; set; }
        public int? EstudianteId { get; set; }
        public int? PostulanteId { get; set; }
        public int? ConvocatoriaId { get; set; }
        public string? Correo { get; set; }
        public string? Email { get; set; }
        public string? Cedula { get; set; }
        public string? Username { get; set; }
    }
}
