using AttandenceDesktop.Data;
using AttandenceDesktop.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AttandenceDesktop
{
    public static class CreateDatabase
    {
        public static async Task Create()
        {
            try
            {
                Console.WriteLine("Creating database...");
                
                // Get the output directory
                string outputDir = AppContext.BaseDirectory;
                Console.WriteLine($"Output directory: {outputDir}");
                
                // Create the database path
                string dbPath = Path.Combine(outputDir, "TimeAttendance.db");
                Console.WriteLine($"Database path: {dbPath}");
                
                // Create the database context
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
                
                using (var context = new ApplicationDbContext(optionsBuilder.Options))
                {
                    // Ensure the database is created
                    await context.Database.EnsureCreatedAsync();
                    Console.WriteLine("Database created successfully");
                    
                    // Check if departments exist
                    var deptCount = await context.Departments.CountAsync();
                    if (deptCount == 0)
                    {
                        Console.WriteLine("Adding departments...");
                        context.Departments.AddRange(
                            new Department { Name = "HR" },
                            new Department { Name = "IT" },
                            new Department { Name = "Finance" },
                            new Department { Name = "Operations" },
                            new Department { Name = "Marketing" }
                        );
                        await context.SaveChangesAsync();
                        Console.WriteLine("Departments added successfully");
                    }
                    else
                    {
                        Console.WriteLine($"Database already has {deptCount} departments");
                    }
                    
                    // Check if work schedules exist
                    var scheduleCount = await context.WorkSchedules.CountAsync();
                    if (scheduleCount == 0)
                    {
                        Console.WriteLine("Adding default work schedule...");
                        context.WorkSchedules.Add(
                            new WorkSchedule 
                            { 
                                Name = "Standard 9-5",
                                IsFlexibleSchedule = false,
                                StartTime = new TimeSpan(9, 0, 0),
                                EndTime = new TimeSpan(17, 0, 0),
                                TotalWorkHours = 8.0,
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
                        await context.SaveChangesAsync();
                        Console.WriteLine("Default work schedule added successfully");
                    }
                    else
                    {
                        Console.WriteLine($"Database already has {scheduleCount} work schedules");
                    }
                }
                
                Console.WriteLine("Database setup completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating database: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                    Console.WriteLine($"Inner stack trace: {ex.InnerException.StackTrace}");
                }
            }
        }
    }
} 