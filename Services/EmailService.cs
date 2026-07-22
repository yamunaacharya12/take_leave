using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Take_Leave.Services
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(IConfiguration configuration, ILogger<EmailService> logger)
        {
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> SendEmailAsync(string to, string subject, string body)
        {
            try
            {
                // Fallback logging check if EmailSettings block or SenderEmail is unconfigured
                if (string.IsNullOrEmpty(_configuration["EmailSettings:SenderEmail"]))
                {
                    _logger.LogInformation("--- DEVELOPMENT MODE: EMAIL LOGGED TO CONSOLE ---");
                    _logger.LogInformation($"To: {to}");
                    _logger.LogInformation($"Subject: {subject}");
                    _logger.LogInformation($"Body: {body}");
                    _logger.LogInformation("-------------------------------------------------");
                    return true;
                }

                // Parse configurations safely, defaulting to standard secure Gmail port 587
                string smtpServer = _configuration["EmailSettings:SmtpServer"] ?? "smtp.gmail.com";
                int port = int.Parse(_configuration["EmailSettings:Port"] ?? "587");
                string senderEmail = _configuration["EmailSettings:SenderEmail"];
                string username = _configuration["EmailSettings:Username"] ?? senderEmail;
                string password = _configuration["EmailSettings:Password"];

                using var client = new SmtpClient(smtpServer, port)
                {
                    Credentials = new NetworkCredential(username, password),
                    EnableSsl = true
                };

                var mailMessage = new MailMessage
                {
                    From = new MailAddress(senderEmail, "Texas College System"),
                    Subject = subject,
                    Body = body,
                    IsBodyHtml = true
                };

                mailMessage.To.Add(to);

                // Asynchronously dispatch the email over network sockets
                await client.SendMailAsync(mailMessage);

                _logger.LogInformation($"Email successfully sent to {to}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Critical failure sending email notification to {to}");
                return false;
            }
        }
    }
}