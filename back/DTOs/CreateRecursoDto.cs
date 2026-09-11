using System.Collections.Generic;

namespace back.DTOs
{
    public class CreateRecursoDto
    {
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public string Url { get; set; }
        public bool EsEsencial { get; set; }
        public int MateriaId { get; set; }
        public List<string> Links { get; set; } = new List<string>();
    }
}