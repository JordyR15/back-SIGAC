using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace back.DTOs
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public class CreateClaseDto
    {
        public string? Nombre { get; set; }
        public int? MateriaId { get; set; }
        public int? CatedraId { get; set; }
        public string? PeriodoAcademico { get; set; }
        public int? DocenteId { get; set; }
        public ICollection<int>? EstudianteIds { get; set; }
        public ICollection<string>? CorreosEstudiantes { get; set; }
    }
}
