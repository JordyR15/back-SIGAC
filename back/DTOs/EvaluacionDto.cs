namespace back.DTOs
{
    public class EvaluacionDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public int CatedraId { get; set; }
        public bool EsDiagnostica { get; set; }
        public bool AdaptadaConIA { get; set; }
        public string DescripcionIA { get; set; }
    }
}
