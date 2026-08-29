using System.Collections.Generic;

namespace back.DTOs
{
    public class BulkUploadResultDto
    {
        public int CreatedCount { get; set; }
        public List<string> CreatedUsernames { get; set; } = new List<string>();
        public List<string> Errors { get; set; } = new List<string>();
    }
}