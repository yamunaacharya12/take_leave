namespace Take_Leave.Models.ViewModels
{
    public class LeaveReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public int TotalApplications { get; set; }
        public int ApprovedApplications { get; set; }
        public int RejectedApplications { get; set; }
        public int PendingApplications { get; set; }
        public List<DepartmentLeaveSummary> LeavesByDepartment { get; set; }
        public List<LeaveTypeSummary> LeavesByType { get; set; }
    }

    public class DepartmentLeaveSummary
    {
        public string Department { get; set; }
        public int Count { get; set; }
    }

    public class LeaveTypeSummary
    {
        public LeaveType LeaveType { get; set; }
        public int Count { get; set; }
    }
}
