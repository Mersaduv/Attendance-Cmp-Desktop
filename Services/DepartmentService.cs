using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Models;

namespace AttandenceDesktop.Services
{
    public class DepartmentService
    {
        private readonly Func<ApplicationDbContext> _contextFactory;
        private readonly DataRefreshService _dataRefreshService;

        public DepartmentService(
            Func<ApplicationDbContext> contextFactory,
            DataRefreshService? dataRefreshService = null) // Optional for design-time constructor
        {
            _contextFactory = contextFactory;
            _dataRefreshService = dataRefreshService;
        }

        // Helper method to obtain a fresh context instance
        private ApplicationDbContext NewCtx() => _contextFactory();

        public async Task<List<Department>> GetAllAsync()
        {
            using var ctx = NewCtx();
            return await ctx.Departments.OrderBy(d => d.Name).ToListAsync();
        }
        
        public async Task<Department?> GetByIdAsync(int id)
        {
            using var ctx = NewCtx();
            return await ctx.Departments.FindAsync(id);
        }
        
        public async Task<Department> CreateAsync(Department department)
        {
            using var ctx = NewCtx();
            ctx.Departments.Add(department);
            await ctx.SaveChangesAsync();
            
            // Notify that departments have changed
            _dataRefreshService?.NotifyDepartmentsChanged();
            
            return department;
        }
        
        public async Task<Department> UpdateAsync(Department department)
        {
            using var ctx = NewCtx();
            var existingDepartment = await ctx.Departments.FindAsync(department.Id);
            if (existingDepartment == null)
            {
                throw new KeyNotFoundException($"Department with ID {department.Id} not found");
            }
            
            // Update properties
            existingDepartment.Name = department.Name;
            
            // Save changes
            await ctx.SaveChangesAsync();
            
            // Notify that departments have changed
            _dataRefreshService?.NotifyDepartmentsChanged();
            
            return existingDepartment;
        }
        
        public async Task DeleteAsync(int id)
        {
            using var ctx = NewCtx();
            var department = await ctx.Departments.FindAsync(id);
            if (department != null)
            {
                ctx.Departments.Remove(department);
                await ctx.SaveChangesAsync();
                
                // Notify that departments have changed
                _dataRefreshService?.NotifyDepartmentsChanged();
            }
        }
    }
} 