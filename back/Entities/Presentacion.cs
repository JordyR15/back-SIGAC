using System;
using System.Collections.Generic;

namespace back.Entities
{
    public class Presentacion
    {
        public int Id { get; set; }
        public int AyudantiaId { get; set; }
        public Ayudantia Ayudantia { get; set; }
        public DateTime Fecha { get; set; }

        // Lista simple de profesores asignados (comas separadas). Si se prefiere, más tarde puede modelarse como relación.
        public string ProfesoresAsignados { get; set; }

        public int? DecanoId { get; set; }
        public User Decano { get; set; }

        public int? CoordinadorCarreraId { get; set; }
        public User CoordinadorCarrera { get; set; }

        public ICollection<PresentacionEvaluacion> Evaluaciones { get; set; } = new List<PresentacionEvaluacion>();
    }
}