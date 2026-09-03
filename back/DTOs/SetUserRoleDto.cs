using System.Collections.Generic;

namespace back.DTOs
{
    public class SetUserRoleDto
    {
        public List<string> Roles { get; set; } = new();
        public string Rol { get; set; } = string.Empty; // Legacy compatibility
    }
}