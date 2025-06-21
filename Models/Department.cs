using System.ComponentModel.DataAnnotations;

namespace AttandenceDesktop.Models
{
    public class Department
    {
        [Key]
        public int Id { get; set; }
        
        [Required, StringLength(100)]
        public string Name { get; set; }
        
        // Navigation properties
        public ICollection<Employee> Employees { get; set; } = new List<Employee>();
        public ICollection<WorkSchedule> WorkSchedules { get; set; } = new List<WorkSchedule>();
    }
} 