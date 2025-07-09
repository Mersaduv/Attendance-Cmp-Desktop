using AttandenceDesktop.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Threading.Tasks;

namespace AttandenceDesktop
{
    public static class DbTest
    {
        public static async Task TestDatabaseConnection()
        {
            try
            {
                Console.WriteLine("Testing database connection...");
                
                // Create database context
                string dbPath = Path.Combine(AppContext.BaseDirectory, "TimeAttendance.db");
                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseSqlite($"Data Source={dbPath}");
                
                Console.WriteLine($"Database path: {dbPath}");
                Console.WriteLine($"File exists: {File.Exists(dbPath)}");
                
                if (File.Exists(dbPath))
                {
                    Console.WriteLine($"File size: {new FileInfo(dbPath).Length} bytes");
                }
                
                using (var context = new ApplicationDbContext(optionsBuilder.Options))
                {
                    // Try to access departments table
                    var departments = await context.Departments.ToListAsync();
                    Console.WriteLine($"Successfully connected to database. Found {departments.Count} departments:");
                    
                    foreach (var dept in departments)
                    {
                        Console.WriteLine($"  - ID: {dept.Id}, Name: {dept.Name}");
                    }
                    
                    // Try to access employees table
                    var employees = await context.Employees.ToListAsync();
                    Console.WriteLine($"Found {employees.Count} employees");
                    
                    // Try to access work schedules table
                    var schedules = await context.WorkSchedules.ToListAsync();
                    Console.WriteLine($"Found {schedules.Count} work schedules");
                }
                
                Console.WriteLine("Database test completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR: {ex.Message}");
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