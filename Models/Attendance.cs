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
        
        // New properties for tracking overtime and attendance issues
        public bool IsLateArrival { get; set; }
        
        public bool IsEarlyDeparture { get; set; }
        
        public bool IsOvertime { get; set; }
        
        public bool IsEarlyArrival { get; set; }
        
        public TimeSpan? LateMinutes { get; set; }
        
        public TimeSpan? EarlyDepartureMinutes { get; set; }
        
        public TimeSpan? OvertimeMinutes { get; set; }
        
        public TimeSpan? EarlyArrivalMinutes { get; set; }
        
        [StringLength(2)]
        public string AttendanceCode { get; set; } = ""; // P, A, L, E, O, EA, W, H
        
        [NotMapped]
        public string AttendanceStatus 
        { 
            get
            {
                if (IsLateArrival && IsEarlyDeparture)
                    return "Late & Left Early";
                else if (IsLateArrival)
                {
                    // Check for half-day when late
                    if (WorkDuration.HasValue && CheckInTime.HasValue && CheckOutTime.HasValue)
                    {
                        // If working less than 4 hours (typical half day threshold), consider it a half day
                        if (WorkDuration.Value.TotalHours <= 4)
                            return "Half Day";
                    }
                    return "Late";
                }
                else if (IsEarlyDeparture)
                {
                    // Check for half-day when leaving early
                    if (WorkDuration.HasValue && CheckInTime.HasValue && CheckOutTime.HasValue)
                    {
                        // If working less than 4 hours (typical half day threshold), consider it a half day
                        if (WorkDuration.Value.TotalHours <= 4)
                            return "Half Day";
                    }
                    return "Left Early";
                }
                else if (IsEarlyArrival)
                    return "Early Arrival";
                else if (IsOvertime)
                    return "Overtime";
                else if (IsComplete)
                    return "Complete";
                else if (CheckInTime.HasValue && !CheckOutTime.HasValue)
                    return "Checked In";
                else
                    return "Not Started";
            }
        }
    }
} 