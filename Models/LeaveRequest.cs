namespace Take_Leave.Models
{
    public class LeaveRequest
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        public string DepartmentId { get; set; } = string.Empty;
        public LeaveType LeaveType { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Reason { get; set; } = string.Empty;
        public string? DocumentUrl { get; set; }
        public LeaveStatus Status { get; set; }
        public string? DepartmentHeadComments { get; set; }
        public string? AdminComments { get; set; }
        public DateTime RequestDate { get; set; }
        public DateTime? DepartmentHeadApprovalDate { get; set; }
        public DateTime? AdminApprovalDate { get; set; }
        public int TotalDays { get; set; }
        public bool IsHalfDay { get; set; }
        public string? HalfDaySession { get; set; } // Morning/Evening
    }

    public enum LeaveType
    {
        Annual,
        Sick,
        Emergency,
        Unpaid,
        Study,
        Maternity,
        Paternity,
        Marriage
    }

    public enum LeaveStatus
    {
        Pending,
        ApprovedByDepartmentHead,
        Approved,
        RejectedByDepartmentHead,
        Rejected
    }
}

