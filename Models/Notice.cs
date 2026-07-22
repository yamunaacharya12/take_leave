using System.ComponentModel.DataAnnotations;

namespace Take_Leave.Models
{
    public class Notice
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        [Required]
        public string SentBy { get; set; } = string.Empty; // Stores the Admin's name/ID who wrote it

        public DateTime SentDate { get; set; } = DateTime.Now; // Automatically logs the timestamp

        // A notice can now target multiple departments/offices (BIT, BCA, HOD, Examination, etc.).
        // An empty collection means "All Departments" — visible to everyone.
        public ICollection<Department> Departments { get; set; } = new List<Department>();
    }
}
