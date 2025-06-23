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
    public class WorkScheduleService
    {
        private readonly Func<ApplicationDbContext> _contextFactory;
        private readonly DataRefreshService _dataRefreshService;
        private readonly AttendanceService _attendanceService;

        public WorkScheduleService(
            Func<ApplicationDbContext> contextFactory, 
            DataRefreshService dataRefreshService,
            AttendanceService attendanceService)
        {
            _contextFactory = contextFactory;
            _dataRefreshService = dataRefreshService;
            _attendanceService = attendanceService;
        }

        private ApplicationDbContext NewCtx() => _contextFactory();
        
        public async Task<List<WorkSchedule>> GetAllAsync()
        {
            using var ctx = NewCtx();
            return await ctx.WorkSchedules
                .Include(ws => ws.Department)
                .ToListAsync();
        }
        
        public async Task<WorkSchedule> GetByIdAsync(int id)
        {
            using var ctx = NewCtx();
            return await ctx.WorkSchedules
                .Include(ws => ws.Department)
                .Include(ws => ws.Employees)
                .FirstOrDefaultAsync(ws => ws.Id == id);
        }
        
        public async Task<List<WorkSchedule>> GetByDepartmentAsync(int departmentId)
        {
            using var ctx = NewCtx();
            return await ctx.WorkSchedules
                .Where(ws => ws.DepartmentId == departmentId)
                .ToListAsync();
        }
        
        public async Task<WorkSchedule> GetEmployeeWorkScheduleAsync(int employeeId)
        {
            Trace.WriteLine($"[WorkSchedule] Getting work schedule for employee ID: {employeeId}");
            
            try 
            {
                using var ctx = NewCtx();
                
                // Load the employee with eager loading of related entities
                var employee = await ctx.Employees
                    .Include(e => e.WorkSchedule)
                    .Include(e => e.Department)
                    .FirstOrDefaultAsync(e => e.Id == employeeId);
                    
                if (employee == null)
                {
                    Trace.WriteLine($"[WorkSchedule] ERROR - Employee with ID {employeeId} not found");
                    return null;
                }
                
                WorkSchedule schedule = null;
                
                // If employee has specific schedule assigned, use it
                if (employee.WorkScheduleId.HasValue && employee.WorkSchedule != null)
                {
                    // Reload the work schedule to ensure Department is included
                    schedule = await ctx.WorkSchedules
                        .Include(ws => ws.Department)
                        .FirstOrDefaultAsync(ws => ws.Id == employee.WorkScheduleId.Value);
                        
                    if (schedule != null)
                    {
                        Trace.WriteLine($"[WorkSchedule] Found specific schedule for employee: {schedule.Name} (ID: {schedule.Id})");
                    }
                }
                
                // If no specific schedule, try department's schedule
                if (schedule == null && employee.DepartmentId > 0)
                {
                    // Explicitly get department schedules with their department included
                    var departmentSchedules = await ctx.WorkSchedules
                        .Include(ws => ws.Department)
                        .Where(ws => ws.DepartmentId == employee.DepartmentId)
                        .ToListAsync();
                        
                    if (departmentSchedules.Any())
                    {
                        schedule = departmentSchedules.First();
                        Trace.WriteLine($"[WorkSchedule] Using department schedule: {schedule.Name} (ID: {schedule.Id})");
                    }
                }
                
                // If still no schedule, get a default one
                if (schedule == null)
                {
                    // Get any available schedule
                    schedule = await ctx.WorkSchedules
                        .Include(ws => ws.Department)
                        .FirstOrDefaultAsync();
                        
                    if (schedule != null)
                    {
                        Trace.WriteLine($"[WorkSchedule] Using default schedule: {schedule.Name} (ID: {schedule.Id})");
                    }
                    else
                    {
                        Trace.WriteLine($"[WorkSchedule] WARNING - No work schedules found in the system");
                    }
                }
                
                // Log the schedule start/end times for debugging
                if (schedule != null)
                {
                    Trace.WriteLine($"[WorkSchedule] Schedule details - Start: {schedule.StartTime}, End: {schedule.EndTime}, " +
                        $"Flex minutes: {schedule.FlexTimeAllowanceMinutes}, Department: {(schedule.Department?.Name ?? "None")}");
                }
                
                return schedule;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WorkSchedule] ERROR - Failed to retrieve schedule: {ex.Message}");
                return null;
            }
        }
        
        public async Task<WorkSchedule> CreateAsync(WorkSchedule workSchedule)
        {
            Trace.WriteLine($"[WorkSchedule Creation] Starting - Name: {workSchedule.Name}, Department: {workSchedule.DepartmentId}");
            try
            {
                using var ctx = NewCtx();
                
                // Ensure the Department reference is loaded if a DepartmentId is specified
                if (workSchedule.DepartmentId.HasValue && workSchedule.DepartmentId.Value > 0)
                {
                    var department = await ctx.Departments.FindAsync(workSchedule.DepartmentId.Value);
                    if (department != null)
                    {
                        Trace.WriteLine($"[WorkSchedule Creation] Found department: {department.Name} (ID: {department.Id})");
                    }
                    else
                    {
                        Trace.WriteLine($"[WorkSchedule Creation] WARNING - Department with ID {workSchedule.DepartmentId} not found");
                    }
                }
                
                ctx.WorkSchedules.Add(workSchedule);
                await ctx.SaveChangesAsync();
                
                // Reload the work schedule with department included
                var savedWorkSchedule = await ctx.WorkSchedules
                    .Include(w => w.Department)
                    .FirstOrDefaultAsync(w => w.Id == workSchedule.Id);
                
                if (savedWorkSchedule != null && savedWorkSchedule.Department != null)
                {
                    Trace.WriteLine($"[WorkSchedule Creation] Successfully linked to department: {savedWorkSchedule.Department.Name}");
                }
                
                // Notify that work schedules have changed
                _dataRefreshService.NotifyWorkSchedulesChanged();
                
                Trace.WriteLine($"[WorkSchedule Creation] Success - Created schedule with ID: {workSchedule.Id}, Name: {workSchedule.Name}, Start: {workSchedule.StartTime}, End: {workSchedule.EndTime}");
                return savedWorkSchedule ?? workSchedule;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WorkSchedule Creation] ERROR - Failed to create schedule: {ex.Message}");
                throw;
            }
        }
        
        public async Task<WorkSchedule> UpdateAsync(WorkSchedule workSchedule)
        {
            Trace.WriteLine($"[WorkSchedule Update] Starting - ID: {workSchedule.Id}, Name: {workSchedule.Name}");
            try
            {
                using var ctx = NewCtx();
                var existingWorkSchedule = await ctx.WorkSchedules.FindAsync(workSchedule.Id);
                if (existingWorkSchedule == null)
                {
                    var errorMsg = $"Work Schedule with ID {workSchedule.Id} not found";
                    Trace.WriteLine($"[WorkSchedule Update] ERROR - {errorMsg}");
                    throw new KeyNotFoundException(errorMsg);
                }
                
                // Store the original department ID to check if it changed
                var originalDepartmentId = existingWorkSchedule.DepartmentId;
                
                // Update properties
                existingWorkSchedule.Name = workSchedule.Name;
                existingWorkSchedule.StartTime = workSchedule.StartTime;
                existingWorkSchedule.EndTime = workSchedule.EndTime;
                existingWorkSchedule.IsWorkingDaySunday = workSchedule.IsWorkingDaySunday;
                existingWorkSchedule.IsWorkingDayMonday = workSchedule.IsWorkingDayMonday;
                existingWorkSchedule.IsWorkingDayTuesday = workSchedule.IsWorkingDayTuesday;
                existingWorkSchedule.IsWorkingDayWednesday = workSchedule.IsWorkingDayWednesday;
                existingWorkSchedule.IsWorkingDayThursday = workSchedule.IsWorkingDayThursday;
                existingWorkSchedule.IsWorkingDayFriday = workSchedule.IsWorkingDayFriday;
                existingWorkSchedule.IsWorkingDaySaturday = workSchedule.IsWorkingDaySaturday;
                existingWorkSchedule.FlexTimeAllowanceMinutes = workSchedule.FlexTimeAllowanceMinutes;
                existingWorkSchedule.Description = workSchedule.Description;
                existingWorkSchedule.DepartmentId = workSchedule.DepartmentId;
                
                // Save changes
                await ctx.SaveChangesAsync();
                
                // Notify that work schedules have changed
                _dataRefreshService.NotifyWorkSchedulesChanged();
                
                Trace.WriteLine($"[WorkSchedule Update] Success - Updated schedule ID: {workSchedule.Id}");
                
                // Get employees affected by this work schedule
                var employeeIds = new List<int>();
                
                // If department changed, include employees from both old and new departments
                if (originalDepartmentId > 0 && originalDepartmentId != existingWorkSchedule.DepartmentId)
                {
                    var originalDeptEmployees = await ctx.Employees
                        .Where(e => e.DepartmentId == originalDepartmentId)
                        .Select(e => e.Id)
                        .ToListAsync();
                    employeeIds.AddRange(originalDeptEmployees);
                }
                
                // Add employees from current department
                if (existingWorkSchedule.DepartmentId > 0)
                {
                    var currentDeptEmployees = await ctx.Employees
                        .Where(e => e.DepartmentId == existingWorkSchedule.DepartmentId)
                        .Select(e => e.Id)
                        .ToListAsync();
                    employeeIds.AddRange(currentDeptEmployees);
                }
                
                // Add employees directly assigned to this schedule
                var assignedEmployees = await ctx.Employees
                    .Where(e => e.WorkScheduleId == existingWorkSchedule.Id)
                    .Select(e => e.Id)
                    .ToListAsync();
                employeeIds.AddRange(assignedEmployees);
                
                // Remove duplicates
                employeeIds = employeeIds.Distinct().ToList();
                
                if (employeeIds.Any())
                {
                    // Recalculate attendance metrics for affected employees
                    var startDate = DateTime.Today.AddDays(-30);
                    await _attendanceService.RecalculateAttendanceMetricsForEmployeesAsync(employeeIds, startDate);
                }
                
                return existingWorkSchedule;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WorkSchedule Update] ERROR - Failed to update schedule: {ex.Message}");
                throw;
            }
        }
        
        public async Task DeleteAsync(int id)
        {
            Trace.WriteLine($"[WorkSchedule Delete] Starting - Schedule ID: {id}");
            try
            {
                using var ctx = NewCtx();
                var workSchedule = await ctx.WorkSchedules.FindAsync(id);
                if (workSchedule != null)
                {
                    ctx.WorkSchedules.Remove(workSchedule);
                    await ctx.SaveChangesAsync();
                    
                    // Notify that work schedules have changed
                    _dataRefreshService.NotifyWorkSchedulesChanged();
                    Trace.WriteLine($"[WorkSchedule Delete] Success - Deleted schedule ID: {id}");
                }
                else
                {
                    Trace.WriteLine($"[WorkSchedule Delete] Warning - Schedule ID: {id} not found");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WorkSchedule Delete] ERROR - Failed to delete schedule: {ex.Message}");
                throw;
            }
        }
        
        public async Task AssignToEmployeeAsync(int workScheduleId, int employeeId)
        {
            Trace.WriteLine($"[WorkSchedule Assignment] Starting - Assigning schedule ID: {workScheduleId} to employee ID: {employeeId}");
            try
            {
                using var ctx = NewCtx();
                var employee = await ctx.Employees.FindAsync(employeeId);
                if (employee != null)
                {
                    employee.WorkScheduleId = workScheduleId;
                    await ctx.SaveChangesAsync();
                    
                    // Notify that employees and work schedules have changed
                    _dataRefreshService.NotifyEmployeesChanged();
                    _dataRefreshService.NotifyWorkSchedulesChanged();
                    
                    Trace.WriteLine($"[WorkSchedule Assignment] Success - Assigned schedule ID: {workScheduleId} to employee ID: {employeeId} (Name: {employee.FirstName} {employee.LastName})");
                    
                    // Create a list with just this employee's ID
                    var employeeIds = new List<int> { employeeId };
                    
                    // Recalculate attendance metrics for this employee
                    // Recalculate for the last 30 days by default
                    var startDate = DateTime.Today.AddDays(-30);
                    await _attendanceService.RecalculateAttendanceMetricsForEmployeesAsync(employeeIds, startDate);
                }
                else
                {
                    Trace.WriteLine($"[WorkSchedule Assignment] ERROR - Employee ID: {employeeId} not found");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WorkSchedule Assignment] ERROR - Failed to assign schedule: {ex.Message}");
                throw;
            }
        }
        
        public async Task AssignToDepartmentAsync(int workScheduleId, int departmentId)
        {
            Trace.WriteLine($"[WorkSchedule Department Assignment] Starting - Assigning schedule ID: {workScheduleId} to department ID: {departmentId}");
            try
            {
                using var ctx = NewCtx();
                var workSchedule = await ctx.WorkSchedules.FindAsync(workScheduleId);
                if (workSchedule != null)
                {
                    // Get department info
                    var department = await ctx.Departments.FindAsync(departmentId);
                    if (department == null)
                    {
                        Trace.WriteLine($"[WorkSchedule Department Assignment] ERROR - Department ID: {departmentId} not found");
                        throw new KeyNotFoundException($"Department with ID {departmentId} not found");
                    }
                    
                    workSchedule.DepartmentId = departmentId;
                    workSchedule.Department = department; // Explicitly set the Department reference
                    await ctx.SaveChangesAsync();
                    
                    // Verify the assignment was successful
                    var updatedSchedule = await ctx.WorkSchedules
                        .Include(w => w.Department)
                        .FirstOrDefaultAsync(w => w.Id == workScheduleId);
                    
                    if (updatedSchedule != null && updatedSchedule.Department != null)
                    {
                        Trace.WriteLine($"[WorkSchedule Department Assignment] Verified assignment to department: {updatedSchedule.Department.Name}");
                    }
                    else
                    {
                        Trace.WriteLine($"[WorkSchedule Department Assignment] WARNING - Department reference still null after assignment");
                    }
                    
                    // Notify that departments and work schedules have changed
                    _dataRefreshService.NotifyDepartmentsChanged();
                    _dataRefreshService.NotifyWorkSchedulesChanged();
                    
                    string deptName = department.Name;
                    
                    Trace.WriteLine($"[WorkSchedule Department Assignment] Success - Assigned schedule ID: {workScheduleId} (Name: {workSchedule.Name}) to department ID: {departmentId} (Name: {deptName})");
                    
                    // Fetch all employees in this department for attendance recalculation
                    var departmentEmployees = await ctx.Employees
                        .Where(e => e.DepartmentId == departmentId)
                        .Select(e => e.Id)
                        .ToListAsync();
                    
                    Trace.WriteLine($"[WorkSchedule Department Assignment] Recalculating metrics for {departmentEmployees.Count} employees in department");
                    
                    // Recalculate attendance metrics for all employees in the department
                    // Recalculate for the last 30 days by default
                    var startDate = DateTime.Today.AddDays(-30);
                    await _attendanceService.RecalculateAttendanceMetricsForEmployeesAsync(departmentEmployees, startDate);
                }
                else
                {
                    Trace.WriteLine($"[WorkSchedule Department Assignment] ERROR - Schedule ID: {workScheduleId} not found");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[WorkSchedule Department Assignment] ERROR - Failed to assign schedule to department: {ex.Message}");
                throw;
            }
        }
        
        // Method to determine if a specific date is a working day for an employee
        public async Task<bool> IsWorkingDayForEmployeeAsync(int employeeId, DateTime date)
        {
            var schedule = await GetEmployeeWorkScheduleAsync(employeeId);
            return schedule?.IsWorkingDay(date.DayOfWeek) ?? false;
        }
        
        // Method to get expected work hours for an employee on a specific date
        public async Task<double> GetExpectedWorkHoursAsync(int employeeId, DateTime date)
        {
            var schedule = await GetEmployeeWorkScheduleAsync(employeeId);
            return schedule?.CalculateExpectedWorkHours(date) ?? 0;
        }
        
        // Method to get the expected check-in and check-out times with grace periods applied
        public async Task<(DateTime? LatestAllowedCheckIn, DateTime? EarliestAllowedCheckOut)> GetAllowedAttendanceTimesAsync(int employeeId, DateTime date)
        {
            var schedule = await GetEmployeeWorkScheduleAsync(employeeId);
            if (schedule == null || !schedule.IsWorkingDay(date.DayOfWeek))
            {
                return (null, null);
            }

            // Calculate the expected check-in and check-out times for the date
            var checkInTime = new DateTime(date.Year, date.Month, date.Day, 
                schedule.StartTime.Hours, schedule.StartTime.Minutes, 0);
                
            var checkOutTime = new DateTime(date.Year, date.Month, date.Day,
                schedule.EndTime.Hours, schedule.EndTime.Minutes, 0);
            
            // Apply the grace period
            var latestAllowedCheckIn = checkInTime.AddMinutes(schedule.FlexTimeAllowanceMinutes);
            var earliestAllowedCheckOut = checkOutTime.AddMinutes(-schedule.FlexTimeAllowanceMinutes);
            
            return (latestAllowedCheckIn, earliestAllowedCheckOut);
        }
    }
} 