namespace back.DTOs
{
    public class CreateEvaluacionDto
    {
        public double Nota { get; set; }
        public double? DominioCientifico { get; set; }
        public double? DestrezaPedagogica { get; set; }
        public double? Desenvolvimiento { get; set; }
        public string Observaciones { get; set; } = string.Empty;
    }
}
