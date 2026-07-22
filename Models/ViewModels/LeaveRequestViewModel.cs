using System.ComponentModel.DataAnnotations;

namespace Take_Leave.Models.ViewModels
{
    public class LeaveRequestViewModel
    {
        [Required(ErrorMessage = "Leave type is required")]
        [Display(Name = "Leave Type")]
        public LeaveType LeaveType { get; set; }

        [Required(ErrorMessage = "Start date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "Start Date")]
        public DateTime StartDate { get; set; }

        [Required(ErrorMessage = "End date is required")]
        [DataType(DataType.Date)]
        [Display(Name = "End Date")]
        [CustomValidation(typeof(LeaveRequestViewModel), nameof(ValidateEndDate))]
        public DateTime EndDate { get; set; }

        [Required(ErrorMessage = "Reason is required")]
        [StringLength(500, MinimumLength = 5, ErrorMessage = "Reason must be between 5 and 500 characters")]
        [Display(Name = "Reason for Leave")]
        public string Reason { get; set; } = string.Empty;

        // Calculate total days - this will work in the controller
        public int TotalDays => (EndDate - StartDate).Days + 1;

        // Custom validation for end date
        public static ValidationResult? ValidateEndDate(DateTime endDate, ValidationContext context)
        {
            var instance = (LeaveRequestViewModel)context.ObjectInstance;
            if (endDate < instance.StartDate)
            {
                return new ValidationResult("End date must be greater than or equal to start date");
            }

            // Optional: Limit leave duration (e.g., max 30 days)
            var totalDays = (endDate - instance.StartDate).Days + 1;
            if (totalDays > 30)
            {
                return new ValidationResult("Leave cannot exceed 30 consecutive days");
            }

            return ValidationResult.Success;
        }
    }
}