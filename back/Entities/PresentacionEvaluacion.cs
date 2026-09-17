using System;

namespace back.Entities
{
    public class PresentacionEvaluacion
    {
        public int Id { get; set; }
        public int PresentacionId { get; set; }
        public Presentacion Presentacion { get; set; }

        // Jurado (User) que realizó la evaluación
        public int JuradoId { get; set; }
        public User Jurado { get; set; }

        public double? DominioCientifico { get; set; }
        public double? DestrezaPedagogica { get; set; }
        public double? Desenvolvimiento { get; set; }

        public double Nota { get; set; }
        public string Observaciones { get; set; } = string.Empty;
        public DateTime Fecha { get; set; } = DateTime.UtcNow;
    }
}
