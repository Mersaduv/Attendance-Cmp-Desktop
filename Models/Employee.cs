using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttandenceDesktop.Models
{
    public class Employee
    {
        [Key]
        public int Id { get; set; }
        
        [Required(ErrorMessage = "First Name is required"), StringLength(50)]
        public string FirstName { get; set; }
        
        [Required(ErrorMessage = "Last Name is required"), StringLength(50)]
        public string LastName { get; set; }
        
        [Required(ErrorMessage = "Email is required"), StringLength(100), EmailAddress]
        public string Email { get; set; }
        
        [Required(ErrorMessage = "Phone Number is required"), StringLength(20)]
        public string PhoneNumber { get; set; }
        
        [Required(ErrorMessage = "Department is required")]
        public int DepartmentId { get; set; }
        
        [ForeignKey("DepartmentId")]
        public Department Department { get; set; }
        
        // Work Schedule assignment (can be null to use department's default schedule)
        public int? WorkScheduleId { get; set; }
        
        [ForeignKey("WorkScheduleId")]
        public WorkSchedule WorkSchedule { get; set; }
        
        [Required(ErrorMessage = "Employee Code is required"), StringLength(20)]
        public string EmployeeCode { get; set; }
        
        [Required(ErrorMessage = "Position is required"), StringLength(100)]
        public string Position { get; set; }
        
        [Required(ErrorMessage = "Hire Date is required")]
        public DateTime HireDate { get; set; }
        
        // Flag to indicate if this employee has flexible working hours
        // When true, the employee is only evaluated on total hours worked, not when they work
        // This overrides any fixed schedule settings from the WorkSchedule
        public bool IsFlexibleHours { get; set; } = false;
        
        // Required work hours per day (used when IsFlexibleHours is true)
        // This defines how many hours the employee must work each day, regardless of when they work
        public double RequiredWorkHoursPerDay { get; set; } = 8.0;
        
        [NotMapped]
        public string FullName => $"{FirstName} {LastName}";
    }
} 