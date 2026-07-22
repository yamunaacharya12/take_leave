namespace Take_Leave.Models.ViewModels
{
    public class AdminDashboardViewModel
    {
        public List<ApplicationUser> PendingRegistrations { get; set; } = new();
        public List<LeaveRequest> PendingLeaves { get; set; } = new();
        public int TotalStaff { get; set; }
        public int PendingLeavesCount { get; set; }
        public int PendingRegistrationsCount { get; set; }
        public int TotalLeavesThisMonth { get; set; }
        public int ApprovedLeavesThisMonth { get; set; }
    }
}
