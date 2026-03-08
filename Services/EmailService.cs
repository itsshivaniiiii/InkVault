using System.Text;
using System.Text.Json;

namespace InkVault.Services
{
    public interface IEmailService
    {
        Task SendOTPAsync(string email, string otp, string purpose = "Registration");
        Task SendEmailAsync(string email, string subject, string htmlBody);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;
        private readonly HttpClient _httpClient;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger, HttpClient httpClient)
        {
            _configuration = configuration;
            _logger = logger;
            _httpClient = httpClient;
        }

        public async Task SendOTPAsync(string email, string otp, string purpose = "Registration")
        {
            _logger.LogInformation("Preparing to send OTP email to {Email}", email);

            var (subject, headerTitle, headerSubtitle, bodyIntro) = purpose switch
            {
                "Login" => (
                    "Login Verification - InkVault",
                    "Login Verification",
                    "Complete your InkVault login",
                    "A one-time password has been requested to complete your login. Use the code below to verify your identity:"
                ),
                "PasswordReset" => (
                    "Password Reset - InkVault",
                    "Password Reset",
                    "Reset your InkVault password",
                    "We received a request to reset your password. Use the one-time password below to proceed:"
                ),
                _ => (
                    "Email Verification - InkVault",
                    "Email Verification",
                    "Confirm your InkVault account",
                    "Thank you for signing up with InkVault! To complete your account verification, please use the one-time password below:"
                )
            };

            var body = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif; line-height: 1.6; color: #333; background: #f5f5f5; }}
        .wrapper {{ background: #f5f5f5; padding: 40px 20px; }}
        .container {{ max-width: 600px; margin: 0 auto; background: white; border-radius: 12px; overflow: hidden; box-shadow: 0 10px 40px rgba(102, 126, 234, 0.15); }}
        .header {{ background: linear-gradient(135deg, #667eea 0%, #764ba2 100%); padding: 60px 30px; text-align: center; color: white; }}
        .header h1 {{ font-size: 36px; font-weight: 700; margin: 0; margin-bottom: 10px; letter-spacing: -0.5px; }}
        .header p {{ font-size: 16px; opacity: 0.95; margin: 0; }}
        .content {{ padding: 40px 30px; }}
        .greeting {{ font-size: 16px; color: #333; margin-bottom: 20px; line-height: 1.8; }}
        .otp-section {{ background: #f0f0f9; border: 2px solid #e0e0f0; border-radius: 12px; padding: 30px; margin: 30px 0; text-align: center; }}
        .otp-label {{ font-size: 12px; font-weight: 700; color: #667eea; letter-spacing: 1px; margin-bottom: 20px; text-transform: uppercase; }}
        .otp-code {{ font-size: 48px; font-weight: 700; color: #667eea; letter-spacing: 8px; margin: 20px 0; font-family: 'Courier New', monospace; }}
        .otp-expiry {{ font-size: 14px; color: #666; margin-top: 15px; }}
        .otp-expiry-time {{ color: #d32f2f; font-weight: 700; }}
        .warning {{ font-size: 13px; color: #999; margin-top: 10px; }}
        .how-to-use {{ background: #f9f9f9; border-left: 4px solid #667eea; padding: 20px; margin: 30px 0; border-radius: 4px; }}
        .how-to-use h3 {{ font-size: 14px; font-weight: 700; color: #333; margin-bottom: 10px; }}
        .how-to-use p {{ font-size: 14px; color: #666; margin: 0; line-height: 1.8; }}
        .footer {{ background: #f8f9fa; padding: 20px; text-align: center; font-size: 12px; color: #999; border-top: 1px solid #eee; }}
        .divider {{ height: 1px; background: #eee; margin: 30px 0; }}
    </style>
</head>
<body>
    <div class='wrapper'>
        <div class='container'>
            <!-- Header -->
            <div class='header'>
                <h1>{headerTitle}</h1>
                <p>{headerSubtitle}</p>
            </div>

            <!-- Content -->
            <div class='content'>
                <p class='greeting'>Hi there,</p>
                <p class='greeting'>{bodyIntro}</p>

                <!-- OTP Section -->
                <div class='otp-section'>
                    <div class='otp-label'>Your One-Time Password (OTP)</div>
                    <div class='otp-code'>{otp}</div>
                    <div class='otp-expiry'>
                        This code will expire in <span class='otp-expiry-time'>10 minutes</span>
                    </div>
                    <div class='warning'>Do not share this code with anyone</div>
                </div>

                <!-- How to Use -->
                <div class='how-to-use'>
                    <h3>How to use:</h3>
                    <p>Enter this one-time password (OTP) on the verification page to complete your email verification and gain full access to InkVault.</p>
                </div>

                <p style='color: #999; font-size: 13px; margin-top: 30px;'>If you didn't create this account, you can safely ignore this email.</p>

                <div class='divider'></div>

                <p style='color: #999; font-size: 12px; margin-top: 20px;'>Need help? Contact our support team at support@inkvault.com</p>
            </div>

            <!-- Footer -->
            <div class='footer'>
                <p>© 2025 InkVault. All rights reserved.</p>
                <p style='margin-top: 10px;'>This is an automated message. Please do not reply to this email address.</p>
            </div>
        </div>
    </div>
</body>
</html>";

            try
            {
                await SendEmailAsync(email, subject, body);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to send OTP email to {Email}. User can still verify via manual entry.", email);
            }
        }

        public async Task SendEmailAsync(string email, string subject, string htmlBody)
        {
            try
            {
                // Accept multiple common env var name formats users might set in Render Dashboard
                var brevoApiKey = Environment.GetEnvironmentVariable("BREVO_API_KEY")
                    ?? Environment.GetEnvironmentVariable("Brevo__ApiKey")
                    ?? Environment.GetEnvironmentVariable("BREVO__API_KEY")
                    ?? _configuration["Brevo:ApiKey"];

                var senderEmail = Environment.GetEnvironmentVariable("Brevo__SenderEmail")
                    ?? Environment.GetEnvironmentVariable("BREVO_SENDER_EMAIL")
                    ?? Environment.GetEnvironmentVariable("BREVO__SENDER_EMAIL")
                    ?? _configuration["Brevo:SenderEmail"]
                    ?? Environment.GetEnvironmentVariable("SmtpSettings__SenderEmail")
                    ?? _configuration["SmtpSettings:SenderEmail"];

                var senderName = Environment.GetEnvironmentVariable("Brevo__SenderName")
                    ?? Environment.GetEnvironmentVariable("BREVO_SENDER_NAME")
                    ?? _configuration["Brevo:SenderName"]
                    ?? "InkVault";

                Console.WriteLine($"[EMAIL] ApiKey present: {!string.IsNullOrEmpty(brevoApiKey)}, SenderEmail: {senderEmail ?? "NOT SET"}");

                if (!string.IsNullOrEmpty(brevoApiKey) && !string.IsNullOrEmpty(senderEmail))
                {
                    await SendViaBrevoAsync(email, subject, htmlBody, brevoApiKey, senderEmail, senderName);
                    return;
                }

                // Fallback to SMTP for local development
                _logger.LogInformation("Brevo not configured, attempting SMTP fallback...");
                await SendViaSmtpAsync(email, subject, htmlBody);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL] ERROR: {ex.Message}");
                _logger.LogWarning(ex, "Failed to send email to {Email}. This is non-critical.", email);
            }
        }

        private async Task SendViaBrevoAsync(string toEmail, string subject, string htmlBody,
            string apiKey, string fromEmail, string fromName)
        {
            Console.WriteLine($"[EMAIL] Sending via Brevo API to {toEmail}...");

            // Strip HTML for plain text alternative (spam filters prefer both)
            var plainText = System.Text.RegularExpressions.Regex.Replace(htmlBody, "<[^>]+>", " ");
            plainText = System.Text.RegularExpressions.Regex.Replace(plainText, @"\s{2,}", " ").Trim();

            var payload = new
            {
                sender  = new { email = fromEmail, name = fromName },
                to      = new[] { new { email = toEmail } },
                replyTo = new { email = fromEmail, name = fromName },
                subject,
                htmlContent = htmlBody,
                textContent = plainText
            };

            var request = new HttpRequestMessage(HttpMethod.Post, "https://api.brevo.com/v3/smtp/email")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
            };
            request.Headers.Add("api-key", apiKey);
            request.Headers.Add("Accept", "application/json");

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"[EMAIL] Brevo: sent successfully to {toEmail}");
                _logger.LogInformation("Email sent via Brevo to {Email}", toEmail);
            }
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"[EMAIL] Brevo error {response.StatusCode}: {errorBody}");
                throw new Exception($"Brevo API error: {response.StatusCode} - {errorBody}");
            }
        }

        private async Task SendViaSmtpAsync(string email, string subject, string htmlBody)
        {
            var smtpServer = _configuration["SmtpSettings:Server"]
                ?? Environment.GetEnvironmentVariable("SmtpSettings__Server");
            var senderEmail = _configuration["SmtpSettings:SenderEmail"]
                ?? Environment.GetEnvironmentVariable("SmtpSettings__SenderEmail");
            var senderPassword = _configuration["SmtpSettings:SenderPassword"]
                ?? Environment.GetEnvironmentVariable("SmtpSettings__SenderPassword");
            var smtpPortStr = _configuration["SmtpSettings:Port"]
                ?? Environment.GetEnvironmentVariable("SmtpSettings__Port")
                ?? "587";

            if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(senderPassword))
            {
                _logger.LogWarning("Neither SendGrid nor SMTP is configured. Email not sent.");
                return;
            }

            if (!int.TryParse(smtpPortStr, out var smtpPort)) smtpPort = 587;

            Console.WriteLine($"[EMAIL] Sending via SMTP {smtpServer}:{smtpPort} to {email}...");

            var message = new MimeKit.MimeMessage();
            message.From.Add(new MimeKit.MailboxAddress("InkVault", senderEmail));
            message.To.Add(new MimeKit.MailboxAddress("", email));
            message.Subject = subject;
            message.Body = new MimeKit.BodyBuilder { HtmlBody = htmlBody }.ToMessageBody();

            using var client = new MailKit.Net.Smtp.SmtpClient();
            client.Timeout = 30000;
            await client.ConnectAsync(smtpServer, smtpPort, MailKit.Security.SecureSocketOptions.StartTls);
            await client.AuthenticateAsync(senderEmail, senderPassword);
            await client.SendAsync(message);
            await client.DisconnectAsync(true);

            Console.WriteLine($"[EMAIL] SMTP: sent successfully to {email}");
            _logger.LogInformation("Email sent via SMTP to {Email}", email);
        }
    }
}
