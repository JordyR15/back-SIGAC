namespace back.DTOs
{
    public class MateriaDto
    {
        public int Id { get; set; }
        public string Nombre { get; set; }
        public string Descripcion { get; set; }
        public string Codigo { get; set; }
        public int DocenteResponsableId { get; set; }

        // Opcional: si necesitas mostrar datos del docente
        public string NombreDocenteResponsable { get; set; }

        // Opcional: si necesitas incluir recursos o actividades
        public List<RecursoDto> Recursos { get; set; }
        public List<ActividadDto> Actividades { get; set; }
    }
}
