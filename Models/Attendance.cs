using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttandenceDesktop.Models
{
    public class Attendance
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public int EmployeeId { get; set; }
        
        [ForeignKey("EmployeeId")]
        public Employee Employee { get; set; }
        
        [Required]
        public DateTime Date { get; set; }
        
        public DateTime? CheckInTime { get; set; }
        
        public DateTime? CheckOutTime { get; set; }
        
        [StringLength(500)]
        [Required]
        public string Notes { get; set; } = "";
        
        public TimeSpan? WorkDuration { get; set; }
        
        public bool IsComplete { get; set; }
    }
} 