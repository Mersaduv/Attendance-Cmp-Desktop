using Avalonia;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using AttandenceDesktop.Data;
using AttandenceDesktop.Services;
using Avalonia.Controls.ApplicationLifetimes;
using AttandenceDesktop.Models;

namespace AttandenceDesktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Setup logging
        string logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
        string logPath = Path.Combine(logDirectory, "app.log");
        
        // Ensure log directory exists
        Directory.CreateDirectory(logDirectory);
        
        // Create or clear the log file
        File.WriteAllText(logPath, $"=== Application Log Started at {DateTime.Now} ===\n\n");
        LogMessage("Application starting");
        
        try
        {
            // Check for debug direct seed mode - Special argument that directly calls seed method and exits
            if (args.Length > 0 && args[0].Equals("--debug-direct-seed", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("DEBUG MODE: Directly calling ClearAndCreateSampleData method...");
                LogMessage("DEBUG MODE: Directly calling ClearAndCreateSampleData method");
                try 
                {
                    SeedAttendanceData.ClearAndCreateSampleData();
                    Console.WriteLine("Seeding completed. Press any key to exit.");
                    Console.ReadKey();
                    return;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"ERROR: {ex.Message}");
                    Console.WriteLine(ex.StackTrace);
                    LogMessage($"ERROR in direct seed: {ex.Message}\n{ex.StackTrace}");
                    Console.WriteLine("Press any key to exit.");
                    Console.ReadKey();
                    return;
                }
            }
            
            // Global exception handlers
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                LogMessage($"[UnhandledException] {e.ExceptionObject}");
            };

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                LogMessage($"[UnobservedTaskException] {e.Exception}");
                e.SetObserved();
            };

            // Check for command line arguments
            if (args.Length > 0)
            {
                if (args[0].Equals("--regenerate-sample-data", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Regenerating sample data...");
                    LogMessage("Regenerating sample data from command line argument");
                    RegenerateSampleData();
                    Console.WriteLine("Sample data regenerated successfully. Press any key to start the application.");
                    Console.ReadKey();
                }
                else if (args[0].Equals("--clear-reseed-attendance", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Clearing and reseeding attendance data with all status types...");
                    LogMessage("Clearing and reseeding attendance data from command line argument");
                    ClearAndReseedAttendanceData();
                    Console.WriteLine("Attendance data cleared and reseeded successfully. Press any key to start the application.");
                    Console.ReadKey();
                }
            }

            LogMessage("Ensuring database exists");
            // Only create database if it doesn't exist
            EnsureDatabaseExists();
            
            LogMessage("Updating database schema if needed");
            // Update database schema if needed
            DatabaseUpdater.UpdateDatabase();
            
            LogMessage("Starting Avalonia application");
            BuildAvaloniaApp()
                .StartWithClassicDesktopLifetime(args, lifetime =>
                {
                    lifetime.ShutdownRequested += (sender, e) =>
                    {
                        // Perform any cleanup or saving operations here
                        LogMessage("Application shutting down...");
                    };
                });
        }
        catch (Exception ex)
        {
            LogMessage($"CRITICAL ERROR: {ex.Message}\n{ex.StackTrace}");
            throw;
        }
    }
    
    /// <summary>
    /// Ensures the database exists and creates it with seed data if it doesn't
    /// </summary>
    private static void EnsureDatabaseExists()
    {
        string dbPath = Path.Combine(AppContext.BaseDirectory, "TimeAttendance.db");
        
        // Only create the database if it doesn't exist
        if (!File.Exists(dbPath))
        {
            LogMessage("Database file not found. Creating new database with seed data...");
            
            // Create database structure
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            optionsBuilder.UseSqlite($"Data Source={dbPath}");
            using (var context = new ApplicationDbContext(optionsBuilder.Options))
            {
                // Create database and schema
                context.Database.EnsureCreated();
                
                // Add seed data
                SeedInitialData(context);
                
                context.SaveChanges();
            }
            
            // Add some sample data for first-time use
            SeedAttendanceData.CreateSampleAttendanceData();
        }
        else
        {
            LogMessage("Database file exists. Using existing database.");
        }
    }
    
    /// <summary>
    /// Seeds initial data in a new database
    /// </summary>
    private static void SeedInitialData(ApplicationDbContext context)
    {
        // Add departments if they don't exist
        if (!context.Departments.Any())
        {
            LogMessage("Adding seed departments...");
            context.Departments.AddRange(
                new Models.Department { Name = "HR" },
                new Models.Department { Name = "IT" },
                new Models.Department { Name = "Finance" },
                new Models.Department { Name = "Operations" },
                new Models.Department { Name = "Marketing" }
            );
        }
        
        // Add default work schedule if it doesn't exist
        if (!context.WorkSchedules.Any())
        {
            LogMessage("Adding seed work schedule...");
            context.WorkSchedules.Add(
                new Models.WorkSchedule
                {
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

    /// <summary>
    /// Regenerates sample attendance data
    /// </summary>
    private static void RegenerateSampleData()
    {
        try
        {
            SeedAttendanceData.CreateSampleAttendanceData();
        }
        catch (Exception ex)
        {
            LogMessage($"Error regenerating sample data: {ex}");
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Clears and reseeds attendance data with all status types (P, A, L, E, O, EA, W, H)
    /// </summary>
    private static void ClearAndReseedAttendanceData()
    {
        try
        {
            LogMessage("Clearing and reseeding attendance data with all status types");
            SeedAttendanceData.ClearAndCreateSampleData();
            LogMessage("Attendance data cleared and reseeded successfully");
        }
        catch (Exception ex)
        {
            LogMessage($"Error clearing and reseeding attendance data: {ex}");
            Console.WriteLine($"Error: {ex.Message}");
        }
    }

    // This method is for logging messages to the app.log file
    public static void LogMessage(string message)
    {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.log");
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string logMessage = $"[{timestamp}] {message}\n";
        
        try
        {
            File.AppendAllText(logPath, logMessage);
        }
        catch (Exception ex)
        {
            // If we can't log to the file, we can't do much about it
            Console.WriteLine($"Error writing to log file: {ex.Message}");
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
