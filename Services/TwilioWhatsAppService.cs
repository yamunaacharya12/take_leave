using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

namespace Take_Leave.Services
{
    public class TwilioWhatsAppService : IWhatsAppService
    {
        private readonly string _accountSid;
        private readonly string _authToken;
        private readonly string _fromNumber; // Twilio's WhatsApp sandbox/business number, e.g. "whatsapp:+14155238886"
        private readonly ILogger<TwilioWhatsAppService> _logger;
        private readonly bool _isConfigured;

        public TwilioWhatsAppService(IConfiguration configuration, ILogger<TwilioWhatsAppService> logger)
        {
            _logger = logger;
            _accountSid = configuration["Twilio:AccountSid"] ?? string.Empty;
            _authToken = configuration["Twilio:AuthToken"] ?? string.Empty;
            _fromNumber = configuration["Twilio:WhatsAppFromNumber"] ?? string.Empty;

            _isConfigured = !string.IsNullOrWhiteSpace(_accountSid)
                && !string.IsNullOrWhiteSpace(_authToken)
                && !string.IsNullOrWhiteSpace(_fromNumber);

            if (_isConfigured)
            {
                TwilioClient.Init(_accountSid, _authToken);
            }
        }

        public async Task<bool> SendWhatsAppMessageAsync(string to, string message)
        {
            if (!_isConfigured)
            {
                _logger.LogWarning("WhatsApp notification skipped — Twilio is not configured in appsettings.json (Twilio:AccountSid / Twilio:AuthToken / Twilio:WhatsAppFromNumber).");
                return false;
            }

            if (string.IsNullOrWhiteSpace(to))
            {
                _logger.LogWarning("WhatsApp notification skipped — recipient has no phone number on file.");
                return false;
            }

            try
            {
                // Twilio expects numbers prefixed with "whatsapp:", e.g. "whatsapp:+9779812345678"
                string toFormatted = to.StartsWith("whatsapp:") ? to : $"whatsapp:{to}";

                var result = await MessageResource.CreateAsync(
                    body: message,
                    from: new PhoneNumber(_fromNumber.StartsWith("whatsapp:") ? _fromNumber : $"whatsapp:{_fromNumber}"),
                    to: new PhoneNumber(toFormatted)
                );

                return result.ErrorCode == null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send WhatsApp message to {To}", to);
                return false;
            }
        }
    }
}