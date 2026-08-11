namespace back.DTOs
{
    public class AsistenciaDto
    {
        public int Id { get; set; }
        public int ClaseSesionId { get; set; }
        public int EstudianteId { get; set; }
        public bool Presente { get; set; }
    }
}