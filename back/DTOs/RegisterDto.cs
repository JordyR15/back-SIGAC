using System.Collections.Generic;

namespace back.DTOs
{
    public class RegisterDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
        public string Nombre { get; set; }
        public string Apellido { get; set; }
        public string Correo { get; set; }
        public List<string> Roles { get; set; } = new();
        public string Rol { get; set; } = string.Empty; // Legacy compatibility
    }
}
