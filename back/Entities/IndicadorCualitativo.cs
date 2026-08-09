namespace back.Entities
{
    public class IndicadorCualitativo
    {
        public int Id { get; set; }
        public int EstudianteId { get; set; }
        public User Estudiante { get; set; }
        public int CatedraId { get; set; }
        public Catedra Catedra { get; set; }
        public string Indicador { get; set; } // "Interés", "Participación", "Desempeño"
        public string Observacion { get; set; }
        public System.DateTime Fecha { get; set; }
    }
}
