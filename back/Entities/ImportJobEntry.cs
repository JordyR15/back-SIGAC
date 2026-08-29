namespace back.Entities
{
    public class ImportJobEntry
    {
        public int Id { get; set; }
        public int ImportJobId { get; set; }
        public ImportJob ImportJob { get; set; }

        public string Nombres { get; set; }
        public string Apellidos { get; set; }
        public string Cedula { get; set; }
        public string Correo { get; set; }

        public string Username { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }
    }
}