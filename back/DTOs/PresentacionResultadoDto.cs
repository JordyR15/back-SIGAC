namespace back.DTOs
{
    public class PresentacionResultadoDto
    {
        public double PromedioEvaluaciones { get; set; }
        public double PromedioEstudiante { get; set; }
        public double PromedioCatedra { get; set; }
        public bool EstudianteSuperiorPromedioCatedra { get; set; }
        public bool EstudianteSuperiorPromedioPresentacion { get; set; }
        public int EvaluacionesCount { get; set; }
    }
}