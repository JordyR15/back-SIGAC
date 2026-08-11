namespace back.DTOs
{
    public class UserDto
    {
        public int Id { get; set; } // Añadido
        public string Username { get; set; }
        public string Token { get; set; }
        public string Rol { get; set; }
    }
}
