using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AttandenceDesktop.Services
{
    public class AttendanceService
    {
        private readonly Func<ApplicationDbContext> _contextFactory;
        private readonly Lazy<WorkScheduleService> _workScheduleService;
        private readonly WorkCalendarService _workCalendarService;
        private readonly DataRefreshService _dataRefreshService;
        
        public AttendanceService(
            Func<ApplicationDbContext> contextFactory,
            Lazy<WorkScheduleService> workScheduleService,
            WorkCalendarService workCalendarService,
            DataRefreshService dataRefreshService)
        {
            _contextFactory = contextFactory;
            _workScheduleService = workScheduleService;
            _workCalendarService = workCalendarService;
            _dataRefreshService = dataRefreshService;
        }
        
        private ApplicationDbContext NewCtx() => _contextFactory();
        
        // Helper method to access WorkScheduleService through Lazy<T>
        private WorkScheduleService WorkScheduleService => _workScheduleService.Value;
        
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
            
            Trace.WriteLine($"[Attendance Check-In] Starting - Employee ID: {employeeId}, Time: {now}");
            
            try
            {
                // Get employee info for logging
                string employeeName = "Unknown";
                Department employeeDepartment = null;
                Employee employee = null;
                bool isFlexibleHours = false;
                double requiredWorkHours = 8.0;
                
                using (var empCtx = NewCtx())
                {
                    employee = await empCtx.Employees
                        .Include(e => e.Department)
                        .Include(e => e.WorkSchedule)
                        .FirstOrDefaultAsync(e => e.Id == employeeId);
                        
                    if (employee != null)
                    {
                        employeeName = $"{employee.FirstName} {employee.LastName}";
                        employeeDepartment = employee.Department;
                        isFlexibleHours = employee.IsFlexibleHours;
                        requiredWorkHours = employee.RequiredWorkHoursPerDay;
                        Trace.WriteLine($"[Attendance Check-In] Employee info - Name: {employeeName}, Department: {employeeDepartment?.Name ?? "None"}, Flexible Hours: {isFlexibleHours}");
                    }
                }
                
                // Check if current date is past their hire date
                var attendance = await GetByEmployeeAndDateAsync(employeeId, today);
                
                bool isRemoteWorker = employeeDepartment?.Name?.Contains("دورکار") == true;
                bool isNightShift = false;
                
                // Get the employee's work schedule for time calculations
                var schedule = await WorkScheduleService.GetEmployeeWorkScheduleAsync(employeeId);
                
                // Check for night shift schedule (if start time is later than end time, it's likely a night shift)
                if (schedule != null && !schedule.IsFlexibleSchedule && !isFlexibleHours)
                {
                    isNightShift = schedule.StartTime.Hours > schedule.EndTime.Hours;
                    Trace.WriteLine($"[Attendance Check-In] Schedule analysis - Is night shift: {isNightShift}");
                }
                
                // For night shifts that cross midnight, we might need to adjust "today" for attendance purposes
                DateTime attendanceDate = today;
                
                // If it's early morning (before 6 AM) and this employee is on night shift,
                // we might want to count this as part of the previous day's shift
                if (isNightShift && now.Hour < 6 && now.Hour >= 0)
                {
                    attendanceDate = today.AddDays(-1);
                    Trace.WriteLine($"[Attendance Check-In] Night shift detection - Using previous day ({attendanceDate.ToShortDateString()}) for attendance record");
                    
                    // Re-check attendance record with adjusted date
                    attendance = await GetByEmployeeAndDateAsync(employeeId, attendanceDate);
                }
                
                if (attendance == null)
                {
                    // No existing attendance record for today
                    // Create a new attendance record
                    attendance = new Attendance
                    {
                        EmployeeId = employeeId,
                        Date = attendanceDate,
                        CheckInTime = now,
                        Notes = "",
                        IsComplete = false,
                        IsLateArrival = false
                    };
                    
                    // Check if the employee is late or early based on schedule
                    if (schedule != null && schedule.IsWorkingDay(attendanceDate.DayOfWeek))
                    {
                        // Override schedule flexibility with employee setting
                        bool isFlexible = isFlexibleHours || schedule.IsFlexibleSchedule;
                        
                        // Get expected work hours for the employee on this day
                        double expectedWorkHours = isFlexibleHours 
                            ? requiredWorkHours 
                            : schedule.CalculateExpectedWorkHours(attendanceDate);
                        
                        // Store the schedule type information
                        attendance.IsFlexibleSchedule = isFlexible;
                        attendance.ExpectedWorkHours = expectedWorkHours;
                        
                        // For fixed schedules with defined start/end times
                        if (!isFlexible)
                        {
                            // Calculate expected start time
                            var expectedStartTime = new DateTime(
                                attendanceDate.Year, attendanceDate.Month, attendanceDate.Day,
                                schedule.StartTime.Hours, schedule.StartTime.Minutes, 0
                            );
                            
                            // Apply flex time allowance
                            var latestAllowedTime = expectedStartTime.AddMinutes(schedule.FlexTimeAllowanceMinutes);
                            
                            Trace.WriteLine($"[Attendance Check-In] Schedule details - Expected start: {expectedStartTime:HH:mm:ss}, Latest allowed: {latestAllowedTime:HH:mm:ss}");
                            
                            // For night shifts, handle the special case if check-in is on the right day
                            if (isNightShift && attendanceDate == today)
                            {
                                Trace.WriteLine($"[Attendance Check-In] Night shift - checking against evening start time");
                            }
                            
                            // Check for late arrival
                            if (now > latestAllowedTime)
                            {
                                attendance.IsLateArrival = true;
                                attendance.LateMinutes = now - expectedStartTime; // Calculate from expected time, not grace period
                                attendance.AttendanceCode = "L"; // L for Late
                                Trace.WriteLine($"[Attendance Check-In] Employee is late by {attendance.LateMinutes.Value.TotalMinutes:0.##} minutes");
                            }
                            else if (now < expectedStartTime)
                            {
                                attendance.IsEarlyArrival = true;
                                attendance.EarlyArrivalMinutes = expectedStartTime - now;
                                attendance.AttendanceCode = "EA"; // EA for Early Arrival
                                Trace.WriteLine($"[Attendance Check-In] Employee arrived early by {attendance.EarlyArrivalMinutes.Value.TotalMinutes:0.##} minutes");
                            }
                            else
                            {
                                attendance.AttendanceCode = "P"; // P for Present
                                Trace.WriteLine($"[Attendance Check-In] Employee arrived on time");
                            }
                        }
                        else
                        {
                            // For flexible schedules, just record check-in without lateness flags
                            attendance.AttendanceCode = "P"; // P for Present
                            Trace.WriteLine($"[Attendance Check-In] Flexible schedule - no late/early check");
                        }
                    }
                    
                    using var ctx = NewCtx();
                    ctx.Attendances.Add(attendance);
                    await ctx.SaveChangesAsync();
                    
                    // Notify that attendance records have changed
                    _dataRefreshService.NotifyAttendanceChanged();
                    
                    Trace.WriteLine($"[Attendance Check-In] Success - Created new attendance record with ID: {attendance.Id}");
                    return attendance;
                }
                else
                {
                    // Attendance record for today already exists
                    if (attendance.IsComplete)
                    {
                        // Record is already completed with check-out - can't check in again
                        Trace.WriteLine($"[Attendance Check-In] Warning - Attendance record is already complete for today");
                        throw new InvalidOperationException("Attendance record for today is already completed.");
                    }
                    
                    if (attendance.CheckInTime.HasValue)
                    {
                        // Already checked in
                        Trace.WriteLine($"[Attendance Check-In] Warning - Already checked in at {attendance.CheckInTime.Value:HH:mm:ss}");
                        throw new InvalidOperationException($"Already checked in at {attendance.CheckInTime.Value:HH:mm:ss}");
                    }
                    
                    // Otherwise, update the check-in time
                    attendance.CheckInTime = now;
                    
                    // Similar logic as before for checking if late/early (adjusted for employee's flexible setting)
                    // Get the employee's work schedule if not already loaded
                    if (schedule == null)
                    {
                        schedule = await WorkScheduleService.GetEmployeeWorkScheduleAsync(employeeId);
                    }
                    
                    if (schedule != null && schedule.IsWorkingDay(attendanceDate.DayOfWeek))
                    {
                        // Override schedule flexibility with employee setting
                        bool isFlexible = isFlexibleHours || schedule.IsFlexibleSchedule;
                        
                        double expectedWorkHours = isFlexibleHours 
                            ? requiredWorkHours 
                            : schedule.CalculateExpectedWorkHours(attendanceDate);
                        
                        attendance.IsFlexibleSchedule = isFlexible;
                        attendance.ExpectedWorkHours = expectedWorkHours;
                        
                        if (!isFlexible)
                        {
                            var expectedStartTime = new DateTime(
                                attendanceDate.Year, attendanceDate.Month, attendanceDate.Day,
                                schedule.StartTime.Hours, schedule.StartTime.Minutes, 0
                            );
                            
                            var latestAllowedTime = expectedStartTime.AddMinutes(schedule.FlexTimeAllowanceMinutes);
                            
                            if (now > latestAllowedTime)
                            {
                                attendance.IsLateArrival = true;
                                attendance.LateMinutes = now - expectedStartTime;
                                attendance.AttendanceCode = "L"; // L for Late
                            }
                            else if (now < expectedStartTime)
                            {
                                attendance.IsEarlyArrival = true;
                                attendance.EarlyArrivalMinutes = expectedStartTime - now;
                                attendance.AttendanceCode = "EA"; // EA for Early Arrival
                            }
                            else
                            {
                                attendance.AttendanceCode = "P"; // P for Present
                            }
                        }
                        else
                        {
                            // For flexible schedules, just record check-in
                            attendance.AttendanceCode = "P"; // P for Present
                        }
                    }
                    
                    using var ctx = NewCtx();
                    ctx.Attendances.Update(attendance);
                    await ctx.SaveChangesAsync();
                    
                    // Notify that attendance records have changed
                    _dataRefreshService.NotifyAttendanceChanged();
                    
                    Trace.WriteLine($"[Attendance Check-In] Success - Updated existing attendance record ID: {attendance.Id} with check-in time");
                    return attendance;
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Attendance Check-In] ERROR - Failed to check in: {ex.Message}");
                throw;
            }
        }
        
        public async Task<Attendance> CheckOutAsync(int employeeId)
        {
            var today = DateTime.Today;
            var now = DateTime.Now;
            
            Trace.WriteLine($"[Attendance Check-Out] Starting - Employee ID: {employeeId}, Time: {now}");
            
            try
            {
                // Get employee info for logging
                string employeeName = "Unknown";
                Department employeeDepartment = null;
                Employee employee = null;
                bool isFlexibleHours = false;
                double requiredWorkHours = 8.0;
                
                using (var empCtx = NewCtx())
                {
                    employee = await empCtx.Employees
                        .Include(e => e.Department)
                        .Include(e => e.WorkSchedule)
                        .FirstOrDefaultAsync(e => e.Id == employeeId);
                        
                    if (employee != null)
                    {
                        employeeName = $"{employee.FirstName} {employee.LastName}";
                        employeeDepartment = employee.Department;
                        isFlexibleHours = employee.IsFlexibleHours;
                        requiredWorkHours = employee.RequiredWorkHoursPerDay;
                        Trace.WriteLine($"[Attendance Check-Out] Employee info - Name: {employeeName}, Department: {employeeDepartment?.Name ?? "None"}, Flexible Hours: {isFlexibleHours}");
                    }
                }
                
                // Get the schedule for this employee
                var schedule = await WorkScheduleService.GetEmployeeWorkScheduleAsync(employeeId);
                
                // Check if this is a night shift (might cross midnight)
                bool isNightShift = false;
                if (schedule != null && !schedule.IsFlexibleSchedule && !isFlexibleHours)
                {
                    isNightShift = schedule.StartTime.Hours > schedule.EndTime.Hours;
                }
                
                // For night shifts that cross midnight, we might need to adjust "today" for attendance purposes
                DateTime attendanceDate = today;
                
                // If it's a night shift and before noon, we might want to check the previous day's record
                if (isNightShift && now.Hour < 12)
                {
                    attendanceDate = today.AddDays(-1);
                    Trace.WriteLine($"[Attendance Check-Out] Night shift detection - Using previous day ({attendanceDate.ToShortDateString()}) for attendance record");
                }
                
                // Find today's attendance record
                var attendance = await GetByEmployeeAndDateAsync(employeeId, attendanceDate);
                
                if (attendance == null)
                {
                    // No attendance record found
                    var errorMsg = $"No check-in record found for today or yesterday (for night shift). Please check-in first.";
                    Trace.WriteLine($"[Attendance Check-Out] ERROR - {errorMsg}");
                    throw new InvalidOperationException(errorMsg);
                }
                
                if (attendance.IsComplete)
                {
                    var errorMsg = $"Attendance record already marked as complete.";
                    Trace.WriteLine($"[Attendance Check-Out] WARNING - {errorMsg}");
                    throw new InvalidOperationException(errorMsg);
                }
                
                if (!attendance.CheckInTime.HasValue)
                {
                    var errorMsg = $"Employee has not checked in yet.";
                    Trace.WriteLine($"[Attendance Check-Out] ERROR - {errorMsg}");
                    throw new InvalidOperationException(errorMsg);
                }
                
                // Update check-out time
                attendance.CheckOutTime = now;
                attendance.IsComplete = true;
                
                // Calculate work duration
                if (attendance.CheckInTime.HasValue && attendance.CheckOutTime.HasValue)
                {
                    attendance.WorkDuration = attendance.CheckOutTime.Value - attendance.CheckInTime.Value;
                    Trace.WriteLine($"[Attendance Check-Out] Work duration: {attendance.WorkDuration.Value.TotalHours:F1} hours");
                }
                
                // Check if the employee is leaving early or working overtime based on schedule
                bool isFlexible = isFlexibleHours || (schedule?.IsFlexibleSchedule ?? false);
                
                if (schedule != null && schedule.IsWorkingDay(attendanceDate.DayOfWeek) && !isFlexible)
                {
                    // Calculate expected end time
                    var expectedEndTime = new DateTime(
                        attendanceDate.Year, attendanceDate.Month, attendanceDate.Day,
                        schedule.EndTime.Hours, schedule.EndTime.Minutes, 0
                    );
                    
                    // If this is a night shift and end time is less than start time, add a day to the end time
                    if (isNightShift && schedule.EndTime < schedule.StartTime)
                    {
                        expectedEndTime = expectedEndTime.AddDays(1);
                        Trace.WriteLine($"[Attendance Check-Out] Night shift - adjusted expected end time to: {expectedEndTime:yyyy-MM-dd HH:mm:ss}");
                    }
                    
                    // Apply flex time allowance
                    var earliestAllowedOut = expectedEndTime.AddMinutes(-schedule.FlexTimeAllowanceMinutes);
                    
                    // Check for early departure
                    if (now < earliestAllowedOut)
                    {
                        attendance.IsEarlyDeparture = true;
                        attendance.EarlyDepartureMinutes = expectedEndTime - now;
                        attendance.AttendanceCode = "E"; // E for Early departure
                        Trace.WriteLine($"[Attendance Check-Out] Employee left early by {attendance.EarlyDepartureMinutes.Value.TotalMinutes:0.##} minutes");
                    }
                    // Check for overtime
                    else if (now > expectedEndTime)
                    {
                        attendance.IsOvertime = true;
                        attendance.OvertimeMinutes = now - expectedEndTime;
                        attendance.AttendanceCode = "O"; // O for Overtime
                        Trace.WriteLine($"[Attendance Check-Out] Employee worked overtime: {attendance.OvertimeMinutes.Value.TotalMinutes:0.##} minutes");
                    }
                    
                    // If they were late and now leaving early, update the code to LE
                    if (attendance.IsLateArrival && attendance.IsEarlyDeparture)
                    {
                        attendance.AttendanceCode = "LE"; // LE for Late and Early departure
                    }
                }
                else if (isFlexible)
                {
                    // For flexible schedules, check if the total work duration meets the required hours
                    double workHours = attendance.WorkDuration?.TotalHours ?? 0;
                    double expectedHours = isFlexibleHours ? requiredWorkHours : (schedule?.TotalWorkHours ?? 8.0);
                    
                    if (workHours < expectedHours)
                    {
                        // Worked less than required hours
                        var shortfall = TimeSpan.FromHours(expectedHours - workHours);
                        attendance.IsEarlyDeparture = true;
                        attendance.EarlyDepartureMinutes = shortfall;
                        attendance.AttendanceCode = "I"; // I for Incomplete hours
                        Trace.WriteLine($"[Attendance Check-Out] Flexible schedule: Worked {workHours:0.##}h of {expectedHours:0.##}h required (short by {shortfall.TotalMinutes:0.##} minutes)");
                    }
                    else if (workHours > expectedHours)
                    {
                        // Worked more than required hours
                        var excess = TimeSpan.FromHours(workHours - expectedHours);
                        attendance.IsOvertime = true;
                        attendance.OvertimeMinutes = excess;
                        attendance.AttendanceCode = "O"; // O for Overtime
                        Trace.WriteLine($"[Attendance Check-Out] Flexible schedule: Worked {workHours:0.##}h of {expectedHours:0.##}h required (extra {excess.TotalMinutes:0.##} minutes)");
                    }
                    else
                    {
                        attendance.AttendanceCode = "P"; // P for Present
                        Trace.WriteLine($"[Attendance Check-Out] Flexible schedule: Worked exactly {workHours:0.##}h as required");
                    }
                }
                
                // Save changes
                using var ctx = NewCtx();
                ctx.Attendances.Update(attendance);
                await ctx.SaveChangesAsync();
                
                // Notify that attendance records have changed
                _dataRefreshService.NotifyAttendanceChanged();
                
                Trace.WriteLine($"[Attendance Check-Out] Success - Updated attendance record ID: {attendance.Id}");
                return attendance;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Attendance Check-Out] ERROR - Failed to check out: {ex.Message}");
                throw;
            }
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
                
                // Recalculate late arrival, early departure, and overtime
                if (existingAttendance.CheckInTime.HasValue)
                {
                    // Check if the employee is late
                    var schedule = await WorkScheduleService.GetEmployeeWorkScheduleAsync(existingAttendance.EmployeeId);
                    if (schedule != null && schedule.IsWorkingDay(existingAttendance.Date.DayOfWeek))
                    {
                        // Calculate expected start time
                        var expectedStartTime = new DateTime(
                            existingAttendance.Date.Year, 
                            existingAttendance.Date.Month, 
                            existingAttendance.Date.Day,
                            schedule.StartTime.Hours, 
                            schedule.StartTime.Minutes, 
                            0
                        );
                        
                        // Apply flex time allowance
                        var latestAllowedTime = expectedStartTime.AddMinutes(schedule.FlexTimeAllowanceMinutes);
                        
                        if (existingAttendance.CheckInTime > latestAllowedTime)
                        {
                            existingAttendance.IsLateArrival = true;
                            existingAttendance.LateMinutes = existingAttendance.CheckInTime - expectedStartTime;
                        }
                        else if (existingAttendance.CheckInTime < expectedStartTime)
                        {
                            existingAttendance.IsEarlyArrival = true;
                            existingAttendance.EarlyArrivalMinutes = expectedStartTime - existingAttendance.CheckInTime;
                            existingAttendance.IsLateArrival = false;
                            existingAttendance.LateMinutes = null;
                        }
                        else
                        {
                            existingAttendance.IsLateArrival = false;
                            existingAttendance.LateMinutes = null;
                            existingAttendance.IsEarlyArrival = false;
                            existingAttendance.EarlyArrivalMinutes = null;
                        }
                        
                        // Check for early departure and overtime if check-out time exists
                        if (existingAttendance.CheckOutTime.HasValue)
                        {
                            // Calculate expected end time
                            var expectedEndTime = new DateTime(
                                existingAttendance.Date.Year, 
                                existingAttendance.Date.Month, 
                                existingAttendance.Date.Day,
                                schedule.EndTime.Hours, 
                                schedule.EndTime.Minutes, 
                                0
                            );
                            
                            // Apply flex time allowance for early departure
                            var earliestAllowedTime = expectedEndTime.AddMinutes(-schedule.FlexTimeAllowanceMinutes);
                            
                            // Check for early departure
                            if (existingAttendance.CheckOutTime < earliestAllowedTime)
                            {
                                existingAttendance.IsEarlyDeparture = true;
                                existingAttendance.EarlyDepartureMinutes = expectedEndTime - existingAttendance.CheckOutTime;
                            }
                            else
                            {
                                existingAttendance.IsEarlyDeparture = false;
                                existingAttendance.EarlyDepartureMinutes = null;
                            }
                            
                            // Check for overtime - no grace period for overtime
                            if (existingAttendance.CheckOutTime > expectedEndTime)
                            {
                                existingAttendance.OvertimeMinutes = existingAttendance.CheckOutTime - expectedEndTime;
                                existingAttendance.IsOvertime = true;
                            }
                            else
                            {
                                existingAttendance.OvertimeMinutes = null;
                                existingAttendance.IsOvertime = false;
                            }
                        }
                    }
                    else
                    {
                        // Not a working day according to schedule, reset flags
                        existingAttendance.IsLateArrival = false;
                        existingAttendance.IsEarlyDeparture = false;
                        existingAttendance.LateMinutes = null;
                        existingAttendance.EarlyDepartureMinutes = null;
                        existingAttendance.OvertimeMinutes = null;
                    }
                }
                
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
                
                await ctx.SaveChangesAsync();
                
                // Notify that attendance data has changed
                _dataRefreshService.NotifyAttendanceChanged();
                
                // Refresh from database
                using var refreshCtx = NewCtx();
                return await refreshCtx.Attendances
                    .Include(a => a.Employee)
                    .ThenInclude(e => e.Department)
                    .FirstOrDefaultAsync(a => a.Id == attendance.Id);
            }
            catch (Exception)
            {
                throw;
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
                
                // Notify that attendance data has changed
                _dataRefreshService.NotifyAttendanceChanged();
            }
        }
        
        // Method to check if an employee is late based on their work schedule
        public async Task<bool> IsLateCheckInAsync(int employeeId, DateTime checkInTime)
        {
            var date = checkInTime.Date;
            var schedule = await WorkScheduleService.GetEmployeeWorkScheduleAsync(employeeId);
            if (schedule == null || !schedule.IsWorkingDay(date.DayOfWeek))
            {
                return false; // Not a working day, so not late
            }
            
            var expectedStartTime = new DateTime(
                date.Year, date.Month, date.Day,
                schedule.StartTime.Hours, schedule.StartTime.Minutes, 0
            );
            
            // Apply flex time allowance
            var latestAllowedTime = expectedStartTime.AddMinutes(schedule.FlexTimeAllowanceMinutes);
            
            // Employee is late if check-in is after the latest allowed time
            return checkInTime > latestAllowedTime;
        }
        
        // Method to check if an employee left early based on their work schedule
        public async Task<bool> IsEarlyCheckOutAsync(int employeeId, DateTime checkOutTime)
        {
            var date = checkOutTime.Date;
            var schedule = await WorkScheduleService.GetEmployeeWorkScheduleAsync(employeeId);
            if (schedule == null || !schedule.IsWorkingDay(date.DayOfWeek))
            {
                return false; // Not a working day, so not early
            }
            
            var expectedEndTime = new DateTime(
                date.Year, date.Month, date.Day,
                schedule.EndTime.Hours, schedule.EndTime.Minutes, 0
            );
            
            // Apply flex time allowance
            var earliestAllowedTime = expectedEndTime.AddMinutes(-schedule.FlexTimeAllowanceMinutes);
            
            // Employee is leaving early if check-out is before the earliest allowed time
            return checkOutTime < earliestAllowedTime;
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
                    employeeId, date, WorkScheduleService);
                    
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
            using var ctx = NewCtx();
            var attendances = await ctx.Attendances.ToListAsync();
            
            foreach (var attendance in attendances)
            {
                // Calculate work duration if both check-in and check-out times are available
                if (attendance.CheckInTime.HasValue && attendance.CheckOutTime.HasValue)
                {
                    attendance.WorkDuration = attendance.CheckOutTime.Value - attendance.CheckInTime.Value;
                }
                else
                {
                    attendance.WorkDuration = null;
                }
                
                // Update IsComplete flag
                attendance.IsComplete = attendance.CheckInTime.HasValue && attendance.CheckOutTime.HasValue;
            }
            
            await ctx.SaveChangesAsync();
            
            // Notify that attendance data has changed
            _dataRefreshService.NotifyAttendanceChanged();
        }
        
        // Method to recalculate all attendance metrics (late, early departure, overtime)
        public async Task RecalculateAllAttendanceMetricsAsync()
        {
            using var ctx = NewCtx();
            var attendances = await ctx.Attendances.ToListAsync();
            
            foreach (var attendance in attendances)
            {
                if (attendance.CheckInTime.HasValue)
                {
                    // Get the employee's work schedule
                    var schedule = await WorkScheduleService.GetEmployeeWorkScheduleAsync(attendance.EmployeeId);
                    if (schedule != null && schedule.IsWorkingDay(attendance.Date.DayOfWeek))
                    {
                        // Calculate expected start time
                        var expectedStartTime = new DateTime(
                            attendance.Date.Year, 
                            attendance.Date.Month, 
                            attendance.Date.Day,
                            schedule.StartTime.Hours, 
                            schedule.StartTime.Minutes, 
                            0
                        );
                        
                        // Apply flex time allowance
                        var latestAllowedTime = expectedStartTime.AddMinutes(schedule.FlexTimeAllowanceMinutes);
                        
                        // Check for late arrival
                        if (attendance.CheckInTime > latestAllowedTime)
                        {
                            attendance.IsLateArrival = true;
                            attendance.LateMinutes = attendance.CheckInTime - expectedStartTime;
                        }
                        else
                        {
                            attendance.IsLateArrival = false;
                            attendance.LateMinutes = null;
                        }
                        
                        // Check for early departure and overtime if check-out time exists
                        if (attendance.CheckOutTime.HasValue)
                        {
                            // Calculate expected end time
                            var expectedEndTime = new DateTime(
                                attendance.Date.Year, 
                                attendance.Date.Month, 
                                attendance.Date.Day,
                                schedule.EndTime.Hours, 
                                schedule.EndTime.Minutes, 
                                0
                            );
                            
                            // Apply flex time allowance for early departure
                            var earliestAllowedTime = expectedEndTime.AddMinutes(-schedule.FlexTimeAllowanceMinutes);
                            
                            // Check for early departure
                            if (attendance.CheckOutTime < earliestAllowedTime)
                            {
                                attendance.IsEarlyDeparture = true;
                                attendance.EarlyDepartureMinutes = expectedEndTime - attendance.CheckOutTime;
                            }
                            else
                            {
                                attendance.IsEarlyDeparture = false;
                                attendance.EarlyDepartureMinutes = null;
                            }
                            
                            // Check for overtime - no grace period for overtime
                            if (attendance.CheckOutTime > expectedEndTime)
                            {
                                attendance.OvertimeMinutes = attendance.CheckOutTime - expectedEndTime;
                                attendance.IsOvertime = true;
                            }
                            else
                            {
                                attendance.OvertimeMinutes = null;
                                attendance.IsOvertime = false;
                            }
                        }
                    }
                    else
                    {
                        // Not a working day according to schedule, reset flags
                        attendance.IsLateArrival = false;
                        attendance.IsEarlyDeparture = false;
                        attendance.LateMinutes = null;
                        attendance.EarlyDepartureMinutes = null;
                        attendance.OvertimeMinutes = null;
                    }
                }
            }
            
            await ctx.SaveChangesAsync();
            
            // Notify that attendance data has changed
            _dataRefreshService.NotifyAttendanceChanged();
        }
        
        // Method to recalculate attendance metrics for specific employees
        public async Task RecalculateAttendanceMetricsForEmployeesAsync(List<int> employeeIds, DateTime? startDate = null)
        {
            if (employeeIds == null || !employeeIds.Any())
                return;
                
            using var ctx = NewCtx();
            var query = ctx.Attendances.Where(a => employeeIds.Contains(a.EmployeeId));
            
            // Apply date filter if provided
            if (startDate.HasValue)
            {
                query = query.Where(a => a.Date >= startDate.Value);
            }
            
            var attendances = await query.ToListAsync();
            
            foreach (var attendance in attendances)
            {
                if (attendance.CheckInTime.HasValue)
                {
                    // Get the employee's work schedule
                    var schedule = await WorkScheduleService.GetEmployeeWorkScheduleAsync(attendance.EmployeeId);

                    if (schedule == null)
                        continue;

                    bool isFlexible = schedule.IsFlexibleSchedule;
                    bool isWorkingDay = schedule.IsWorkingDay(attendance.Date.DayOfWeek);

                    if (!isWorkingDay)
                    {
                        // Not a working day according to schedule, clear all punctuality flags
                        attendance.IsLateArrival = false;
                        attendance.IsEarlyDeparture = false;
                        attendance.LateMinutes = null;
                        attendance.EarlyDepartureMinutes = null;
                        attendance.IsOvertime = false;
                        attendance.OvertimeMinutes = null;
                        continue;
                    }

                    if (isFlexible)
                    {
                        attendance.IsFlexibleSchedule = true;
                        attendance.ExpectedWorkHours = schedule.TotalWorkHours;

                        // Clear punctuality flags that are irrelevant
                        attendance.IsLateArrival = false;
                        attendance.IsEarlyDeparture = false;
                        attendance.LateMinutes = null;
                        attendance.EarlyDepartureMinutes = null;

                        // Ensure WorkDuration is calculated
                        if (attendance.CheckOutTime.HasValue && attendance.CheckInTime.HasValue && !attendance.WorkDuration.HasValue)
                        {
                            attendance.WorkDuration = attendance.CheckOutTime - attendance.CheckInTime;
                        }

                        // Calculate overtime purely on total hours
                        if (attendance.WorkDuration.HasValue && attendance.WorkDuration.Value.TotalHours > schedule.TotalWorkHours)
                        {
                            attendance.IsOvertime = true;
                            attendance.OvertimeMinutes = attendance.WorkDuration - TimeSpan.FromHours(schedule.TotalWorkHours);
                        }
                        else
                        {
                            attendance.IsOvertime = false;
                            attendance.OvertimeMinutes = null;
                        }
                    }
                    else
                    {
                        // ---------- Regular (fixed) schedule processing (existing logic) ----------
                        // Calculate expected start time
                        var expectedStartTime = new DateTime(
                            attendance.Date.Year,
                            attendance.Date.Month,
                            attendance.Date.Day,
                            schedule.StartTime.Hours,
                            schedule.StartTime.Minutes,
                            0);

                        var latestAllowedTime = expectedStartTime.AddMinutes(schedule.FlexTimeAllowanceMinutes);

                        // Late / early arrival logic
                        if (attendance.CheckInTime > latestAllowedTime)
                        {
                            attendance.IsLateArrival = true;
                            attendance.LateMinutes = attendance.CheckInTime - expectedStartTime;
                        }
                        else if (attendance.CheckInTime < expectedStartTime)
                        {
                            attendance.IsEarlyArrival = true;
                            attendance.EarlyArrivalMinutes = expectedStartTime - attendance.CheckInTime;
                            attendance.IsLateArrival = false;
                            attendance.LateMinutes = null;
                        }
                        else
                        {
                            attendance.IsLateArrival = false;
                            attendance.LateMinutes = null;
                            attendance.IsEarlyArrival = false;
                            attendance.EarlyArrivalMinutes = null;
                        }

                        // Early departure / overtime if check-out exists
                        if (attendance.CheckOutTime.HasValue)
                        {
                            var expectedEndTime = new DateTime(
                                attendance.Date.Year,
                                attendance.Date.Month,
                                attendance.Date.Day,
                                schedule.EndTime.Hours,
                                schedule.EndTime.Minutes,
                                0);

                            var earliestAllowedTime = expectedEndTime.AddMinutes(-schedule.FlexTimeAllowanceMinutes);

                            if (attendance.CheckOutTime < earliestAllowedTime)
                            {
                                attendance.IsEarlyDeparture = true;
                                attendance.EarlyDepartureMinutes = expectedEndTime - attendance.CheckOutTime;
                            }
                            else
                            {
                                attendance.IsEarlyDeparture = false;
                                attendance.EarlyDepartureMinutes = null;
                            }

                            if (attendance.CheckOutTime > expectedEndTime)
                            {
                                attendance.IsOvertime = true;
                                attendance.OvertimeMinutes = attendance.CheckOutTime - expectedEndTime;
                            }
                            else
                            {
                                attendance.IsOvertime = false;
                                attendance.OvertimeMinutes = null;
                            }
                        }
                    }
                }
            }
            
            await ctx.SaveChangesAsync();
            
            // Notify that attendance data has changed
            _dataRefreshService.NotifyAttendanceChanged();
        }

        public async Task GenerateAttendanceFromPunchLogsAsync(List<int> employeeIds, DateTime? since = null)
        {
            if (employeeIds == null || !employeeIds.Any()) return;
            using var ctx = NewCtx();
            DateTime minDate = since?.Date ?? DateTime.Today.AddDays(-30);
            var logsQuery = ctx.PunchLogs
                .Include(p => p.Employee)
                .Where(p => employeeIds.Contains(p.EmployeeId) && p.PunchTime.Date >= minDate);

            var logs = await logsQuery.ToListAsync();
            var grouped = logs.GroupBy(l => new { l.EmployeeId, Day = l.PunchTime.Date });

            foreach (var g in grouped)
            {
                DateTime? firstIn = g.Where(x => x.PunchType == PunchType.CheckIn).OrderBy(x => x.PunchTime).FirstOrDefault()?.PunchTime;
                DateTime? lastOut = g.Where(x => x.PunchType == PunchType.CheckOut).OrderByDescending(x => x.PunchTime).FirstOrDefault()?.PunchTime;
                bool hasIn = firstIn.HasValue;
                bool hasOut = lastOut.HasValue;
                // Fallback: if no explicit CheckIn but there is only one record treat it as CheckIn
                if (!hasIn && !hasOut)
                {
                    var only = g.Min(x => x.PunchTime);
                    firstIn = only;
                    hasIn = true;
                }
                 
                var attendance = await ctx.Attendances.FirstOrDefaultAsync(a => a.EmployeeId == g.Key.EmployeeId && a.Date == g.Key.Day);
                if (attendance == null)
                {
                    attendance = new Attendance
                    {
                        EmployeeId = g.Key.EmployeeId,
                        Date = g.Key.Day,
                        CheckInTime = firstIn,
                        CheckOutTime = hasIn && hasOut && lastOut > firstIn ? lastOut : null,
                        Notes = "Imported from device logs",
                        WorkDuration = (hasIn && hasOut && lastOut > firstIn) ? lastOut.Value - firstIn.Value : null,
                        IsComplete = hasIn && hasOut && lastOut > firstIn
                    };
                    ctx.Attendances.Add(attendance);
                }
                else
                {
                    // Update if missing times
                    if (hasIn && (!attendance.CheckInTime.HasValue || firstIn < attendance.CheckInTime))
                        attendance.CheckInTime = firstIn;
                    if (hasOut && (!attendance.CheckOutTime.HasValue || lastOut > attendance.CheckOutTime))
                        attendance.CheckOutTime = lastOut;
                    if (attendance.CheckInTime.HasValue && attendance.CheckOutTime.HasValue && attendance.CheckOutTime > attendance.CheckInTime)
                    {
                        attendance.WorkDuration = attendance.CheckOutTime - attendance.CheckInTime;
                        attendance.IsComplete = true;
                    }
                }
            }
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