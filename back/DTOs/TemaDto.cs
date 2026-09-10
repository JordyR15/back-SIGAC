namespace back.DTOs
{
    public class TemaDto
    {
        public int Id { get; set; }
        public int MateriaId { get; set; }
        public string Titulo { get; set; }
        public string Descripcion { get; set; }
        public int Orden { get; set; }
    }
}
