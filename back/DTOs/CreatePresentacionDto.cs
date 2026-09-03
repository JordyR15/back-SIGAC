using System;
using System.Collections.Generic;

namespace back.DTOs
{
    public class CreatePresentacionDto
    {
        public int AyudantiaId { get; set; }
        public DateTime Fecha { get; set; }
        public string ProfesoresAsignados { get; set; } = string.Empty;
        public List<int> JuradoIds { get; set; } = new();
        public int? DecanoId { get; set; }
        public int? CoordinadorCarreraId { get; set; }
    }
}