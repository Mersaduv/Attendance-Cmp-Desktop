using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AttandenceDesktop.Services
{
    public class WorkCalendarService
    {
        private readonly Func<ApplicationDbContext> _contextFactory;
        private readonly DataRefreshService _dataRefreshService;

        public WorkCalendarService(
            Func<ApplicationDbContext> contextFactory,
            DataRefreshService dataRefreshService)
        {
            _contextFactory = contextFactory;
            _dataRefreshService = dataRefreshService;
        }

        private ApplicationDbContext NewCtx() => _contextFactory();
        
        public async Task<List<WorkCalendar>> GetAllAsync()
        {
            using (var context = NewCtx())
            {
                try 
                {
                    return await context.WorkCalendars
                        .OrderBy(wc => wc.Date)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in GetAllAsync: {ex.Message}");
                    
                    // Return empty list as fallback
                    return new List<WorkCalendar>();
                }
            }
        }
        
        public async Task<WorkCalendar> GetByIdAsync(int id)
        {
            using (var context = NewCtx())
            {
                try
                {
                    return await context.WorkCalendars
                        .FirstOrDefaultAsync(wc => wc.Id == id);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in GetByIdAsync: {ex.Message}");
                    return null;
                }
            }
        }
        
        public async Task<List<WorkCalendar>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            using (var context = NewCtx())
            {
                try
                {
                    return await context.WorkCalendars
                        .Where(wc => wc.Date >= startDate && wc.Date <= endDate)
                        .OrderBy(wc => wc.Date)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in GetByDateRangeAsync: {ex.Message}");
                    return new List<WorkCalendar>();
                }
            }
        }
        
        public async Task<List<WorkCalendar>> GetByMonthYearAsync(int month, int year)
        {
            using (var context = NewCtx())
            {
                try
                {
                    return await context.WorkCalendars
                        .Where(wc => wc.Date.Month == month && wc.Date.Year == year)
                        .OrderBy(wc => wc.Date)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in GetByMonthYearAsync: {ex.Message}");
                    return new List<WorkCalendar>();
                }
            }
        }
        
        public async Task<WorkCalendar> GetByDateAsync(DateTime date)
        {
            using (var context = NewCtx())
            {
                try
                {
                    return await context.WorkCalendars
                        .Where(wc => wc.Date.Day == date.Day && 
                                    wc.Date.Month == date.Month && 
                                    (wc.Date.Year == date.Year || wc.IsRecurringAnnually))
                        .FirstOrDefaultAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in GetByDateAsync: {ex.Message}");
                    return null;
                }
            }
        }
        
        public async Task<List<WorkCalendar>> GetRecurringEntriesForMonthAsync(int month)
        {
            using (var context = NewCtx())
            {
                try
                {
                    return await context.WorkCalendars
                        .Where(wc => wc.IsRecurringAnnually && wc.Date.Month == month)
                        .OrderBy(wc => wc.Date.Day)
                        .ToListAsync();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in GetRecurringEntriesForMonthAsync: {ex.Message}");
                    return new List<WorkCalendar>();
                }
            }
        }
        
        public async Task<WorkCalendar> CreateAsync(WorkCalendar workCalendar)
        {
            using (var context = NewCtx())
            {
                context.WorkCalendars.Add(workCalendar);
                await context.SaveChangesAsync();
                
                _dataRefreshService.NotifyWorkCalendarsChanged();
                return workCalendar;
            }
        }
        
        public async Task<WorkCalendar> UpdateAsync(WorkCalendar workCalendar)
        {
            using (var context = NewCtx())
            {
                var existingEntry = await context.WorkCalendars.FindAsync(workCalendar.Id);
                if (existingEntry == null)
                {
                    throw new KeyNotFoundException($"WorkCalendar with ID {workCalendar.Id} not found");
                }
                
                existingEntry.Date = workCalendar.Date;
                existingEntry.Name = workCalendar.Name;
                existingEntry.Description = workCalendar.Description;
                existingEntry.EntryType = workCalendar.EntryType;
                existingEntry.IsRecurringAnnually = workCalendar.IsRecurringAnnually;
                
                await context.SaveChangesAsync();
                _dataRefreshService.NotifyWorkCalendarsChanged();
                
                return existingEntry;
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
                    _dataRefreshService.NotifyWorkCalendarsChanged();
                }
            }
        }
        
        public async Task<bool> IsWorkingDateAsync(DateTime date)
        {
            using (var context = NewCtx())
            {
                try
                {
                    // Check if it's a holiday or non-working day in the calendar
                    var calendarEntry = await context.WorkCalendars
                        .Where(wc => 
                            (wc.Date.Day == date.Day && wc.Date.Month == date.Month && wc.Date.Year == date.Year) || 
                            (wc.IsRecurringAnnually && wc.Date.Day == date.Day && wc.Date.Month == date.Month))
                        .FirstOrDefaultAsync();
                    
                    if (calendarEntry != null)
                    {
                        // If it's a holiday or non-working day, return false
                        return calendarEntry.EntryType == CalendarEntryType.ShortDay;
                    }
                    
                    // If no calendar entry, check if it's a weekend
                    return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in IsWorkingDateAsync: {ex.Message}");
                    
                    // Default to weekday check if there's an error
                    return date.DayOfWeek != DayOfWeek.Saturday && date.DayOfWeek != DayOfWeek.Sunday;
                }
            }
        }
        
        public async Task<bool> IsWorkingDateForEmployeeAsync(int employeeId, DateTime date, Lazy<WorkScheduleService> workScheduleService)
        {
            // Check if it's a holiday or special day
            var isHoliday = !(await IsWorkingDateAsync(date));
            if (isHoliday)
            {
                return false; // It's a holiday, so not a working day
            }
            
            // Get employee's work schedule
            var schedule = await workScheduleService.Value.GetEmployeeWorkScheduleAsync(employeeId);
            if (schedule == null)
            {
                return false; // No schedule found
            }
            
            // Check if the day of the week is a working day according to the schedule
            return schedule.IsWorkingDay(date.DayOfWeek);
        }
        
        // Overload to accept direct WorkScheduleService reference for backward compatibility
        public async Task<bool> IsWorkingDateForEmployeeAsync(int employeeId, DateTime date, WorkScheduleService workScheduleService)
        {
            // Check if it's a holiday or special day
            var isHoliday = !(await IsWorkingDateAsync(date));
            if (isHoliday)
            {
                return false; // It's a holiday, so not a working day
            }
            
            // Get employee's work schedule
            var schedule = await workScheduleService.GetEmployeeWorkScheduleAsync(employeeId);
            if (schedule == null)
            {
                return false; // No schedule found
            }
            
            // Check if the day of the week is a working day according to the schedule
            return schedule.IsWorkingDay(date.DayOfWeek);
        }
    }
} 