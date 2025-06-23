using System;
using System.IO;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using System.Diagnostics;

namespace AttandenceDesktop
{
    public static class DatabaseUpdater
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
                    
                    // Check if the columns already exist
                    bool earlyArrivalExists = CheckColumnExists(connection, "Attendances", "IsEarlyArrival");
                    bool earlyArrivalMinutesExists = CheckColumnExists(connection, "Attendances", "EarlyArrivalMinutes");
                    bool attendanceCodeExists = CheckColumnExists(connection, "Attendances", "AttendanceCode");
                    
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
                    
                    Trace.WriteLine("Database update completed successfully.");
                }
            }
            catch (Exception ex)
            {
                Trace.WriteLine($"Error updating database: {ex.Message}");
                Trace.WriteLine(ex.StackTrace);
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