// Removed Identity dependency for desktop application
// using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Models;

namespace AttandenceDesktop.Data;

public class ApplicationDbContext : DbContext
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }
    
    public DbSet<Department> Departments { get; set; }
    public DbSet<Employee> Employees { get; set; }
    public DbSet<Attendance> Attendances { get; set; }
    public DbSet<WorkSchedule> WorkSchedules { get; set; }
    public DbSet<WorkCalendar> WorkCalendars { get; set; }
    
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        
        // Make DepartmentId nullable in WorkSchedule
        builder.Entity<WorkSchedule>()
            .Property(ws => ws.DepartmentId)
            .IsRequired(false);
        
        // Seed initial departments
        builder.Entity<Department>().HasData(
            new Department { Id = 1, Name = "HR" },
            new Department { Id = 2, Name = "IT" },
            new Department { Id = 3, Name = "Finance" },
            new Department { Id = 4, Name = "Operations" },
            new Department { Id = 5, Name = "Marketing" }
        );
        
        // Seed default work schedule (Standard 9-5)
        builder.Entity<WorkSchedule>().HasData(
            new WorkSchedule 
            { 
                Id = 1, 
                Name = "Standard 9-5",
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(17, 0, 0),
                IsWorkingDayMonday = true,
                IsWorkingDayTuesday = true,
                IsWorkingDayWednesday = true,
                IsWorkingDayThursday = true,
                IsWorkingDayFriday = true,
                IsWorkingDaySaturday = false,
                IsWorkingDaySunday = false,
                FlexTimeAllowanceMinutes = 15,
                Description = "Standard work schedule from 9 AM to 5 PM, Monday to Friday"
            }
        );
    }
}
