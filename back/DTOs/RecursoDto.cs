namespace back.DTOs
{
    public class RecursoDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public string Url { get; set; }
        public bool EsEsencial { get; set; }
        public int MateriaId { get; set; }
    }
}