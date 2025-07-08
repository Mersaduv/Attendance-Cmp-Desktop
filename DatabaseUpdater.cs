using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using System.Diagnostics;

namespace AttandenceDesktop
{
    public class DatabaseUpdater
    {
        public static void UpdateDatabase()
        {
            string dbPath = Path.Combine(AppContext.BaseDirectory, "TimeAttendance.db");
            
            if (!File.Exists(dbPath))
            {
                Trace.WriteLine("Database file not found. No update needed.");
                return;
            }
            
            try
            {
                Trace.WriteLine("Starting database update...");
                
                // Create a connection to the database
                string connectionString = $"Data Source={dbPath}";
                using (var connection = new SqliteConnection(connectionString))
                {
                    connection.Open();
                    UpdateDatabaseSchema(connection);
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error updating database: {ex.Message}");
                Trace.WriteLine(ex.StackTrace);
            }
        }

        public static void UpdateDatabaseSchema(SqliteConnection connection)
        {
            // Check if the columns already exist
            bool earlyArrivalExists = CheckColumnExists(connection, "Attendances", "IsEarlyArrival");
            bool earlyArrivalMinutesExists = CheckColumnExists(connection, "Attendances", "EarlyArrivalMinutes");
            bool attendanceCodeExists = CheckColumnExists(connection, "Attendances", "AttendanceCode");
            bool isFlexibleScheduleExists = CheckColumnExists(connection, "WorkSchedules", "IsFlexibleSchedule");
            bool totalWorkHoursExists = CheckColumnExists(connection, "WorkSchedules", "TotalWorkHours");
            bool attendanceIsFlexibleScheduleExists = CheckColumnExists(connection, "Attendances", "IsFlexibleSchedule");
            bool attendanceExpectedWorkHoursExists = CheckColumnExists(connection, "Attendances", "ExpectedWorkHours");
            bool employeeIsFlexibleHoursExists = CheckColumnExists(connection, "Employees", "IsFlexibleHours");
            bool employeeRequiredWorkHoursPerDayExists = CheckColumnExists(connection, "Employees", "RequiredWorkHoursPerDay");
            bool deviceCommunicationPasswordExists = CheckColumnExists(connection, "Devices", "CommunicationPassword");
            
            // Add columns if they don't exist
            if (!earlyArrivalExists)
            {
                Trace.WriteLine("Adding IsEarlyArrival column to Attendances table...");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "ALTER TABLE Attendances ADD COLUMN IsEarlyArrival INTEGER NOT NULL DEFAULT 0";
                    command.ExecuteNonQuery();
                }
            }
            
            if (!earlyArrivalMinutesExists)
            {
                Trace.WriteLine("Adding EarlyArrivalMinutes column to Attendances table...");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "ALTER TABLE Attendances ADD COLUMN EarlyArrivalMinutes TEXT";
                    command.ExecuteNonQuery();
                }
            }
            
            if (!attendanceCodeExists)
            {
                Trace.WriteLine("Adding AttendanceCode column to Attendances table...");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "ALTER TABLE Attendances ADD COLUMN AttendanceCode TEXT DEFAULT ''";
                    command.ExecuteNonQuery();
                }
            }
            
            // Add new columns for flexible schedules to WorkSchedules table
            if (!isFlexibleScheduleExists)
            {
                Trace.WriteLine("Adding IsFlexibleSchedule column to WorkSchedules table...");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "ALTER TABLE WorkSchedules ADD COLUMN IsFlexibleSchedule INTEGER NOT NULL DEFAULT 0";
                    command.ExecuteNonQuery();
                }
            }
            
            if (!totalWorkHoursExists)
            {
                Trace.WriteLine("Adding TotalWorkHours column to WorkSchedules table...");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "ALTER TABLE WorkSchedules ADD COLUMN TotalWorkHours REAL NOT NULL DEFAULT 8.0";
                    command.ExecuteNonQuery();
                }
            }
            
            // Add new columns for flexible schedules to Attendances table
            if (!attendanceIsFlexibleScheduleExists)
            {
                Trace.WriteLine("Adding IsFlexibleSchedule column to Attendances table...");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "ALTER TABLE Attendances ADD COLUMN IsFlexibleSchedule INTEGER NOT NULL DEFAULT 0";
                    command.ExecuteNonQuery();
                }
            }
            
            if (!attendanceExpectedWorkHoursExists)
            {
                Trace.WriteLine("Adding ExpectedWorkHours column to Attendances table...");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "ALTER TABLE Attendances ADD COLUMN ExpectedWorkHours REAL NOT NULL DEFAULT 0";
                    command.ExecuteNonQuery();
                }
            }
            
            // Add new columns for flexible hours to Employees table
            if (!employeeIsFlexibleHoursExists)
            {
                Trace.WriteLine("Adding IsFlexibleHours column to Employees table...");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "ALTER TABLE Employees ADD COLUMN IsFlexibleHours INTEGER NOT NULL DEFAULT 0";
                    command.ExecuteNonQuery();
                }
            }
            
            if (!employeeRequiredWorkHoursPerDayExists)
            {
                Trace.WriteLine("Adding RequiredWorkHoursPerDay column to Employees table...");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "ALTER TABLE Employees ADD COLUMN RequiredWorkHoursPerDay REAL NOT NULL DEFAULT 8.0";
                    command.ExecuteNonQuery();
                }
            }
            
            // Add CommunicationPassword column to Devices table
            if (!deviceCommunicationPasswordExists)
            {
                Trace.WriteLine("Adding CommunicationPassword column to Devices table...");
                Program.LogMessage("Adding CommunicationPassword column to Devices table...");
                using (var command = connection.CreateCommand())
                {
                    command.CommandText = "ALTER TABLE Devices ADD COLUMN CommunicationPassword TEXT DEFAULT '0'";
                    command.ExecuteNonQuery();
                }
            }
            
            Trace.WriteLine("Database update completed successfully.");
        }
        
        public void InitializeDatabase()
        {
            try
            {
                string dbPath = Path.Combine(AppContext.BaseDirectory, "TimeAttendance.db");
                bool exists = File.Exists(dbPath);
                
                if (exists)
                {
                    Program.LogMessage("Database file exists. Using existing database.");
                    
                    // Update the schema for existing database
                    string connectionString = $"Data Source={dbPath}";
                    using (var connection = new SqliteConnection(connectionString))
                    {
                        connection.Open();
                        UpdateDatabaseSchema(connection);
                    }
                }
                else
                {
                    Program.LogMessage("Creating new database with initial schema...");
                    using (var context = new ApplicationDbContextFactory().CreateDbContext(Array.Empty<string>()))
                    {
                        context.Database.EnsureCreated();
                        Program.LogMessage("Database created successfully.");
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogMessage($"Failed to initialize database: {ex.Message}");
                throw new ApplicationException("Failed to initialize database", ex);
            }
        }
        
        private static bool CheckColumnExists(SqliteConnection connection, string tableName, string columnName)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA table_info({tableName})";
                using (var reader = command.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        string name = reader.GetString(1);
                        if (name.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }
} 