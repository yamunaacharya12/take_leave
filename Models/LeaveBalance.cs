namespace Take_Leave.Models
{
    public class LeaveBalance
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public ApplicationUser? User { get; set; }
        public int Year { get; set; }
        public LeaveType LeaveType { get; set; }
        public int TotalQuota { get; set; }
        public int UsedQuota { get; set; }
        public int RemainingQuota => TotalQuota - UsedQuota;
        public int CarriedOver { get; set; }
    }
}
