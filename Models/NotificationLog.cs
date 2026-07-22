namespace Take_Leave.Models
{
    public class NotificationLog
    {
        public int Id { get; set; }
        public int LeaveRequestId { get; set; }
        public LeaveRequest? LeaveRequest { get; set; }
        public string RecipientUserId { get; set; } = string.Empty;
        public ApplicationUser? RecipientUser { get; set; }
        public NotificationChannel Channel { get; set; }
        public string Subject { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public bool IsSent { get; set; }
        public DateTime? SentDate { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime CreatedDate { get; set; }
    }

    public enum NotificationChannel
    {
        Email,
        WhatsApp,
        Both
    }
}
