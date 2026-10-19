using System.Threading.Tasks;

namespace Take_Leave.Services
{
    public interface IWhatsAppService
    {
        // 'to' should be a phone number in E.164 format, e.g. +9779812345678
        Task<bool> SendWhatsAppMessageAsync(string to, string message);
    }
}