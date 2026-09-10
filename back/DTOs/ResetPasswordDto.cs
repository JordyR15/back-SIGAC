using System.ComponentModel.DataAnnotations;

namespace back.DTOs
{
    public class ResetPasswordDto
    {
        [Required]
        public string EmailOrUsername { get; set; } = string.Empty;

        [Required]
        public string NewPassword { get; set; } = string.Empty;
    }
}
