namespace back.Entities
{
    public class Recurso
    {
        public int Id { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public string Url { get; set; } // URL al archivo o enlace externo
        public bool EsEsencial { get; set; } // Para la barra lateral
        public int MateriaId { get; set; }

        // Navigation property
        public Materia Materia { get; set; }
    }
}