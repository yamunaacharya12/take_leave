using System;

namespace Take_Leave.Models
{
    // Severity/level shown in the "All Levels" filter dropdown.
    public enum ActivityLevel
    {
        Info,
        Success,
        Warning,
        Error
    }

    // General-purpose audit/activity log for the admin feed.
    // Separate from NotificationLog, which only tracks email/WhatsApp delivery
    // for leave requests. This table can log ANY event: leave actions,
    // staff registration, security events, notices, system events, etc.
    public class ActivityLog
    {
        public int Id { get; set; }

        // "Leave", "Staff", "Security", "Notice", "System"
        public string Category { get; set; } = string.Empty;

        public ActivityLevel Level { get; set; } = ActivityLevel.Info;

        // Short, human-readable one-liner, e.g.
        // "Leave request by Sita Kunwar (Casual Leave) has been approved."
        public string Message { get; set; } = string.Empty;

        // Display text for "by Admin" / "by System" / "by Alisha Nepal"
        public string PerformedByName { get; set; } = "System";

        // Optional link back to the actual user account that performed the action
        public string? PerformedByUserId { get; set; }
        public ApplicationUser? PerformedByUser { get; set; }

        // Optional links to related records, purely for the "Re: ..." / "To: ..." meta line
        public int? LeaveRequestId { get; set; }
        public LeaveRequest? LeaveRequest { get; set; }

        public string? RecipientUserId { get; set; }
        public ApplicationUser? RecipientUser { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
