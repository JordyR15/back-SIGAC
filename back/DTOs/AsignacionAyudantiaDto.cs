namespace back.DTOs
{
    public class AsignacionAyudantiaDto
    {
        public int AyudantiaId { get; set; }
        public int EstudianteId { get; set; }
        public int CatedraId { get; set; }
        public int HorasAsignadas { get; set; } = 60;
    }
}
