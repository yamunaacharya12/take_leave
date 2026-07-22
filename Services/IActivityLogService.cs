using System.Threading.Tasks;
using Take_Leave.Models;

namespace Take_Leave.Services
{
    public interface IActivityLogService
    {
        Task LogAsync(
            string category,
            string message,
            ActivityLevel level = ActivityLevel.Info,
            string performedByName = "Admin",
            string? performedByUserId = null,
            int? leaveRequestId = null,
            string? recipientUserId = null);
    }
}
