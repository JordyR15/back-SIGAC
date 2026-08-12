namespace back.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Token { get; set; }
        public string Rol { get; set; }
        public string Nombre { get; set; }    // Agregar
        public string Apellido { get; set; }  // Agregar
        public string Correo { get; set; }
    }
}
