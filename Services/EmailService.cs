using System.Net;
using System.Net.Mail;

namespace InkVault.Services
{
    public interface IEmailService
    {
        Task SendOTPAsync(string email, string otp);
        Task SendEmailAsync(string email, string subject, string htmlBody);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendOTPAsync(string email, string otp)
        {
            try
            {
                var smtpSettings = _configuration.GetSection("SmtpSettings");
                var smtpServer = smtpSettings["Server"];

                if (string.IsNullOrEmpty(smtpServer))
                {
                    throw new InvalidOperationException("SMTP configuration is missing. Please ensure SmtpSettings are configured in appsettings.json or Environment Variables.");
                }

                var smtpPort = int.Parse(smtpSettings["Port"] ?? "587");
                var senderEmail = smtpSettings["SenderEmail"];
                var senderPassword = smtpSettings["SenderPassword"];

                if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
                {
                    throw new InvalidOperationException("SMTP credentials are missing.");
                }

                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(senderEmail, senderPassword);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(senderEmail),
                        Subject = "Your OTP for InkVault",
                        Body = $@"
                            <html>
                            <body>
                                <h2>InkVault - One-Time Password</h2>
                                <p>Your OTP is: <strong>{otp}</strong></p>
                                <p>This OTP will expire in 10 minutes.</p>
                                <p>If you didn't request this, please ignore this email.</p>
                            </body>
                            </html>",
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(email);
                    await client.SendMailAsync(mailMessage);
                }
            }
            catch (Exception ex)
            {
                // Log the exception here
                throw new InvalidOperationException($"Error sending email: {ex.Message}", ex);
            }
        }

        public async Task SendEmailAsync(string email, string subject, string htmlBody)
        {
            try
            {
                var smtpSettings = _configuration.GetSection("SmtpSettings");
                var smtpServer = smtpSettings["Server"];

                if (string.IsNullOrEmpty(smtpServer))
                {
                    throw new InvalidOperationException("SMTP configuration is missing. Please ensure SmtpSettings are configured in appsettings.json or Environment Variables.");
                }

                var smtpPort = int.Parse(smtpSettings["Port"] ?? "587");
                var senderEmail = smtpSettings["SenderEmail"];
                var senderPassword = smtpSettings["SenderPassword"];

                if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
                {
                    throw new InvalidOperationException("SMTP credentials are missing.");
                }

                using (var client = new SmtpClient(smtpServer, smtpPort))
                {
                    client.EnableSsl = true;
                    client.Credentials = new NetworkCredential(senderEmail, senderPassword);

                    var mailMessage = new MailMessage
                    {
                        From = new MailAddress(senderEmail),
                        Subject = subject,
                        Body = htmlBody,
                        IsBodyHtml = true
                    };

                    mailMessage.To.Add(email);
                    await client.SendMailAsync(mailMessage);
                }
            }
            catch (Exception ex)
            {
                // Log the exception here
                throw new InvalidOperationException($"Error sending email: {ex.Message}", ex);
            }
        }
    }
}
