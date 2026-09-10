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
                    EnableSsl = _settings.EnableSsl,
                    Timeout = 10000
                };

                if (!string.IsNullOrWhiteSpace(_settings.Username) && !string.IsNullOrWhiteSpace(_settings.Password))
                {
                    var cleanPassword = _settings.Password.Replace(" ", "").Trim();
                    client.Credentials = new NetworkCredential(_settings.Username.Trim(), cleanPassword);
                }

                var senderAddress = !string.IsNullOrWhiteSpace(_settings.SenderEmail) ? _settings.SenderEmail : "no-reply@uteq.edu.ec";
                var fromAddress = new MailAddress(senderAddress, _settings.SenderName ?? "SIGAC UTEQ");
                var toAddress = new MailAddress(toEmail);

                using var mailMessage = new MailMessage(fromAddress, toAddress)
                {
                    Subject = subject,
                    Body = htmlBody,
                    IsBodyHtml = true,
                    BodyEncoding = Encoding.UTF8,
                    SubjectEncoding = Encoding.UTF8
                };

                await client.SendMailAsync(mailMessage);
                _logger.LogInformation("Correo enviado exitosamente a {ToEmail} con asunto '{Subject}'.", toEmail, subject);
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

        public async Task<bool> SendCredentialsEmailAsync(string toEmail, string nombreCompleto, string username, string password, string rol)
        {
            var subject = "Bienvenido a SIGAC - Credenciales de Acceso";
            var htmlBody = $@"
<!DOCTYPE html>
<html lang=""es"">
<head>
  <meta charset=""UTF-8"">
  <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
  <title>Credenciales de Acceso - SIGAC</title>
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f8fafc; color: #1e293b; margin: 0; padding: 24px; }}
    .container {{ max-width: 580px; margin: 0 auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); }}
    .header {{ background: #0f172a; color: #ffffff; padding: 28px 24px; text-align: center; }}
    .header h1 {{ margin: 0; font-size: 20px; font-weight: 700; letter-spacing: 0.5px; }}
    .header p {{ margin: 4px 0 0; font-size: 13px; color: #94a3b8; }}
    .content {{ padding: 28px 24px; }}
    .greeting {{ font-size: 16px; font-weight: 600; margin-bottom: 12px; color: #0f172a; }}
    .intro {{ font-size: 14px; line-height: 1.6; color: #475569; margin-bottom: 20px; }}
    .card {{ background: #f1f5f9; border-radius: 8px; padding: 16px; margin-bottom: 20px; }}
    .card-row {{ display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #e2e8f0; font-size: 14px; }}
    .card-row:last-child {{ border-bottom: none; }}
    .label {{ font-weight: 600; color: #334155; }}
    .value {{ font-family: Consolas, Monaco, monospace; color: #0284c7; font-weight: 600; }}
    .badge {{ display: inline-block; background: #e0e7ff; color: #3730a3; padding: 2px 8px; border-radius: 4px; font-size: 12px; font-weight: 600; }}
    .warning {{ font-size: 13px; color: #64748b; line-height: 1.5; margin-bottom: 24px; }}
    .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #94a3b8; border-top: 1px solid #f1f5f9; }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>SIGAC</h1>
      <p>Sistema Integrado de Gestión Académica y Cátedras</p>
    </div>
    <div class=""content"">
      <div class=""greeting"">Estimado/a {WebUtility.HtmlEncode(nombreCompleto)},</div>
      <div class=""intro"">
        Se ha creado tu cuenta institucional en la plataforma académica SIGAC. A continuación, encontrarás los datos de acceso asignados:
      </div>
      <div class=""card"">
        <div class=""card-row"">
          <span class=""label"">Rol Institucional:</span>
          <span class=""badge"">{WebUtility.HtmlEncode(rol)}</span>
        </div>
        <div class=""card-row"">
          <span class=""label"">Usuario:</span>
          <span class=""value"">{WebUtility.HtmlEncode(username)}</span>
        </div>
        <div class=""card-row"">
          <span class=""label"">Contraseña temporal:</span>
          <span class=""value"">{WebUtility.HtmlEncode(password)}</span>
        </div>
      </div>
      <div class=""warning"">
        <strong>Importante:</strong> Por motivos de seguridad y resguardo de la información institucional, te recomendamos modificar tu contraseña temporal durante el primer inicio de sesión.
      </div>
    </div>
    <div class=""footer"">
      © {DateTime.UtcNow.Year} Universidad Técnica Estatal de Quevedo - SIGAC.<br>
      Este es un mensaje automático generado por el sistema. Por favor, no respondas a este correo.
    </div>
  </div>
</body>
</html>";

            return await SendEmailAsync(toEmail, subject, htmlBody);
        }

        public async Task<bool> SendCredentialsAsync(string toEmail, string nombreCompleto, string username, string password, string rol)
        {
            try
            {
                _logger.LogInformation("Iniciando envío inmediato de credenciales a {ToEmail} (Usuario: {Username}) mediante servidor {Server}:{Port}...", toEmail, username, _settings.Server, _settings.Port);
                var result = await SendCredentialsEmailAsync(toEmail, nombreCompleto, username, password, rol);
                if (result)
                {
                    _logger.LogInformation("Credenciales de acceso entregadas correctamente a {ToEmail}.", toEmail);
                }
                else
                {
                    _logger.LogWarning("No se pudo completar el envío de credenciales a {ToEmail}.", toEmail);
                }
                return result;
            }
            catch (SmtpException smtpEx)
            {
                _logger.LogError(smtpEx, "Fallo SMTP de Google/Red en SendCredentialsAsync hacia {ToEmail}: {Message} (StatusCode: {StatusCode})", toEmail, smtpEx.Message, smtpEx.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Excepción no controlada en SendCredentialsAsync hacia {ToEmail}: {Message}", toEmail, ex.Message);
                return false;
            }
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
  <style>
    body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif; background-color: #f8fafc; color: #1e293b; margin: 0; padding: 24px; }}
    .container {{ max-width: 580px; margin: 0 auto; background: #ffffff; border: 1px solid #e2e8f0; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 6px -1px rgba(0,0,0,0.05); }}
    .header {{ background: #0f172a; color: #ffffff; padding: 28px 24px; text-align: center; }}
    .header h1 {{ margin: 0; font-size: 20px; font-weight: 700; }}
    .header p {{ margin: 4px 0 0; font-size: 13px; color: #94a3b8; }}
    .content {{ padding: 28px 24px; }}
    .greeting {{ font-size: 16px; font-weight: 600; margin-bottom: 12px; color: #0f172a; }}
    .intro {{ font-size: 14px; line-height: 1.6; color: #475569; margin-bottom: 20px; }}
    .token-box {{ background: #eff6ff; border: 1px dashed #3b82f6; border-radius: 8px; padding: 18px; text-align: center; margin-bottom: 20px; }}
    .token-val {{ font-family: Consolas, Monaco, monospace; font-size: 20px; font-weight: 700; color: #1d4ed8; letter-spacing: 2px; }}
    .warning {{ font-size: 13px; color: #64748b; line-height: 1.5; }}
    .footer {{ text-align: center; padding: 20px; font-size: 12px; color: #94a3b8; border-top: 1px solid #f1f5f9; }}
  </style>
</head>
<body>
  <div class=""container"">
    <div class=""header"">
      <h1>SIGAC</h1>
      <p>Sistema Integrado de Gestión Académica y Cátedras</p>
    </div>
    <div class=""content"">
      <div class=""greeting"">Estimado/a {WebUtility.HtmlEncode(nombreCompleto)},</div>
      <div class=""intro"">
        Hemos recibido una solicitud para restablecer la contraseña de tu cuenta en el sistema SIGAC. Puedes utilizar la siguiente contraseña provisoria / código de verificación para ingresar:
      </div>
      <div class=""token-box"">
        <span class=""token-val"">{WebUtility.HtmlEncode(resetTokenOrTemporaryPassword)}</span>
      </div>
      <div class=""warning"">
        Si tú no solicitaste este cambio, por favor ponte en contacto con la administración del sistema o el coordinador de tu carrera inmediatamente.
      </div>
    </div>
    <div class=""footer"">
      © {DateTime.UtcNow.Year} Universidad Técnica Estatal de Quevedo - SIGAC.<br>
      Mensaje automático de seguridad.
    </div>
  </div>
</body>
</html>";

            return await SendEmailAsync(toEmail, subject, htmlBody);
        }
    }
}
