namespace back.DTOs
{
    public class SolicitudAyudantiaDto
    {
        public int AyudantiaId { get; set; }
        public int EstudianteId { get; set; }
        public string NombreEstudiante { get; set; }
        public int CatedraId { get; set; }
        public string NombreCatedra { get; set; }
        public string Estado { get; set; }
    }
}
