using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Models;

namespace AttandenceDesktop.Services
{
    public class WorkScheduleService
    {
        private readonly Func<ApplicationDbContext> _contextFactory;

        public WorkScheduleService(Func<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
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
            using var ctx = NewCtx();
            var employee = await ctx.Employees
                .Include(e => e.WorkSchedule)
                .Include(e => e.Department)
                .ThenInclude(d => d.WorkSchedules)
                .FirstOrDefaultAsync(e => e.Id == employeeId);
                
            if (employee == null)
                return null;
                
            // If employee has specific schedule assigned, return it
            if (employee.WorkSchedule != null)
                return employee.WorkSchedule;
                
            // Otherwise, get department's default schedule
            var departmentSchedules = await ctx.WorkSchedules
                .Where(ws => ws.DepartmentId == employee.DepartmentId)
                .ToListAsync();
                
            var first = departmentSchedules.FirstOrDefault();
            if (first != null) return first;
            return await ctx.WorkSchedules.FirstOrDefaultAsync(ws => ws.Id == 1);
        }
        
        public async Task<WorkSchedule> CreateAsync(WorkSchedule workSchedule)
        {
            using var ctx = NewCtx();
            ctx.WorkSchedules.Add(workSchedule);
            await ctx.SaveChangesAsync();
            return workSchedule;
        }
        
        public async Task<WorkSchedule> UpdateAsync(WorkSchedule workSchedule)
        {
            using var ctx = NewCtx();
            var existingWorkSchedule = await ctx.WorkSchedules.FindAsync(workSchedule.Id);
            if (existingWorkSchedule == null)
            {
                throw new KeyNotFoundException($"Work Schedule with ID {workSchedule.Id} not found");
            }
            
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
            return existingWorkSchedule;
        }
        
        public async Task DeleteAsync(int id)
        {
            using var ctx = NewCtx();
            var workSchedule = await ctx.WorkSchedules.FindAsync(id);
            if (workSchedule != null)
            {
                ctx.WorkSchedules.Remove(workSchedule);
                await ctx.SaveChangesAsync();
            }
        }
        
        public async Task AssignToEmployeeAsync(int workScheduleId, int employeeId)
        {
            using var ctx = NewCtx();
            var employee = await ctx.Employees.FindAsync(employeeId);
            if (employee != null)
            {
                employee.WorkScheduleId = workScheduleId;
                await ctx.SaveChangesAsync();
            }
        }
        
        public async Task AssignToDepartmentAsync(int workScheduleId, int departmentId)
        {
            using var ctx = NewCtx();
            var workSchedule = await ctx.WorkSchedules.FindAsync(workScheduleId);
            if (workSchedule != null)
            {
                workSchedule.DepartmentId = departmentId;
                await ctx.SaveChangesAsync();
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
            
            // Calculate the expected check-in time for the day
            var expectedCheckInTime = new DateTime(
                date.Year, 
                date.Month, 
                date.Day,
                schedule.StartTime.Hours,
                schedule.StartTime.Minutes,
                0);
                
            // Calculate the expected check-out time for the day
            var expectedCheckOutTime = new DateTime(
                date.Year, 
                date.Month, 
                date.Day,
                schedule.EndTime.Hours,
                schedule.EndTime.Minutes,
                0);
                
            // Apply grace periods
            var latestAllowedCheckIn = expectedCheckInTime.AddMinutes(schedule.FlexTimeAllowanceMinutes);
            var earliestAllowedCheckOut = expectedCheckOutTime.AddMinutes(-schedule.FlexTimeAllowanceMinutes);
            
            return (latestAllowedCheckIn, earliestAllowedCheckOut);
        }
    }
} 