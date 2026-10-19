using System.Threading.Tasks;
using Take_Leave.Models;

namespace Take_Leave.Services
{
    public interface INotificationService
    {
        // Sends a raw WhatsApp message directly — used internally by the
        // business-workflow methods below, but also exposed for direct
        // admin-panel triggers if ever needed.
        Task SendWhatsAppAsync(string mobileNumber, string message);

        // Your existing business workflow methods
        Task NotifyLeaveApplicationAsync(LeaveRequest leaveRequest);
        Task NotifyDepartmentHeadAsync(LeaveRequest leaveRequest);
        Task NotifyAdminAsync(LeaveRequest leaveRequest);
        Task NotifyStatusChangeAsync(LeaveRequest leaveRequest);
        Task NotifyLeaveBalanceAlertAsync(ApplicationUser user, LeaveType leaveType);
        Task SendDailyDigestAsync();
        Task SendWeeklyReportAsync();
    }
}