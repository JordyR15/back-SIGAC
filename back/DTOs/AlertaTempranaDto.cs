namespace back.DTOs
{
    public class AlertaTempranaDto
    {
        public int EstudianteId { get; set; }
        public string NombreEstudiante { get; set; }
        public double PromedioActual { get; set; }
        public bool AlertaActiva { get; set; }
    }
}
