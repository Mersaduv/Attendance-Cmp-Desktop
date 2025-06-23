using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Diagnostics;

namespace AttandenceDesktop.Services
{
    public class EmployeeService
    {
        private readonly Func<ApplicationDbContext> _contextFactory;
        private readonly WorkScheduleService _workScheduleService;
        private readonly DataRefreshService _dataRefreshService;
        
        public EmployeeService(
            Func<ApplicationDbContext> contextFactory,
            WorkScheduleService workScheduleService,
            DataRefreshService dataRefreshService)
        {
            _contextFactory = contextFactory;
            _workScheduleService = workScheduleService;
            _dataRefreshService = dataRefreshService;
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
            Trace.WriteLine($"[Employee Creation] Starting - Employee: {employee.FirstName} {employee.LastName}, Code: {employee.EmployeeCode}, Department: {employee.DepartmentId}");
            try
            {
                using var ctx = NewCtx();
                ctx.Employees.Add(employee);
                await ctx.SaveChangesAsync();
                
                // Notify that employees have changed
                _dataRefreshService.NotifyEmployeesChanged();
                
                Trace.WriteLine($"[Employee Creation] Success - Created employee with ID: {employee.Id}, Name: {employee.FirstName} {employee.LastName}");
                return employee;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Employee Creation] ERROR - Failed to create employee: {ex.Message}");
                throw;
            }
        }
        
        public async Task<Employee> UpdateAsync(Employee employee)
        {
            Trace.WriteLine($"[Employee Update] Starting - Employee ID: {employee.Id}, Name: {employee.FirstName} {employee.LastName}");
            try
            {
                using var ctx = NewCtx();
                var existingEmployee = await ctx.Employees.FindAsync(employee.Id);
                if (existingEmployee == null)
                {
                    var errorMsg = $"Employee with ID {employee.Id} not found";
                    Trace.WriteLine($"[Employee Update] ERROR - {errorMsg}");
                    throw new KeyNotFoundException(errorMsg);
                }
                
                // Update properties
                existingEmployee.FirstName = employee.FirstName;
                existingEmployee.LastName = employee.LastName;
                existingEmployee.Email = employee.Email;
                existingEmployee.PhoneNumber = employee.PhoneNumber;
                existingEmployee.DepartmentId = employee.DepartmentId;
                existingEmployee.WorkScheduleId = employee.WorkScheduleId;
                existingEmployee.EmployeeCode = employee.EmployeeCode;
                existingEmployee.Position = employee.Position;
                existingEmployee.HireDate = employee.HireDate;
                
                // Save changes
                await ctx.SaveChangesAsync();
                
                // Notify that employees have changed
                _dataRefreshService.NotifyEmployeesChanged();
                
                Trace.WriteLine($"[Employee Update] Success - Updated employee ID: {employee.Id}");
                return existingEmployee;
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Employee Update] ERROR - Failed to update employee: {ex.Message}");
                throw;
            }
        }
        
        public async Task DeleteAsync(int id)
        {
            Trace.WriteLine($"[Employee Delete] Starting - Employee ID: {id}");
            try
            {
                using var ctx = NewCtx();
                var employee = await ctx.Employees.FindAsync(id);
                if (employee != null)
                {
                    ctx.Employees.Remove(employee);
                    await ctx.SaveChangesAsync();
                    
                    // Notify that employees have changed
                    _dataRefreshService.NotifyEmployeesChanged();
                    Trace.WriteLine($"[Employee Delete] Success - Deleted employee ID: {id}");
                }
                else
                {
                    Trace.WriteLine($"[Employee Delete] Warning - Employee ID: {id} not found");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"[Employee Delete] ERROR - Failed to delete employee: {ex.Message}");
                throw;
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