using System.Collections.Generic;

namespace back.DTOs
{
    public class UserDto
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Token { get; set; }
        public List<string> Roles { get; set; } = new();
        public string Rol { get; set; } = string.Empty; // Legacy compatibility
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Correo { get; set; }
    }
}
