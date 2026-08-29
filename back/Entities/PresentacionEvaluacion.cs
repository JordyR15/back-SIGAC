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

        public double Nota { get; set; }
        public string Observaciones { get; set; }
        public DateTime Fecha { get; set; }
    }
}