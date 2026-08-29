using System;

namespace back.DTOs
{
    public class CreatePresentacionDto
    {
        public int AyudantiaId { get; set; }
        public DateTime Fecha { get; set; }
        public string ProfesoresAsignados { get; set; }
        public int? DecanoId { get; set; }
        public int? CoordinadorCarreraId { get; set; }
    }
}