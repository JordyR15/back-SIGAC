using System.Threading.Tasks;

namespace back.Services
{
    public interface IEmailService
    {
        Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody);
        Task<bool> SendCredentialsEmailAsync(string toEmail, string nombreCompleto, string username, string password, string rol);
        Task<bool> SendCredentialsAsync(string toEmail, string nombreCompleto, string username, string password, string rol);
        Task<bool> SendPasswordResetEmailAsync(string toEmail, string nombreCompleto, string resetTokenOrTemporaryPassword);
    }
}
