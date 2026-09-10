namespace back.Services
{
    public class SmtpSettings
    {
        public string Server { get; set; } = string.Empty;
        public int Port { get; set; } = 587;
        public string SenderName { get; set; } = "SIGAC UTEQ";
        public string SenderEmail { get; set; } = "no-reply@uteq.edu.ec";
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public bool EnableSsl { get; set; } = true;
    }
}
