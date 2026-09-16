using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace back.DTOs
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
    public class CreateClaseDto
    {
        public string? Nombre { get; set; }
        public int? MateriaId { get; set; }
        public int? CatedraId
        {
            get => MateriaId;
            set
            {
                if (!MateriaId.HasValue && value.HasValue)
                    MateriaId = value.Value;
            }
        }
        public int? DocenteId { get; set; }
        public ICollection<int>? EstudianteIds { get; set; } // IDs de estudiantes a asignar a la clase
    }
}
