using System;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace back.Services
{
    public class EmailService : IEmailService
    {
        private readonly SmtpSettings _settings;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IOptions<SmtpSettings> settings, IConfiguration configuration, ILogger<EmailService> logger)
        {
            _logger = logger;
            _settings = settings?.Value ?? new SmtpSettings();

            // Fallback reading directly from IConfiguration if SmtpSettings wasn't bound via IOptions
            if (string.IsNullOrWhiteSpace(_settings.Server))
            {
                _settings.Server = configuration["SmtpSettings:Server"] ?? configuration["Smtp:Host"] ?? string.Empty;
                if (int.TryParse(configuration["SmtpSettings:Port"] ?? configuration["Smtp:Port"], out var p))
                {
                    _settings.Port = p;
                }
                _settings.SenderEmail = configuration["SmtpSettings:SenderEmail"] ?? configuration["Smtp:From"] ?? "no-reply@uteq.edu.ec";
                _settings.SenderName = configuration["SmtpSettings:SenderName"] ?? "SIGAC UTEQ";
                _settings.Username = configuration["SmtpSettings:Username"] ?? configuration["Smtp:User"] ?? string.Empty;
                _settings.Password = configuration["SmtpSettings:Password"] ?? configuration["Smtp:Pass"] ?? string.Empty;
                if (bool.TryParse(configuration["SmtpSettings:EnableSsl"] ?? configuration["Smtp:EnableSsl"], out var ssl))
                {
                    _settings.EnableSsl = ssl;
                }
            }
        }

        public async Task<bool> SendEmailAsync(string toEmail, string subject, string htmlBody)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                _logger.LogWarning("Intento de enviar correo con destinatario vacío.");
                return false;
            }

            // Si no hay un servidor SMTP configurado (entorno local o de pruebas), simular el envío
            if (string.IsNullOrWhiteSpace(_settings.Server))
            {
                _logger.LogInformation("[SMTP Simulado/Desarrollo] Correo a '{ToEmail}' | Asunto: '{Subject}'", toEmail, subject);
                return true;
            }

            try
            {
                using var client = new SmtpClient(_settings.Server, _settings.Port)
                {
                    EnableSsl = true,
                    UseDefaultCredentials = false, // OBLIGATORIO para Gmail antes de asignar credenciales
                    Credentials = new NetworkCredential(_settings.Username, _settings.Password.Trim()),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    Timeout = 15000
                };

                var senderAddress = !string.IsNullOrWhiteSpace(_settings.SenderEmail) ? _settings.SenderEmail : "no-reply@uteq.edu.ec";
                var senderDisplayName = !string.IsNullOrWhiteSpace(_settings.SenderName) ? _settings.SenderName : "SIGAC UTEQ";

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderAddress, senderDisplayName, Encoding.UTF8),
                    Subject = subject,
                    SubjectEncoding = Encoding.UTF8,
                    Body = htmlBody,
                    BodyEncoding = Encoding.UTF8,
                    HeadersEncoding = Encoding.UTF8,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(new MailAddress(toEmail));

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation(">>> [SMTP ÉXITO] Correo de credenciales enviado a: {To}", toEmail);
                return true;
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError(smtpEx, "Fallo de conexión o autenticación SMTP de Google ({Server}:{Port}) al enviar correo a {ToEmail}: {Message} [StatusCode: {StatusCode}]", _settings.Server, _settings.Port, toEmail, smtpEx.Message, smtpEx.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al enviar correo electrónico SMTP a {ToEmail}: {Message}", toEmail, ex.Message);
                return false;
            }
        }

        public async Task<bool> SendCredentialsAsync(string toEmail, string fullName, string username, string tempPassword, string role)
        {
            string subject = $"Acceso Institucional SIGAC - Credenciales de {role}";
            string body = $@"
<!DOCTYPE html>
<html>
<head><meta charset='utf-8'></head>
<body style='font-family: Arial, sans-serif; background-color: #f8fafc; padding: 30px; margin: 0;'>
  <div style='max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.08); border: 1px solid #e2e8f0;'>
    <div style='background: linear-gradient(135deg, #1e3a8a, #047857); padding: 25px; text-align: center; color: white;'>
      <h1 style='margin: 0; font-size: 20px; font-weight: bold; letter-spacing: 0.5px;'>UNIVERSIDAD TÉCNICA ESTATAL DE QUEVEDO</h1>
      <p style='margin: 6px 0 0 0; font-size: 13px; opacity: 0.9;'>Sistema Integral de Gestión Académica y Ayudantías (SIGAC)</p>
    </div>
    <div style='padding: 30px; color: #1e293b; line-height: 1.6;'>
      <h2 style='color: #0f172a; font-size: 18px; margin-top: 0;'>Estimado(a) {fullName},</h2>
      <p>Se ha registrado tu postulación y cuenta institucional en la plataforma con el rol de <strong>{role}</strong>.</p>

      <div style='background-color: #f1f5f9; border-left: 4px solid #047857; padding: 20px; border-radius: 6px; margin: 25px 0;'>
        <h3 style='margin: 0 0 12px 0; color: #047857; font-size: 15px;'>Tus Credenciales de Acceso:</h3>
        <p style='margin: 6px 0;'><strong>Usuario:</strong> <code style='font-size: 15px; color: #1e3a8a; background: #e2e8f0; padding: 2px 6px; border-radius: 4px;'>{username}</code></p>
        <p style='margin: 6px 0;'><strong>Contraseña Temporal:</strong> <code style='font-size: 15px; color: #b91c1c; background: #fee2e2; padding: 2px 6px; border-radius: 4px; font-weight: bold;'>{tempPassword}</code></p>
        <p style='margin: 6px 0;'><strong>Rol Asignado:</strong> {role}</p>
      </div>

      <div style='text-align: center; margin: 30px 0;'>
        <a href='http://localhost:4200/login' style='background-color: #1e3a8a; color: white; padding: 12px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 14px; display: inline-block;'>Iniciar Sesión en SIGAC</a>
      </div>

      <p style='font-size: 12px; color: #64748b;'>Por razones de seguridad, se recomienda cambiar tu contraseña temporal tras el primer ingreso.</p>
    </div>
    <div style='background-color: #f8fafc; padding: 15px 30px; text-align: center; border-top: 1px solid #e2e8f0; font-size: 11px; color: #94a3b8;'>
      Comisión de Ayudantías de Cátedra • Universidad Técnica Estatal de Quevedo
    </div>
  </div>
</body>
</html>";

            return await SendEmailAsync(toEmail, subject, body);
        }

        public async Task<bool> SendCredentialsEmailAsync(string toEmail, string nombreCompleto, string username, string password, string rol)
        {
            return await SendCredentialsAsync(toEmail, nombreCompleto, username, password, rol);
        }

        public async Task<bool> SendPasswordResetEmailAsync(string toEmail, string nombreCompleto, string resetTokenOrTemporaryPassword)
        {
            var subject = "SIGAC - Restablecimiento de Contraseña";
            var htmlBody = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Restablecimiento de Contraseña - SIGAC</title>
</head>
<body style='font-family: Arial, sans-serif; background-color: #f8fafc; padding: 30px; margin: 0;'>
  <div style='max-width: 600px; margin: 0 auto; background: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 12px rgba(0,0,0,0.08); border: 1px solid #e2e8f0;'>
    <div style='background: linear-gradient(135deg, #1e3a8a, #047857); padding: 25px; text-align: center; color: white;'>
      <h1 style='margin: 0; font-size: 20px; font-weight: bold; letter-spacing: 0.5px;'>UNIVERSIDAD TÉCNICA ESTATAL DE QUEVEDO</h1>
      <p style='margin: 6px 0 0 0; font-size: 13px; opacity: 0.9;'>Sistema Integral de Gestión Académica y Ayudantías (SIGAC)</p>
    </div>
    <div style='padding: 30px; color: #1e293b; line-height: 1.6;'>
      <h2 style='color: #0f172a; font-size: 18px; margin-top: 0;'>Estimado(a) {WebUtility.HtmlEncode(nombreCompleto)},</h2>
      <p>Hemos recibido una solicitud para restablecer la contraseña de tu cuenta en el sistema SIGAC. Puedes utilizar la siguiente contraseña provisoria / código de verificación para ingresar:</p>
      <div style='background: #eff6ff; border: 1px dashed #3b82f6; border-radius: 8px; padding: 18px; text-align: center; margin: 20px 0;'>
        <span style='font-family: Consolas, Monaco, monospace; font-size: 20px; font-weight: 700; color: #1d4ed8; letter-spacing: 2px;'>{WebUtility.HtmlEncode(resetTokenOrTemporaryPassword)}</span>
      </div>
      <div style='text-align: center; margin: 30px 0;'>
        <a href='http://localhost:4200/login' style='background-color: #1e3a8a; color: white; padding: 12px 28px; text-decoration: none; border-radius: 8px; font-weight: bold; font-size: 14px; display: inline-block;'>Iniciar Sesión en SIGAC</a>
      </div>
      <p style='font-size: 12px; color: #64748b;'>Si tú no solicitaste este cambio, por favor ponte en contacto con la administración del sistema o el coordinador de tu carrera inmediatamente.</p>
    </div>
    <div style='background-color: #f8fafc; padding: 15px 30px; text-align: center; border-top: 1px solid #e2e8f0; font-size: 11px; color: #94a3b8;'>
      © {DateTime.UtcNow.Year} Universidad Técnica Estatal de Quevedo • SIGAC
    </div>
  </div>
</body>
</html>";

            return await SendEmailAsync(toEmail, subject, htmlBody);
        }
    }
}
