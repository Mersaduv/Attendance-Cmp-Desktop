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
                using (var empCtx = NewCtx())
                {
                    var emp = await empCtx.Employees
                        .Include(e => e.Department)
                        .FirstOrDefaultAsync(e => e.Id == employeeId);
                    if (emp != null)
                    {
                        employeeName = $"{emp.FirstName} {emp.LastName}";
                        Trace.WriteLine($"[Attendance Check-In] Employee info - Name: {employeeName}, Department: {emp.Department?.Name ?? "None"}");
                    }
                }
                
                var attendance = await GetByEmployeeAndDateAsync(employeeId, today);
                
                // Get the employee's work schedule for time calculations
                var schedule = await WorkScheduleService.GetEmployeeWorkScheduleAsync(employeeId);
                if (schedule == null)
                {
                    Trace.WriteLine($"[Attendance Check-In] WARNING - No work schedule found for employee {employeeId} ({employeeName})");
                }
                else 
                {
                    Trace.WriteLine($"[Attendance Check-In] Found schedule: {schedule.Name}, Start: {schedule.StartTime}, End: {schedule.EndTime}");
                }
                
                if (attendance == null)
                {
                    Trace.WriteLine($"[Attendance Check-In] Creating new attendance record for employee ID: {employeeId} ({employeeName})");
                    
                    // Create new attendance record
                    attendance = new Attendance
                    {
                        EmployeeId = employeeId,
                        Date = today,
                        CheckInTime = now,
                        Notes = ""
                    };
                    
                    // Check if the employee is late
                    if (schedule != null && schedule.IsWorkingDay(today.DayOfWeek))
                    {
                        // Calculate expected start time
                        var expectedStartTime = new DateTime(
                            today.Year, today.Month, today.Day,
                            schedule.StartTime.Hours, schedule.StartTime.Minutes, 0
                        );
                        
                        // Apply flex time allowance
                        var latestAllowedTime = expectedStartTime.AddMinutes(schedule.FlexTimeAllowanceMinutes);
                        
                        Trace.WriteLine($"[Attendance Check-In] Schedule details - Expected start: {expectedStartTime:HH:mm:ss}, Latest allowed: {latestAllowedTime:HH:mm:ss}");
                        
                        if (now > latestAllowedTime)
                        {
                            attendance.IsLateArrival = true;
                            attendance.LateMinutes = now - expectedStartTime; // Calculate from expected time, not grace period
                            Trace.WriteLine($"[Attendance Check-In] LATE - Employee ID: {employeeId} ({employeeName}) is late by {attendance.LateMinutes.Value.TotalMinutes:F0} minutes");
                        }
                        else if (now < expectedStartTime)
                        {
                            attendance.IsEarlyArrival = true;
                            attendance.EarlyArrivalMinutes = expectedStartTime - now;
                            Trace.WriteLine($"[Attendance Check-In] EARLY ARRIVAL - Employee ID: {employeeId} ({employeeName}) arrived early by {attendance.EarlyArrivalMinutes.Value.TotalMinutes:F0} minutes");
                        }
                        else
                        {
                            Trace.WriteLine($"[Attendance Check-In] ON TIME - Employee ID: {employeeId} ({employeeName}) arrived on time");
                        }
                    }
                    else if (schedule != null)
                    {
                        Trace.WriteLine($"[Attendance Check-In] Today ({today.DayOfWeek}) is not a working day according to schedule");
                    }
                    
                    using var addCtx = NewCtx();
                    addCtx.Attendances.Add(attendance);
                    await addCtx.SaveChangesAsync();
                    
                    // Notify that attendance data has changed
                    _dataRefreshService.NotifyAttendanceChanged();
                    Trace.WriteLine($"[Attendance Check-In] Success - Created attendance record ID: {attendance.Id} for employee ID: {employeeId} ({employeeName})");
                }
                else if (!attendance.CheckInTime.HasValue)
                {
                    Trace.WriteLine($"[Attendance Check-In] Updating existing attendance record ID: {attendance.Id} for employee ID: {employeeId} ({employeeName})");
                    
                    // Find the existing entity to update it
                    using var updateCtx = NewCtx();
                    var existingAttendance = await updateCtx.Attendances.FindAsync(attendance.Id);
                    if (existingAttendance != null)
                    {
                        existingAttendance.CheckInTime = now;
                        
                        // Check if the employee is late
                        if (schedule != null && schedule.IsWorkingDay(today.DayOfWeek))
                        {
                            // Calculate expected start time
                            var expectedStartTime = new DateTime(
                                today.Year, today.Month, today.Day,
                                schedule.StartTime.Hours, schedule.StartTime.Minutes, 0
                            );
                            
                            // Apply flex time allowance
                            var latestAllowedTime = expectedStartTime.AddMinutes(schedule.FlexTimeAllowanceMinutes);
                            
                            Trace.WriteLine($"[Attendance Check-In] Schedule details - Expected start: {expectedStartTime:HH:mm:ss}, Latest allowed: {latestAllowedTime:HH:mm:ss}");
                            
                            if (now > latestAllowedTime)
                            {
                                existingAttendance.IsLateArrival = true;
                                existingAttendance.LateMinutes = now - expectedStartTime; // Calculate from expected time, not grace period
                                Trace.WriteLine($"[Attendance Check-In] LATE - Employee ID: {employeeId} ({employeeName}) is late by {existingAttendance.LateMinutes.Value.TotalMinutes:F0} minutes");
                            }
                            else if (now < expectedStartTime)
                            {
                                existingAttendance.IsEarlyArrival = true;
                                existingAttendance.EarlyArrivalMinutes = expectedStartTime - now;
                                Trace.WriteLine($"[Attendance Check-In] EARLY ARRIVAL - Employee ID: {employeeId} ({employeeName}) arrived early by {existingAttendance.EarlyArrivalMinutes.Value.TotalMinutes:F0} minutes");
                            }
                            else
                            {
                                Trace.WriteLine($"[Attendance Check-In] ON TIME - Employee ID: {employeeId} ({employeeName}) arrived on time");
                            }
                        }
                        else if (schedule != null)
                        {
                            Trace.WriteLine($"[Attendance Check-In] Today ({today.DayOfWeek}) is not a working day according to schedule");
                        }
                        
                        await updateCtx.SaveChangesAsync();
                        
                        // Notify that attendance data has changed
                        _dataRefreshService.NotifyAttendanceChanged();
                        Trace.WriteLine($"[Attendance Check-In] Success - Updated attendance record ID: {attendance.Id} for employee ID: {employeeId} ({employeeName})");
                    }
                }
                else
                {
                    Trace.WriteLine($"[Attendance Check-In] WARNING - Employee ID: {employeeId} ({employeeName}) already checked in at {attendance.CheckInTime.Value}");
                }
                
                using var refreshCtx = NewCtx();
                attendance = await refreshCtx.Attendances.FindAsync(attendance.Id);
                
                return attendance;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Attendance Check-In] ERROR - Failed check-in for employee ID: {employeeId}: {ex.Message}");
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
                using (var empCtx = NewCtx())
                {
                    var emp = await empCtx.Employees
                        .Include(e => e.Department)
                        .FirstOrDefaultAsync(e => e.Id == employeeId);
                    if (emp != null)
                    {
                        employeeName = $"{emp.FirstName} {emp.LastName}";
                        Trace.WriteLine($"[Attendance Check-Out] Employee info - Name: {employeeName}, Department: {emp.Department?.Name ?? "None"}");
                    }
                }
                
                var attendance = await GetByEmployeeAndDateAsync(employeeId, today);
                
                // Get the employee's work schedule for time calculations
                var schedule = await WorkScheduleService.GetEmployeeWorkScheduleAsync(employeeId);
                if (schedule == null)
                {
                    Trace.WriteLine($"[Attendance Check-Out] WARNING - No work schedule found for employee {employeeId} ({employeeName})");
                }
                else 
                {
                    Trace.WriteLine($"[Attendance Check-Out] Found schedule: {schedule.Name}, Start: {schedule.StartTime}, End: {schedule.EndTime}");
                }
                
                if (attendance != null && attendance.CheckInTime.HasValue)
                {
                    // Find the existing entity to update it
                    using var updateCtx2 = NewCtx();
                    var existingAttendance = await updateCtx2.Attendances.FindAsync(attendance.Id);
                    if (existingAttendance != null)
                    {
                        if (existingAttendance.CheckOutTime.HasValue)
                        {
                            Trace.WriteLine($"[Attendance Check-Out] WARNING - Employee ID: {employeeId} ({employeeName}) already checked out at {existingAttendance.CheckOutTime.Value}");
                        }
                        else
                        {
                            existingAttendance.CheckOutTime = now;
                            
                            // Calculate work duration
                            if (existingAttendance.CheckInTime.HasValue)
                            {
                                existingAttendance.WorkDuration = now - existingAttendance.CheckInTime.Value;
                                Trace.WriteLine($"[Attendance Check-Out] Work duration: {existingAttendance.WorkDuration.Value.TotalHours:F2} hours for employee ID: {employeeId} ({employeeName})");
                            }
                            
                            // Check if the employee is leaving early or working overtime
                            if (schedule != null && schedule.IsWorkingDay(today.DayOfWeek))
                            {
                                // Calculate expected end time
                                var expectedEndTime = new DateTime(
                                    today.Year, today.Month, today.Day,
                                    schedule.EndTime.Hours, schedule.EndTime.Minutes, 0
                                );
                                
                                // Apply flex time allowance for early departure
                                var earliestAllowedTime = expectedEndTime.AddMinutes(-schedule.FlexTimeAllowanceMinutes);
                                
                                Trace.WriteLine($"[Attendance Check-Out] Schedule details - Expected end: {expectedEndTime:HH:mm:ss}, Earliest allowed: {earliestAllowedTime:HH:mm:ss}");
                                
                                // Check for early departure
                                if (now < earliestAllowedTime)
                                {
                                    existingAttendance.IsEarlyDeparture = true;
                                    existingAttendance.EarlyDepartureMinutes = expectedEndTime - now; // Calculate from expected time, not grace period
                                    Trace.WriteLine($"[Attendance Check-Out] EARLY DEPARTURE - Employee ID: {employeeId} ({employeeName}) left early by {existingAttendance.EarlyDepartureMinutes.Value.TotalMinutes:F0} minutes");
                                }
                                
                                // Check for overtime - no grace period for overtime
                                if (now > expectedEndTime)
                                {
                                    existingAttendance.OvertimeMinutes = now - expectedEndTime;
                                    existingAttendance.IsOvertime = true;
                                    Trace.WriteLine($"[Attendance Check-Out] OVERTIME - Employee ID: {employeeId} ({employeeName}) worked {existingAttendance.OvertimeMinutes.Value.TotalMinutes:F0} minutes overtime");
                                }
                                else
                                {
                                    Trace.WriteLine($"[Attendance Check-Out] REGULAR - Employee ID: {employeeId} ({employeeName}) checked out at regular time");
                                }
                            }
                            else if (schedule != null)
                            {
                                Trace.WriteLine($"[Attendance Check-Out] Today ({today.DayOfWeek}) is not a working day according to schedule");
                            }
                            
                            // Update IsComplete flag
                            existingAttendance.IsComplete = true;
                            
                            await updateCtx2.SaveChangesAsync();
                            
                            // Notify that attendance data has changed
                            _dataRefreshService.NotifyAttendanceChanged();
                            Trace.WriteLine($"[Attendance Check-Out] Success - Updated attendance record ID: {attendance.Id} for employee ID: {employeeId} ({employeeName})");
                        }
                        
                        // Refresh from database
                        using var refreshCtx2 = NewCtx();
                        attendance = await refreshCtx2.Attendances.FindAsync(attendance.Id);
                    }
                }
                else
                {
                    Trace.WriteLine($"[Attendance Check-Out] ERROR - No check-in record found for employee ID: {employeeId} ({employeeName}) for today");
                }
                
                return attendance;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Attendance Check-Out] ERROR - Failed check-out for employee ID: {employeeId}: {ex.Message}");
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
                            Trace.WriteLine($"[RecalculateMetrics] Employee ID: {attendance.EmployeeId} was late on {attendance.Date:yyyy-MM-dd} by {attendance.LateMinutes.Value.TotalMinutes:F0} minutes");
                        }
                        else if (attendance.CheckInTime < expectedStartTime)
                        {
                            attendance.IsEarlyArrival = true;
                            attendance.EarlyArrivalMinutes = expectedStartTime - attendance.CheckInTime;
                            attendance.IsLateArrival = false;
                            attendance.LateMinutes = null;
                            Trace.WriteLine($"[RecalculateMetrics] Employee ID: {attendance.EmployeeId} arrived early on {attendance.Date:yyyy-MM-dd} by {attendance.EarlyArrivalMinutes.Value.TotalMinutes:F0} minutes");
                        }
                        else
                        {
                            attendance.IsLateArrival = false;
                            attendance.LateMinutes = null;
                            attendance.IsEarlyArrival = false;
                            attendance.EarlyArrivalMinutes = null;
                            Trace.WriteLine($"[RecalculateMetrics] Employee ID: {attendance.EmployeeId} arrived on time on {attendance.Date:yyyy-MM-dd}");
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