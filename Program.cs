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
using AttandenceDesktop.Views;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using AttandenceDesktop.ViewModels;
using Avalonia.Media;

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
        string logPath = Path.Combine(logDirectory, "app_log.txt");
        
        // Ensure log directory exists
        Directory.CreateDirectory(logDirectory);
        
        // Create or clear the log file
        File.WriteAllText(logPath, $"=== Application Log Started at {DateTime.Now} ===\n\n");
        LogMessage("Application starting");
        LogMessage($"Is64BitProcess: {Environment.Is64BitProcess}");
        
        // Try to setup ZKTeco DLLs
        SetupZkTecoDlls();
        
        try
        {
            // Direct database clear mode - special argument to clear database and exit
            if (args.Length > 0 && args[0].Equals("--direct-clear-database", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("====== DATABASE CLEAR UTILITY ======");
                Console.WriteLine("This utility will PERMANENTLY DELETE all data from the database.");
                Console.WriteLine("This includes all departments, employees, work schedules, and attendance records.");
                Console.WriteLine("The database schema will be preserved.");
                Console.WriteLine();
                Console.Write("Are you absolutely sure you want to proceed? (yes/no): ");
                string response = Console.ReadLine()?.ToLower() ?? "no";
                
                if (response == "yes")
                {
                    try
                    {
                        LogMessage("Direct database clear requested and confirmed");
                        
                        // Create database context directly
                        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                        var dbPath = Path.Combine(AppContext.BaseDirectory, "TimeAttendance.db");
                        var connectionString = $"Data Source={dbPath}";
                        optionsBuilder.UseSqlite(connectionString);
                        
                        using (var context = new ApplicationDbContext(optionsBuilder.Options))
                        {
                            // Clear each table in sequence
                            Console.WriteLine("\nRemoving attendance records...");
                            context.Attendances.RemoveRange(context.Attendances.ToList());
                            context.SaveChanges();
                            Console.WriteLine($"  ✓ {context.Attendances.Count()} attendance records remaining");
                            
                            Console.WriteLine("Removing employees...");
                            context.Employees.RemoveRange(context.Employees.ToList());
                            context.SaveChanges();
                            Console.WriteLine($"  ✓ {context.Employees.Count()} employees remaining");
                            
                            Console.WriteLine("Removing work schedules...");
                            context.WorkSchedules.RemoveRange(context.WorkSchedules.ToList());
                            context.SaveChanges();
                            Console.WriteLine($"  ✓ {context.WorkSchedules.Count()} work schedules remaining");
                            
                            Console.WriteLine("Removing work calendars...");
                            context.WorkCalendars.RemoveRange(context.WorkCalendars.ToList());
                            context.SaveChanges();
                            Console.WriteLine($"  ✓ {context.WorkCalendars.Count()} work calendars remaining");
                            
                            Console.WriteLine("Removing departments...");
                            context.Departments.RemoveRange(context.Departments.ToList());
                            context.SaveChanges();
                            Console.WriteLine($"  ✓ {context.Departments.Count()} departments remaining");
                            
                            Console.WriteLine("\n✅ Database cleared successfully!");
                            LogMessage("Database cleared successfully through direct utility");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"\n❌ ERROR: {ex.Message}");
                        LogMessage($"Error in direct database clear: {ex.Message}\n{ex.StackTrace}");
                    }
                }
                else
                {
                    Console.WriteLine("Operation cancelled.");
                    LogMessage("User cancelled the direct database clear operation");
                }
                
                Console.WriteLine("\nPress any key to exit...");
                Console.ReadKey();
                return;
            }
            
            // Check for debug direct seed mode - Special argument that directly calls seed method and exits
            if (args.Length > 0 && args[0].Equals("--debug-direct-seed", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("DEBUG MODE: This feature has been removed");
                LogMessage("DEBUG MODE: Sample data seeding feature has been removed");
                Console.WriteLine("Press any key to exit.");
                Console.ReadKey();
                return;
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
                    Console.WriteLine("This feature has been removed.");
                    LogMessage("Sample data regeneration feature has been removed");
                    Console.WriteLine("Press any key to start the application.");
                    Console.ReadKey();
                }
                else if (args[0].Equals("--clear-reseed-attendance", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("This feature has been removed.");
                    LogMessage("Clear and reseed attendance data feature has been removed");
                    Console.WriteLine("Press any key to start the application.");
                    Console.ReadKey();
                }
                else if (args[0].Equals("--clear-all-data", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Clearing all data from the database...");
                    Console.WriteLine("WARNING: This will remove ALL departments, employees, schedules, and attendance records.");
                    Console.Write("Do you want to continue? (y/n): ");
                    string response = Console.ReadLine()?.ToLower() ?? "n";
                    
                    if (response == "y" || response == "yes")
                    {
                        LogMessage("User confirmed to clear all data from the database");
                        ClearAllData();
                        Console.WriteLine("All data has been cleared successfully.");
                        Console.WriteLine("Press any key to start the application.");
                        Console.ReadKey();
                    }
                    else
                    {
                        Console.WriteLine("Operation cancelled by user.");
                        LogMessage("User cancelled the clear all data operation");
                        Console.WriteLine("Press any key to start the application.");
                        Console.ReadKey();
                    }
                }
                else if (args[0].Equals("--create-sample-data", StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("This feature has been removed.");
                    LogMessage("Sample data creation feature has been removed");
                    Console.WriteLine("Press any key to start the application.");
                    Console.ReadKey();
                }
            }

            LogMessage("Ensuring database exists");
            // Only create database if it doesn't exist
            EnsureDatabaseExists();
            
            LogMessage("Database ready");
            // Remove database updater call
            
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
        bool dbExists = File.Exists(dbPath);
        
        LogMessage($"Database path: {dbPath}, exists: {dbExists}");
        
        if (dbExists)
        {
            LogMessage("Database file exists, checking schema...");
            
            try
            {
                // Check if the database is accessible by opening a connection
                using (var db = ApplicationDbContext.Create())
                {
                    // Force a simple query to verify the database is valid
                    var departmentCount = db.Departments.Count();
                    LogMessage($"Database accessible, contains {departmentCount} departments");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error accessing database: {ex.Message}");
                LogMessage("Database exists but may be corrupt or incompatible. Will attempt to recreate it.");
                
                try
                {
                    // Backup the old database before recreating
                    string backupPath = $"{dbPath}.bak_{DateTime.Now:yyyyMMdd_HHmmss}";
                    File.Copy(dbPath, backupPath);
                    LogMessage($"Created backup of database at {backupPath}");
                    
                    // Delete the corrupt database
                    File.Delete(dbPath);
                    LogMessage("Deleted corrupt database");
                    
                    // Set dbExists to false to recreate the database
                    dbExists = false;
                }
                catch (Exception backupEx)
                {
                    LogMessage($"Error backing up/deleting corrupt database: {backupEx.Message}");
                    throw new Exception("Unable to repair database. Please delete TimeAttendance.db manually and restart the application.", ex);
                }
            }
        }
        
        if (!dbExists)
        {
            LogMessage("Database does not exist, creating...");
            
            try
            {
                using (var db = ApplicationDbContext.Create())
                {
                    // This will create the database and schema
                    db.Database.EnsureCreated();
                    LogMessage("Database created successfully");
                    
                    // Seed initial data
                    SeedInitialData(db);
                    LogMessage("Initial data seeded");
                }
            }
            catch (Exception ex)
            {
                LogMessage($"Error creating database: {ex.Message}");
                throw new Exception("Failed to initialize database. Check your SQLite connection and restart the application.", ex);
            }
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
                new Models.Department { Name = "Marketing" },
                new Models.Department { Name = "Remote Workers" }
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
            
            // Add a flexible schedule that only requires total hours
            context.WorkSchedules.Add(
                new Models.WorkSchedule
                {
                    Name = "Flexible Hours",
                    IsFlexibleSchedule = true,
                    StartTime = new TimeSpan(0, 0, 0), // Not used
                    EndTime = new TimeSpan(0, 0, 0),   // Not used
                    TotalWorkHours = 8.0,              // 8 hours total required
                    IsWorkingDayMonday = true,
                    IsWorkingDayTuesday = true,
                    IsWorkingDayWednesday = true,
                    IsWorkingDayThursday = true,
                    IsWorkingDayFriday = true,
                    IsWorkingDaySaturday = false,
                    IsWorkingDaySunday = false,
                    FlexTimeAllowanceMinutes = 0,      // No grace period for flexible schedules
                    Description = "Flexible work schedule with 8 hours total per day, no specific start/end times"
                }
            );
        }
    }

    /// <summary>
    /// Clears all data from the database without creating new sample data
    /// </summary>
    private static void ClearAllData()
    {
        try
        {
            LogMessage("Clearing all data from the database");
            Console.WriteLine("Starting to clear database...");
            
            // Create a new database context
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            var dbPath = Path.Combine(AppContext.BaseDirectory, "TimeAttendance.db");
            var connectionString = $"Data Source={dbPath}";
            optionsBuilder.UseSqlite(connectionString);
            
            using (var context = new ApplicationDbContext(optionsBuilder.Options))
            {
                // Clear ALL existing data
                Console.WriteLine("Removing all attendance records...");
                context.Attendances.RemoveRange(context.Attendances.ToList());
                context.SaveChanges();
                Console.WriteLine($"  ✓ {context.Attendances.Count()} attendance records remaining");
                
                Console.WriteLine("Removing all employees...");
                context.Employees.RemoveRange(context.Employees.ToList());
                context.SaveChanges();
                Console.WriteLine($"  ✓ {context.Employees.Count()} employees remaining");
                
                Console.WriteLine("Removing all work schedules...");
                context.WorkSchedules.RemoveRange(context.WorkSchedules.ToList());
                context.SaveChanges();
                Console.WriteLine($"  ✓ {context.WorkSchedules.Count()} work schedules remaining");
                
                Console.WriteLine("Removing all work calendars...");
                context.WorkCalendars.RemoveRange(context.WorkCalendars.ToList());
                context.SaveChanges();
                Console.WriteLine($"  ✓ {context.WorkCalendars.Count()} work calendars remaining");
                
                Console.WriteLine("Removing all departments...");
                context.Departments.RemoveRange(context.Departments.ToList());
                context.SaveChanges();
                Console.WriteLine($"  ✓ {context.Departments.Count()} departments remaining");
                
                Console.WriteLine("\n✅ All data has been cleared successfully!");
                LogMessage("All data has been cleared successfully");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error clearing data: {ex}");
            Console.WriteLine($"Error: {ex.Message}");
        }
    }



    // This method is for logging messages to the app.log file
    public static void LogMessage(string message)
    {
        string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_log.txt");
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

    /// <summary>
    /// Attempts to set up the ZKTeco DLLs by looking for them in common locations and copying to the native folders
    /// </summary>
    private static void SetupZkTecoDlls()
    {
        try
        {
            LogMessage("Setting up ZKTeco DLLs...");
            LogMessage($"Process is running as {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}");
            
            // Create native directories if they don't exist
            string x86Dir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "native", "x86");
            if (!Directory.Exists(x86Dir)) Directory.CreateDirectory(x86Dir);
            
            // For 32-bit mode, we only need the x86 folder
            LogMessage($"Native x86 directory: {x86Dir}");
            
            // Look for zkemkeeper.dll in common locations
            string[] searchPaths = new[] {
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SDK"),
                @"..\connectTest\GhalibHRAttendance\SDK",
                @"D:\config\Fingerprint\sdk",
                @"C:\Program Files\ZKTeco\ZKBioTime\sdk",
                @"C:\Program Files (x86)\ZKTeco\ZKBioTime\sdk",
                AppDomain.CurrentDomain.BaseDirectory
            };
            
            bool foundDll = false;
            foreach (var path in searchPaths)
            {
                if (!Directory.Exists(path)) continue;
                
                LogMessage($"Checking for ZKTeco DLLs in {path}");
                
                string zkemkeeperDll = Path.Combine(path, "zkemkeeper.dll");
                if (File.Exists(zkemkeeperDll))
                {
                    LogMessage($"Found zkemkeeper.dll at {zkemkeeperDll}");
                    foundDll = true;
                    
                    try
                    {
                        // Copy to native folder - we're using 32-bit mode so only copy to x86
                        File.Copy(zkemkeeperDll, Path.Combine(x86Dir, "zkemkeeper.dll"), true);
                        LogMessage("Copied zkemkeeper.dll to native x86 directory");
                        
                        // Copy any other DLLs in the same directory
                        foreach (var dll in Directory.GetFiles(path, "*.dll"))
                        {
                            string fileName = Path.GetFileName(dll);
                            if (fileName != "zkemkeeper.dll")
                            {
                                File.Copy(dll, Path.Combine(x86Dir, fileName), true);
                                LogMessage($"Copied {fileName} to native x86 directory");
                            }
                        }
                        
                        // Also ensure the DLLs are in the application directory
                        File.Copy(zkemkeeperDll, Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "zkemkeeper.dll"), true);
                        LogMessage("Copied zkemkeeper.dll to application directory");
                        
                        // Update application PATH environment variable
                        try 
                        {
                            string currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
                            if (!currentPath.Contains(x86Dir))
                            {
                                Environment.SetEnvironmentVariable("PATH", $"{x86Dir};{currentPath}");
                                LogMessage("Added x86 directory to PATH environment variable");
                            }
                        }
                        catch (Exception ex)
                        {
                            LogMessage($"Error updating PATH: {ex.Message}");
                        }
                        
                        break;
                    }
                    catch (Exception ex)
                    {
                        LogMessage($"Error copying DLLs: {ex.Message}");
                    }
                }
            }
            
            if (!foundDll)
            {
                LogMessage("WARNING: Could not find zkemkeeper.dll in any of the search paths");
            }
        }
        catch (Exception ex)
        {
            LogMessage($"Error setting up ZKTeco DLLs: {ex.Message}");
        }
    }
    
    private static void RunRegsvr32(string regsvr32Path, string dllPath)
    {
        try
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = regsvr32Path,
                    Arguments = $"/s \"{dllPath}\"",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true
                }
            };
            
            process.Start();
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            
            if (!string.IsNullOrEmpty(output))
            {
                LogMessage($"regsvr32 output: {output}");
            }
            
            if (!string.IsNullOrEmpty(error))
            {
                LogMessage($"regsvr32 error: {error}");
            }
            
            LogMessage($"regsvr32 exit code: {process.ExitCode}");
        }
        catch (Exception ex)
        {
            LogMessage($"Error running regsvr32: {ex.Message}");
        }
    }

    private static void ConfigureServices(ServiceCollection services)
    {
        LogMessage("Configuring services");
        
        // Database factory
        services.AddSingleton<Func<ApplicationDbContext>>(() => new ApplicationDbContextFactory().CreateDbContext(Array.Empty<string>()));
        
        // Core services
        services.AddSingleton<DataRefreshService>();
        services.AddSingleton<WorkScheduleService>();
        services.AddSingleton<DepartmentService>();
        services.AddSingleton<EmployeeService>();
        services.AddSingleton<DeviceService>();
        services.AddSingleton<AttendanceService>();
        services.AddSingleton<WorkCalendarService>();
        services.AddSingleton<ReportService>();
        services.AddSingleton<PrintingService>();
        services.AddSingleton<ExportService>();
        services.AddSingleton<ReportGridExportService>();
        services.AddSingleton<DeviceSyncService>();
        
        // View models
        services.AddTransient<MainWindowViewModel>();
        services.AddTransient<DashboardViewModel>();
        services.AddTransient<EmployeeViewModel>();
        services.AddTransient<DepartmentViewModel>();
        services.AddTransient<WorkScheduleViewModel>();
        services.AddTransient<WorkCalendarViewModel>();
        services.AddTransient<AttendanceViewModel>();
        services.AddTransient<DeviceViewModel>();
        services.AddTransient<ReportViewModel>();
        services.AddTransient<OverviewReportViewModel>();
        
        LogMessage("Services configured");
    }
}
