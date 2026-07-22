namespace Take_Leave.Models.ViewModels
{
    public class DepartmentHeadDashboardViewModel
    {
        public Department Department { get; set; }
        public List<LeaveRequest> PendingLeaves { get; set; } = new();
        public int TotalStaff { get; set; }
        public int PendingLeavesCount { get; set; } // Used as PendingLeavesCount

        // Added properties to match AdminDashboardViewModel requirements
        public List<ApplicationUser> PendingRegistrations { get; set; } = new();
        public int PendingRegistrationsCount { get; set; }
        public int ApprovedLeavesThisMonth { get; set; } // Renamed from ApprovedThisMonth

        public List<LeaveRequest> RecentLeaves { get; set; } = new();
    }
}