using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Models;

namespace AttandenceDesktop.Services
{
    public class DepartmentService
    {
        private readonly Func<ApplicationDbContext> _contextFactory;

        public DepartmentService(Func<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
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
            return department;
        }
        
        public async Task<Department> UpdateAsync(Department department)
        {
            using var ctx = NewCtx();
            ctx.Entry(department).State = EntityState.Modified;
            await ctx.SaveChangesAsync();
            return department;
        }
        
        public async Task DeleteAsync(int id)
        {
            using var ctx = NewCtx();
            var department = await ctx.Departments.FindAsync(id);
            if (department != null)
            {
                ctx.Departments.Remove(department);
                await ctx.SaveChangesAsync();
            }
        }
    }
} 