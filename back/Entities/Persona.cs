using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace back.Entities
{
    public class Persona
    {
        public int Id { get; set; }
        [Required]
        public string Nombre { get; set; }
        [Required]
        public string Apellido { get; set; }
        [Required]
        public string Correo { get; set; }
        public string? Cedula { get; set; }

        // Legacy / normalized storage for roles. Use comma-separated values to support multiple roles.
        [Required]
        public string Rol { get; set; } = string.Empty;

        public List<string> GetRoles()
        {
            if (string.IsNullOrWhiteSpace(Rol)) return new List<string>();
            return Rol
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(r => r.Trim())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public void SetRoles(IEnumerable<string> roles)
        {
            var normalized = (roles ?? Enumerable.Empty<string>())
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => r.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(r => r, StringComparer.OrdinalIgnoreCase)
                .ToList();

            Rol = normalized.Count == 0 ? string.Empty : string.Join(",", normalized);
        }

        // Relación uno a uno con User
        public int UserId { get; set; }
        public User User { get; set; }
    }
}
