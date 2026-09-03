namespace back.DTOs
{
    public class CalificarEntregaDto
    {
        public int EntregaId { get; set; }
        public decimal Calificacion { get; set; }
        public string Retroalimentacion { get; set; } = string.Empty;
    }
}
