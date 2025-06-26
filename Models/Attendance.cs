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
        
        // Properties for flexible schedules
        public bool IsFlexibleSchedule { get; set; } = false;
        
        public double ExpectedWorkHours { get; set; } = 0;
        
        [StringLength(2)]
        public string AttendanceCode { get; set; } = ""; // P, A, L, E, O, EA, W, H
        
        [NotMapped]
        public string AttendanceStatus 
        { 
            get
            {
                // Check for half-day based on work duration percentage
                if (CheckInTime.HasValue && CheckOutTime.HasValue && WorkDuration.HasValue)
                {
                    // For flexible schedules, check if work percentage is 40-60%
                    if (IsFlexibleSchedule && ExpectedWorkHours > 0)
                    {
                        double percentage = WorkDuration.Value.TotalHours / ExpectedWorkHours * 100;
                        if (percentage >= 40 && percentage <= 60)
                        {
                            return "Half Day";
                        }
                    }
                    // For regular schedules, check if work duration is less than 60% of expected duration
                    else if (!IsFlexibleSchedule)
                    {
                        // If working less than 60% of expected time (typical half day threshold)
                        if (WorkDuration.Value.TotalHours <= 4)
                        {
                            return "Half Day";
                        }
                    }
                }
                
                if (IsLateArrival && IsEarlyDeparture)
                    return "Late & Left Early";
                else if (IsLateArrival)
                    return "Late";
                else if (IsEarlyDeparture)
                    return "Left Early";
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
        
        // Method to get the percentage of expected work hours completed (for flexible schedules)
        [NotMapped]
        public double WorkHoursPercentage
        {
            get
            {
                if (ExpectedWorkHours <= 0 || !WorkDuration.HasValue) return 0;
                return WorkDuration.Value.TotalHours / ExpectedWorkHours * 100;
            }
        }
    }
} 