namespace back.Entities
{
    public class Evaluacion
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public int CatedraId { get; set; }
        public Catedra Catedra { get; set; }
        public bool EsDiagnostica { get; set; } // Para RF-005
        public bool AdaptadaConIA { get; set; } // Para RF-003
        public string DescripcionIA { get; set; } // Para RF-003
    }
}
