using System;
using System.Collections.Generic;

namespace back.Entities
{
    public class ClaseSesion
    {
        public int Id { get; set; }
        public int MateriaId { get; set; } // The general Materia
        public int? ClaseId { get; set; } // Specific instance of a class (e.g., "Modelamiento 2023-2")
        public int DocenteId { get; set; } // FK to User (Docente)
        public DateTime Fecha { get; set; }
        public TimeSpan HoraInicio { get; set; }
        public TimeSpan HoraFin { get; set; }
        public string TipoClase { get; set; } // "Virtual", "Presencial"
        public string LinkVirtual { get; set; }
        public string AplicacionVirtual { get; set; } // e.g., "Zoom", "Meet"
        public string EdificioPresencial { get; set; }
        public string AulaPresencial { get; set; }
        public string PisoPresencial { get; set; }

        // Navigation properties
        public Materia Materia { get; set; }
        public Clase Clase { get; set; }
        public User Docente { get; set; }
        public ICollection<Asistencia> Asistencias { get; set; }
    }
}
