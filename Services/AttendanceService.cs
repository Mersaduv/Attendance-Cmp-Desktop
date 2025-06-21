using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Models;

namespace AttandenceDesktop.Services
{
    public class AttendanceService
    {
        private readonly Func<ApplicationDbContext> _contextFactory;
        private readonly WorkScheduleService _workScheduleService;
        private readonly WorkCalendarService _workCalendarService;
        
        public AttendanceService(
            Func<ApplicationDbContext> contextFactory,
            WorkScheduleService workScheduleService,
            WorkCalendarService workCalendarService)
        {
            _contextFactory = contextFactory;
            _workScheduleService = workScheduleService;
            _workCalendarService = workCalendarService;
        }
        
        private ApplicationDbContext NewCtx() => _contextFactory();
        
        public async Task<List<Attendance>> GetAllAsync()
        {
            using var ctx = NewCtx();
            return await ctx.Attendances
                .Include(a => a.Employee)
                .ThenInclude(e => e.Department)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
        }
        
        public async Task<List<Attendance>> GetByDateRangeAsync(DateTime startDate, DateTime endDate)
        {
            using var ctx = NewCtx();
            return await ctx.Attendances
                .Include(a => a.Employee)
                .ThenInclude(e => e.Department)
                .Where(a => a.Date >= startDate && a.Date <= endDate)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
        }
        
        public async Task<List<Attendance>> GetByEmployeeAsync(int employeeId)
        {
            using var ctx = NewCtx();
            return await ctx.Attendances
                .Include(a => a.Employee)
                .Where(a => a.EmployeeId == employeeId)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
        }
        
        public async Task<List<Attendance>> GetByEmployeeAndDateRangeAsync(int employeeId, DateTime startDate, DateTime endDate)
        {
            using var ctx = NewCtx();
            return await ctx.Attendances
                .Include(a => a.Employee)
                .Where(a => a.EmployeeId == employeeId && a.Date >= startDate && a.Date <= endDate)
                .OrderByDescending(a => a.Date)
                .ToListAsync();
        }
        
        public async Task<Attendance> GetByIdAsync(int id)
        {
            using var ctx = NewCtx();
            return await ctx.Attendances
                .Include(a => a.Employee)
                .ThenInclude(e => e.Department)
                .FirstOrDefaultAsync(a => a.Id == id);
        }
        
        public async Task<Attendance> GetByEmployeeAndDateAsync(int employeeId, DateTime date)
        {
            using var ctx = NewCtx();
            return await ctx.Attendances
                .FirstOrDefaultAsync(a => 
                    a.EmployeeId == employeeId && 
                    a.Date.Year == date.Year && 
                    a.Date.Month == date.Month && 
                    a.Date.Day == date.Day);
        }
        
        public async Task<Attendance> CheckInAsync(int employeeId)
        {
            var today = DateTime.Today;
            var now = DateTime.Now;
            
            var attendance = await GetByEmployeeAndDateAsync(employeeId, today);
            
            if (attendance == null)
            {
                attendance = new Attendance
                {
                    EmployeeId = employeeId,
                    Date = today,
                    CheckInTime = now,
                    Notes = ""
                };
                
                using var addCtx = NewCtx();
                addCtx.Attendances.Add(attendance);
                await addCtx.SaveChangesAsync();
            }
            else if (!attendance.CheckInTime.HasValue)
            {
                // Find the existing entity to update it
                using var updateCtx = NewCtx();
                var existingAttendance = await updateCtx.Attendances.FindAsync(attendance.Id);
                if (existingAttendance != null)
                {
                    existingAttendance.CheckInTime = now;
                    await updateCtx.SaveChangesAsync();
                }
            }
            
            using var refreshCtx = NewCtx();
            attendance = await refreshCtx.Attendances.FindAsync(attendance.Id);
            
            return attendance;
        }
        
        public async Task<Attendance> CheckOutAsync(int employeeId)
        {
            var today = DateTime.Today;
            var now = DateTime.Now;
            
            var attendance = await GetByEmployeeAndDateAsync(employeeId, today);
            
            if (attendance != null && attendance.CheckInTime.HasValue)
            {
                // Find the existing entity to update it
                using var updateCtx2 = NewCtx();
                var existingAttendance = await updateCtx2.Attendances.FindAsync(attendance.Id);
                if (existingAttendance != null)
                {
                    existingAttendance.CheckOutTime = now;
                    
                    // Calculate work duration
                    if (existingAttendance.CheckInTime.HasValue)
                    {
                        existingAttendance.WorkDuration = now - existingAttendance.CheckInTime.Value;
                    }
                    
                    // Update IsComplete flag
                    existingAttendance.IsComplete = true;
                    
                    await updateCtx2.SaveChangesAsync();
                    
                    // Refresh from database
                    using var refreshCtx2 = NewCtx();
                    attendance = await refreshCtx2.Attendances.FindAsync(attendance.Id);
                }
            }
            
            return attendance;
        }
        
        public async Task<Attendance> UpdateAsync(Attendance attendance)
        {
            try
            {
                // Find the existing entity from the database
                using var ctx = NewCtx();
                var existingAttendance = await ctx.Attendances.FindAsync(attendance.Id);
                
                if (existingAttendance == null)
                {
                    throw new Exception($"Attendance record with ID {attendance.Id} not found.");
                }
                
                // Update properties
                existingAttendance.EmployeeId = attendance.EmployeeId;
                existingAttendance.Date = attendance.Date;
                existingAttendance.CheckInTime = attendance.CheckInTime;
                existingAttendance.CheckOutTime = attendance.CheckOutTime;
                existingAttendance.Notes = attendance.Notes;
                
                // Calculate work duration if both check-in and check-out times are available
                if (existingAttendance.CheckInTime.HasValue && existingAttendance.CheckOutTime.HasValue)
                {
                    existingAttendance.WorkDuration = existingAttendance.CheckOutTime.Value - 
                                                    existingAttendance.CheckInTime.Value;
                }
                else
                {
                    existingAttendance.WorkDuration = null;
                }
                
                // Update IsComplete flag
                existingAttendance.IsComplete = existingAttendance.CheckInTime.HasValue && 
                                              existingAttendance.CheckOutTime.HasValue;
                
                // Save changes
                using var retryCtx = NewCtx();
                retryCtx.ChangeTracker.Clear();
                retryCtx.Attendances.Update(attendance);
                await retryCtx.SaveChangesAsync();
                
                return existingAttendance;
            }
            catch (Exception)
            {
                // If there's still an issue, try detaching and then updating
                using var retryCtx = NewCtx();
                retryCtx.ChangeTracker.Clear();
                retryCtx.Attendances.Update(attendance);
                await retryCtx.SaveChangesAsync();
                
                return attendance;
            }
        }
        
        public async Task DeleteAsync(int id)
        {
            using var ctx = NewCtx();
            var attendance = await ctx.Attendances.FindAsync(id);
            if (attendance != null)
            {
                ctx.Attendances.Remove(attendance);
                await ctx.SaveChangesAsync();
            }
        }
        
        // Method to check if an employee is late based on their work schedule
        public async Task<bool> IsLateCheckInAsync(int employeeId, DateTime checkInTime)
        {
            // Check if it's a working day according to calendar and schedule
            bool isWorkingDay = await _workCalendarService.IsWorkingDateForEmployeeAsync(
                employeeId, checkInTime.Date, _workScheduleService);
                
            if (!isWorkingDay)
                return false;
                
            // Get allowed attendance times with grace periods applied
            var (latestAllowedCheckIn, _) = await _workScheduleService.GetAllowedAttendanceTimesAsync(employeeId, checkInTime.Date);
            
            if (!latestAllowedCheckIn.HasValue)
                return false;
                
            // Compare with actual check-in time
            return checkInTime > latestAllowedCheckIn.Value;
        }
        
        // Method to check if an employee left early based on their work schedule
        public async Task<bool> IsEarlyCheckOutAsync(int employeeId, DateTime checkOutTime)
        {
            // Check if it's a working day according to calendar and schedule
            bool isWorkingDay = await _workCalendarService.IsWorkingDateForEmployeeAsync(
                employeeId, checkOutTime.Date, _workScheduleService);
                
            if (!isWorkingDay)
                return false;
                
            // Get allowed attendance times with grace periods applied
            var (_, earliestAllowedCheckOut) = await _workScheduleService.GetAllowedAttendanceTimesAsync(employeeId, checkOutTime.Date);
            
            if (!earliestAllowedCheckOut.HasValue)
                return false;
                
            // Compare with actual check-out time
            return checkOutTime < earliestAllowedCheckOut.Value;
        }
        
        // Method to calculate attendance statistics for an employee in a date range
        public async Task<AttendanceStatistics> GetAttendanceStatisticsAsync(int employeeId, DateTime startDate, DateTime endDate)
        {
            var statistics = new AttendanceStatistics
            {
                TotalWorkingDays = 0,
                DaysPresent = 0,
                DaysAbsent = 0,
                LateArrivals = 0,
                EarlyDepartures = 0
            };
            
            // Loop through each day in the date range
            for (var date = startDate; date <= endDate; date = date.AddDays(1))
            {
                // Check if it's a working day according to calendar and schedule
                bool isWorkingDay = await _workCalendarService.IsWorkingDateForEmployeeAsync(
                    employeeId, date, _workScheduleService);
                    
                if (isWorkingDay)
                {
                    statistics.TotalWorkingDays++;
                    
                    // Get attendance record for this day
                    var attendance = await GetByEmployeeAndDateAsync(employeeId, date);
                    
                    if (attendance != null && attendance.CheckInTime.HasValue)
                    {
                        statistics.DaysPresent++;
                        
                        // Check if employee was late
                        if (await IsLateCheckInAsync(employeeId, attendance.CheckInTime.Value))
                        {
                            statistics.LateArrivals++;
                        }
                        
                        // Check if employee left early
                        if (attendance.CheckOutTime.HasValue && 
                            await IsEarlyCheckOutAsync(employeeId, attendance.CheckOutTime.Value))
                        {
                            statistics.EarlyDepartures++;
                        }
                    }
                    else
                    {
                        statistics.DaysAbsent++;
                    }
                }
            }
            
            return statistics;
        }
        
        // Method to update WorkDuration and IsComplete fields for all attendance records
        public async Task UpdateAllWorkDurationsAndCompleteStatus()
        {
            // Get all attendance records
            using var ctx = NewCtx();
            var allAttendances = await ctx.Attendances.ToListAsync();
            
            foreach (var attendance in allAttendances)
            {
                // Calculate WorkDuration if both check-in and check-out times are available
                if (attendance.CheckInTime.HasValue && attendance.CheckOutTime.HasValue)
                {
                    attendance.WorkDuration = attendance.CheckOutTime.Value - attendance.CheckInTime.Value;
                    attendance.IsComplete = true;
                }
                else
                {
                    attendance.WorkDuration = null;
                    attendance.IsComplete = attendance.CheckInTime.HasValue && attendance.CheckOutTime.HasValue;
                }
            }
            
            // Save changes to the database
            await ctx.SaveChangesAsync();
        }
    }
    
    // Class to hold attendance statistics
    public class AttendanceStatistics
    {
        public int TotalWorkingDays { get; set; }
        public int DaysPresent { get; set; }
        public int DaysAbsent { get; set; }
        public int LateArrivals { get; set; }
        public int EarlyDepartures { get; set; }
        
        // Calculated properties
        public double AttendancePercentage => TotalWorkingDays > 0 
            ? (double)DaysPresent / TotalWorkingDays * 100 
            : 0;
            
        public double AbsencePercentage => TotalWorkingDays > 0 
            ? (double)DaysAbsent / TotalWorkingDays * 100 
            : 0;
            
        public double PunctualityPercentage => DaysPresent > 0 
            ? (double)(DaysPresent - LateArrivals) / DaysPresent * 100 
            : 0;
    }
} 