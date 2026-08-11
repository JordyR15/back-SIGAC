namespace back.DTOs
{
    public class CreateAsistenciaDto
    {
        public int ClaseSesionId { get; set; }
        public int EstudianteId { get; set; }
        public bool Presente { get; set; }
    }
}
