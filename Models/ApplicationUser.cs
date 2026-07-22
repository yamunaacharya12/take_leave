using Microsoft.AspNetCore.Identity;

namespace Take_Leave.Models
{
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; } = string.Empty;

        // Optional: not every department/office (HOD, Admin, Reception, etc.)
        // issues employee IDs.
        public string? EmployeeId { get; set; }

        public string Designation { get; set; } = string.Empty;

        public DateTime JoiningDate { get; set; }
        public UserRole UserRole { get; set; }
        public bool IsApproved { get; set; }
        public DateTime? ApprovalDate { get; set; }
        public int AnnualLeaveBalance { get; set; } = 20;
        public int SickLeaveBalance { get; set; } = 12;
        public int EmergencyLeaveBalance { get; set; } = 5;
        public DateTime? LastLoginDate { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime RegistrationDate { get; set; }

        // NEW: used by Staff/Profile.cshtml
        public string? Address { get; set; }

        // Corrected Entity Framework Core relational properties
        // Corrected Entity Framework Core relational properties (Nullable/Optional)
        public int? DepartmentId { get; set; }
        public Department? Department { get; set; }
    }

    public enum UserRole
    {
        Staff,
        DepartmentHead,
        Admin
    }
}
