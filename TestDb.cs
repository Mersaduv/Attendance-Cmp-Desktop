using AttandenceDesktop.Data;
using AttandenceDesktop.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AttandenceDesktop
{
    public class TestDb
    {
        public static async Task RunTest()
        {
            try
            {
                Console.WriteLine("=== Database Test ===");
                
                // Create database context
                string dbPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TimeAttendance.db");
                Console.WriteLine($"Database path: {dbPath}");
                Console.WriteLine($"File exists: {File.Exists(dbPath)}");
                
                if (File.Exists(dbPath))
                {
                    Console.WriteLine($"File size: {new FileInfo(dbPath).Length} bytes");
                    
                    // Create context
                    var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                    optionsBuilder.UseSqlite($"Data Source={dbPath}");
                    
                    using (var context = new ApplicationDbContext(optionsBuilder.Options))
                    {
                        // Ensure database is created
                        context.Database.EnsureCreated();
                        
                        // Check if departments table exists and has data
                        var departments = await context.Departments.ToListAsync();
                        Console.WriteLine($"Found {departments.Count} departments");
                        
                        // If no departments, create some
                        if (departments.Count == 0)
                        {
                            Console.WriteLine("No departments found. Creating sample departments...");
                            
                            context.Departments.AddRange(
                                new Department { Name = "HR" },
                                new Department { Name = "IT" },
                                new Department { Name = "Finance" },
                                new Department { Name = "Operations" },
                                new Department { Name = "Marketing" }
                            );
                            
                            await context.SaveChangesAsync();
                            Console.WriteLine("Sample departments created successfully");
                            
                            // Verify departments were created
                            departments = await context.Departments.ToListAsync();
                            Console.WriteLine($"Now have {departments.Count} departments");
                        }
                        
                        // List all departments
                        Console.WriteLine("\nDepartments:");
                        foreach (var dept in departments)
                        {
                            Console.WriteLine($"  ID: {dept.Id}, Name: {dept.Name}");
                        }
                    }
                }
                else
                {
                    Console.WriteLine("Database file does not exist. Creating new database...");
                    
                    // Create database
                    var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                    optionsBuilder.UseSqlite($"Data Source={dbPath}");
                    
                    using (var context = new ApplicationDbContext(optionsBuilder.Options))
                    {
                        // Ensure database is created
                        context.Database.EnsureCreated();
                        Console.WriteLine("Database created successfully");
                        
                        // Add sample departments
                        context.Departments.AddRange(
                            new Department { Name = "HR" },
                            new Department { Name = "IT" },
                            new Department { Name = "Finance" },
                            new Department { Name = "Operations" },
                            new Department { Name = "Marketing" }
                        );
                        
                        await context.SaveChangesAsync();
                        Console.WriteLine("Sample departments created successfully");
                    }
                }
                
                Console.WriteLine("\nTest completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\nERROR: {ex.Message}");
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