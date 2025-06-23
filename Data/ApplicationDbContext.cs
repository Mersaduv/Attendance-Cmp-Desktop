// Removed Identity dependency for desktop application
// using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Models;
using System.IO;
using System;

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

        // Configure entity relationships and constraints
        
        // Configure Employee entity
        builder.Entity<Employee>()
            .HasOne(e => e.Department)
            .WithMany(d => d.Employees)
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.Entity<Employee>()
            .HasOne(e => e.WorkSchedule)
            .WithMany(w => w.Employees)
            .HasForeignKey(e => e.WorkScheduleId)
            .OnDelete(DeleteBehavior.Restrict);
        
        // Configure Department entity
        builder.Entity<Department>()
            .HasMany(d => d.Employees)
            .WithOne(e => e.Department)
            .HasForeignKey(e => e.DepartmentId);
        
        // Configure WorkSchedule entity
        builder.Entity<WorkSchedule>()
            .HasMany(w => w.Employees)
            .WithOne(e => e.WorkSchedule)
            .HasForeignKey(e => e.WorkScheduleId);
            
        // Configure Attendance entity
        builder.Entity<Attendance>()
            .HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Ensure IsOvertime is properly mapped
        builder.Entity<Attendance>()
            .Property(a => a.IsOvertime)
            .HasDefaultValue(false);
            
        // Ensure IsEarlyArrival is properly mapped
        builder.Entity<Attendance>()
            .Property(a => a.IsEarlyArrival)
            .HasDefaultValue(false);
            
        // Configure AttendanceCode property
        builder.Entity<Attendance>()
            .Property(a => a.AttendanceCode)
            .HasMaxLength(2)
            .HasDefaultValue("");
    }
    
    // Add static method to create a configured instance for use in services
    public static ApplicationDbContext Create()
    {
        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        var dbPath = Path.Combine(AppContext.BaseDirectory, "TimeAttendance.db");
        var connectionString = $"Data Source={dbPath}";
        optionsBuilder.UseSqlite(connectionString);
        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
