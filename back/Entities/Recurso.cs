using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace back.Entities
{
    public class Recurso
    {
        public int Id { get; set; }

        public string Titulo { get; set; } = string.Empty;

        public string Descripcion { get; set; } = string.Empty;

        public string Url { get; set; } = string.Empty;

        public bool EsEsencial { get; set; }

        public int MateriaId { get; set; }

        // Se guarda físicamente en la BD como JSON.
        public string LinksJson { get; set; } = "[]";

        // Se usa normalmente desde controller/frontend.
        [NotMapped]
        public List<string> Links
        {
            get
            {
                if (string.IsNullOrWhiteSpace(LinksJson))
                {
                    return new List<string>();
                }

                try
                {
                    return JsonSerializer.Deserialize<List<string>>(LinksJson)
                           ?? new List<string>();
                }
                catch
                {
                    return new List<string>();
                }
            }

            set
            {
                LinksJson = JsonSerializer.Serialize(
                    value ?? new List<string>()
                );
            }
        }

        public Materia Materia { get; set; } = null!;
    }
}