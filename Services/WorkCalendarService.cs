using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Models;

namespace AttandenceDesktop.Services
{
    public class WorkCalendarService
    {
        private readonly Func<ApplicationDbContext> _contextFactory;

        public WorkCalendarService(Func<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }

        private ApplicationDbContext NewCtx() => _contextFactory();
        
        public async Task<List<WorkCalendar>> GetAllAsync()
        {
            using (var context = NewCtx())
            {
                return await context.WorkCalendars
                    .OrderBy(wc => wc.Date)
                    .ToListAsync();
            }
        }
        
        public async Task<WorkCalendar> GetByIdAsync(int id)
        {
            using (var context = NewCtx())
            {
                return await context.WorkCalendars
                    .FirstOrDefaultAsync(wc => wc.Id == id);
            }
        }
        
        public async Task<List<WorkCalendar>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            using (var context = NewCtx())
            {
                return await context.WorkCalendars
                    .Where(wc => wc.Date >= startDate && wc.Date <= endDate)
                    .OrderBy(wc => wc.Date)
                    .ToListAsync();
            }
        }
        
        public async Task<List<WorkCalendar>> GetByMonthYearAsync(int month, int year)
        {
            using (var context = NewCtx())
            {
                return await context.WorkCalendars
                    .Where(wc => wc.Date.Month == month && wc.Date.Year == year)
                    .OrderBy(wc => wc.Date)
                    .ToListAsync();
            }
        }
        
        public async Task<WorkCalendar> GetByDateAsync(DateTime date)
        {
            using (var context = NewCtx())
            {
                return await context.WorkCalendars
                    .FirstOrDefaultAsync(wc => 
                        wc.Date.Year == date.Year && 
                        wc.Date.Month == date.Month && 
                        wc.Date.Day == date.Day);
            }
        }
        
        public async Task<List<WorkCalendar>> GetRecurringEntriesForMonthAsync(int month)
        {
            // Get recurring entries (like annual holidays) that occur in the specified month
            using (var context = NewCtx())
            {
                return await context.WorkCalendars
                    .Where(wc => wc.IsRecurringAnnually && wc.Date.Month == month)
                    .ToListAsync();
            }
        }
        
        public async Task<WorkCalendar> CreateAsync(WorkCalendar workCalendar)
        {
            using (var context = NewCtx())
            {
                context.WorkCalendars.Add(workCalendar);
                await context.SaveChangesAsync();
                return workCalendar;
            }
        }
        
        public async Task<WorkCalendar> UpdateAsync(WorkCalendar workCalendar)
        {
            // Find the existing entity
            using (var context = NewCtx())
            {
                var existingCalendar = await context.WorkCalendars.FindAsync(workCalendar.Id);
                if (existingCalendar == null)
                {
                    throw new KeyNotFoundException($"Work Calendar entry with ID {workCalendar.Id} not found");
                }
                
                // Update properties
                existingCalendar.Date = workCalendar.Date;
                existingCalendar.Name = workCalendar.Name;
                existingCalendar.Description = workCalendar.Description;
                existingCalendar.EntryType = workCalendar.EntryType;
                existingCalendar.IsRecurringAnnually = workCalendar.IsRecurringAnnually;
                
                // Save changes
                await context.SaveChangesAsync();
                return existingCalendar;
            }
        }
        
        public async Task DeleteAsync(int id)
        {
            using (var context = NewCtx())
            {
                var workCalendar = await context.WorkCalendars.FindAsync(id);
                if (workCalendar != null)
                {
                    context.WorkCalendars.Remove(workCalendar);
                    await context.SaveChangesAsync();
                }
            }
        }
        
        // Method to check if a date is a holiday or non-working day
        public async Task<bool> IsWorkingDateAsync(DateTime date)
        {
            // Check for exact date match
            using (var context = NewCtx())
            {
                var calendarEntry = await GetByDateAsync(date);
                
                // If no exact match, check recurring entries
                if (calendarEntry == null)
                {
                    var recurringEntries = await context.WorkCalendars
                        .Where(wc => wc.IsRecurringAnnually && 
                               wc.Date.Month == date.Month && 
                               wc.Date.Day == date.Day)
                        .ToListAsync();
                    
                    calendarEntry = recurringEntries.FirstOrDefault();
                }
                
                // If we found a calendar entry, check its type
                if (calendarEntry != null)
                {
                    return calendarEntry.EntryType != CalendarEntryType.Holiday && 
                           calendarEntry.EntryType != CalendarEntryType.NonWorkingDay;
                }
                
                // If no calendar entry is found, it's a regular working day
                return true;
            }
        }
        
        // Method to check if a date is a holiday or non-working day for a specific employee
        public async Task<bool> IsWorkingDateForEmployeeAsync(int employeeId, DateTime date, WorkScheduleService workScheduleService)
        {
            // First check if it's a calendar holiday or non-working day
            bool isWorkingDate = await IsWorkingDateAsync(date);
            
            // If it's not a holiday or non-working day, check the employee's work schedule
            if (isWorkingDate)
            {
                return await workScheduleService.IsWorkingDayForEmployeeAsync(employeeId, date);
            }
            
            return false;
        }
    }
} 