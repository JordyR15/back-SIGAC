using System;
using System.Collections.Generic;

namespace back.DTOs
{
    public class CreatePresentacionDto
    {
        public int AyudantiaId { get; set; }
        public int PostulanteId { get; set; }
        public int? EstudianteId { get; set; }
        public int CatedraId { get; set; }
        public int? MateriaId { get; set; }
        public int JuradoId { get; set; }
        public List<int>? DocentesIds { get; set; }
        public List<int>? JuradoIds { get; set; } = new();
        public string? ProfesoresAsignados { get; set; } = string.Empty;
        public DateTime Fecha { get; set; }
        public DateTime FechaPresentacion { get; set; }
        public string? Tema { get; set; }
        public string? Lugar { get; set; }
        public int? DecanoId { get; set; }
        public int? CoordinadorCarreraId { get; set; }
    }
}
