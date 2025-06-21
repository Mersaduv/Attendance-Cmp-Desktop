using System.ComponentModel.DataAnnotations;

namespace AttandenceDesktop.Models
{
    public class WorkCalendar
    {
        [Key]
        public int Id { get; set; }
        
        [Required]
        public DateTime Date { get; set; }
        
        [Required, StringLength(100)]
        public string Name { get; set; }
        
        [StringLength(500)]
        public string Description { get; set; } = "";
        
        [Required]
        public CalendarEntryType EntryType { get; set; }
        
        // Indicates if this calendar entry is recurring annually (like national holidays)
        public bool IsRecurringAnnually { get; set; } = false;
    }
    
    public enum CalendarEntryType
    {
        Holiday,        // Official holiday (e.g., national holiday)
        NonWorkingDay,  // Other non-working day (e.g., company event, maintenance)
        ShortDay        // Working day with reduced hours
    }
} 