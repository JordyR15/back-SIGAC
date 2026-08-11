namespace back.DTOs
{
    public class MarkRecursoAsSeenDto
    {
        public int RecursoId { get; set; }
        // The EstudianteId will be taken from the authenticated user
    }
}