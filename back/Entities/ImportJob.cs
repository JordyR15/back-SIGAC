using System;
using System.Collections.Generic;

namespace back.Entities
{
    public class ImportJob
    {
        public int Id { get; set; }
        public int CreatedByUserId { get; set; }
        public User CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string FileName { get; set; }
        public int CreatedCount { get; set; }
        public int ErrorCount { get; set; }
        public string ResultFileName { get; set; }

        public ICollection<ImportJobEntry> Entries { get; set; } = new List<ImportJobEntry>();
    }
}