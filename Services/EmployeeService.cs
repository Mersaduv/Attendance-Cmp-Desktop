using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Models;

namespace AttandenceDesktop.Services
{
    public class EmployeeService
    {
        private readonly Func<ApplicationDbContext> _contextFactory;
        private readonly WorkScheduleService _workScheduleService;
        
        public EmployeeService(Func<ApplicationDbContext> contextFactory, WorkScheduleService workScheduleService)
        {
            _contextFactory = contextFactory;
            _workScheduleService = workScheduleService;
        }
        
        private ApplicationDbContext NewCtx() => _contextFactory();
        
        public async Task<List<Employee>> GetAllAsync()
        {
            using var ctx = NewCtx();
            return await ctx.Employees
                .Include(e => e.Department)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }
        
        public async Task<Employee> GetByIdAsync(int id)
        {
            using var ctx = NewCtx();
            return await ctx.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == id);
        }
        
        public async Task<List<Employee>> GetByDepartmentAsync(int departmentId)
        {
            using var ctx = NewCtx();
            return await ctx.Employees
                .Where(e => e.DepartmentId == departmentId)
                .OrderBy(e => e.LastName)
                .ThenBy(e => e.FirstName)
                .ToListAsync();
        }
        
        public async Task<Employee> CreateAsync(Employee employee)
        {
            // Check if employee code is unique
            if (!await IsEmployeeCodeUnique(employee.EmployeeCode))
            {
                throw new InvalidOperationException($"Employee code '{employee.EmployeeCode}' already exists. Please use a unique code.");
            }
            
            // Get the department's work schedule if it exists
            var departmentSchedules = await _workScheduleService.GetByDepartmentAsync(employee.DepartmentId);
            var departmentSchedule = departmentSchedules.FirstOrDefault();
            
            // Assign the department's work schedule to the employee if available, otherwise use the default schedule
            if (departmentSchedule != null)
            {
                employee.WorkScheduleId = departmentSchedule.Id;
            }
            else
            {
                // Get default schedule (ID = 1) if department has no specific schedule
                using var ctx = NewCtx();
                var defaultSchedule = await ctx.WorkSchedules.FindAsync(1);
                if (defaultSchedule != null)
                {
                    employee.WorkScheduleId = defaultSchedule.Id;
                }
            }
            
            using (var ctx2 = NewCtx())
            {
                ctx2.Employees.Add(employee);
                await ctx2.SaveChangesAsync();
            }
            return employee;
        }
        
        public async Task<Employee> UpdateAsync(Employee employee)
        {
            // Check if employee code is unique
            if (!await IsEmployeeCodeUnique(employee.EmployeeCode, employee.Id))
            {
                throw new InvalidOperationException($"Employee code '{employee.EmployeeCode}' already exists. Please use a unique code.");
            }
            
            using var ctx = NewCtx();
            var existingEmployee = await ctx.Employees.FindAsync(employee.Id);
            if (existingEmployee == null)
            {
                throw new KeyNotFoundException($"Employee with ID {employee.Id} not found");
            }
            
            // Update properties
            existingEmployee.FirstName = employee.FirstName;
            existingEmployee.LastName = employee.LastName;
            existingEmployee.Email = employee.Email;
            existingEmployee.PhoneNumber = employee.PhoneNumber;
            existingEmployee.Position = employee.Position;
            existingEmployee.EmployeeCode = employee.EmployeeCode;
            existingEmployee.DepartmentId = employee.DepartmentId;
            existingEmployee.HireDate = employee.HireDate;
            
            // Get the department's work schedule if it exists
            var departmentSchedules = await _workScheduleService.GetByDepartmentAsync(employee.DepartmentId);
            var departmentSchedule = departmentSchedules.FirstOrDefault();
            
            // Assign the department's work schedule to the employee if available, otherwise use the default schedule
            if (departmentSchedule != null)
            {
                existingEmployee.WorkScheduleId = departmentSchedule.Id;
            }
            else
            {
                // Get default schedule (ID = 1) if department has no specific schedule
                var defaultSchedule = await ctx.WorkSchedules.FindAsync(1);
                if (defaultSchedule != null)
                {
                    existingEmployee.WorkScheduleId = defaultSchedule.Id;
                }
            }
            
            // Save changes
            await ctx.SaveChangesAsync();
            return existingEmployee;
        }
        
        public async Task DeleteAsync(int id)
        {
            using var ctx = NewCtx();
            var employee = await ctx.Employees.FindAsync(id);
            if (employee != null)
            {
                ctx.Employees.Remove(employee);
                await ctx.SaveChangesAsync();
            }
        }
        
        // Check if employee code is unique
        public async Task<bool> IsEmployeeCodeUnique(string employeeCode, int employeeId = 0)
        {
            using var ctx = NewCtx();
            if (employeeId > 0)
            {
                return !await ctx.Employees
                    .AnyAsync(e => e.EmployeeCode == employeeCode && e.Id != employeeId);
            }
            return !await ctx.Employees.AnyAsync(e => e.EmployeeCode == employeeCode);
        }
    }
} 