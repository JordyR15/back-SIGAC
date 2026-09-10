namespace back.Entities
{
    public class Inscripcion
    {
        public int Id { get; set; }
        public int EstudianteId { get; set; }
        public User Estudiante { get; set; }
        public int? CatedraId { get; set; }
        public Catedra? Catedra { get; set; }
        public int? ClaseId { get; set; }
        public Clase? Clase { get; set; }

        // Para RF-001: Alertas Tempranas. Un promedio bajo podría generar una alerta.
        public double PromedioActual { get; set; }
        public bool AlertaRendimiento { get; set; }
    }
}
