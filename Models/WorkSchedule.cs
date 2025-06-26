using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttandenceDesktop.Models
{
    public class WorkSchedule
    {
        [Key]
        public int Id { get; set; }
        
        [Required, StringLength(100)]
        public string Name { get; set; }
        
        // Flag to determine if this is a flexible schedule (total hours only) or fixed schedule (start/end time)
        public bool IsFlexibleSchedule { get; set; } = false;
        
        // For fixed schedules, these are required
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        
        // For flexible schedules, only total work hours is required
        public double TotalWorkHours { get; set; } = 8.0; // Default 8 hours
        
        // Working days (0 = Sunday, 1 = Monday, ..., 6 = Saturday)
        public bool IsWorkingDaySunday { get; set; } = false;
        public bool IsWorkingDayMonday { get; set; } = true;
        public bool IsWorkingDayTuesday { get; set; } = true;
        public bool IsWorkingDayWednesday { get; set; } = true;
        public bool IsWorkingDayThursday { get; set; } = true;
        public bool IsWorkingDayFriday { get; set; } = true;
        public bool IsWorkingDaySaturday { get; set; } = false;
        
        [StringLength(500)]
        public string Description { get; set; } = "";
        
        // Flexible time allowance in minutes (grace period for both late check-ins and early check-outs)
        public int FlexTimeAllowanceMinutes { get; set; } = 15;
        
        // Department assignment (null if schedule applies to specific employees)
        public int? DepartmentId { get; set; }
        
        [ForeignKey("DepartmentId")]
        public Department Department { get; set; }
        
        // Navigation property for employees assigned to this schedule
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
        
        // Methods to check if a specific day is a working day
        public bool IsWorkingDay(DayOfWeek dayOfWeek)
        {
            return dayOfWeek switch
            {
                DayOfWeek.Sunday => IsWorkingDaySunday,
                DayOfWeek.Monday => IsWorkingDayMonday,
                DayOfWeek.Tuesday => IsWorkingDayTuesday,
                DayOfWeek.Wednesday => IsWorkingDayWednesday,
                DayOfWeek.Thursday => IsWorkingDayThursday,
                DayOfWeek.Friday => IsWorkingDayFriday,
                DayOfWeek.Saturday => IsWorkingDaySaturday,
                _ => false
            };
        }
        
        // Calculate expected work hours for a given day
        public double CalculateExpectedWorkHours(DateTime date)
        {
            if (!IsWorkingDay(date.DayOfWeek))
            {
                return 0;
            }
            
            // If flexible schedule, return total work hours
            if (IsFlexibleSchedule)
            {
                return TotalWorkHours;
            }
            
            // Otherwise calculate from start/end time
            return (EndTime - StartTime).TotalHours;
        }
    }
} 