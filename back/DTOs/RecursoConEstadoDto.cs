namespace back.DTOs
{
    public class RecursoConEstadoDto
    {
        public int Id { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public string Url { get; set; }
        public bool EsEsencial { get; set; }
        public int MateriaId { get; set; }
        public bool Visto { get; set; } // Indica si el recurso ha sido visto por el estudiante
    }
}
