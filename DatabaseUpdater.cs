using AttandenceDesktop.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace AttandenceDesktop
{
    public static class DatabaseUpdater
    {
        public static async Task UpdateDatabaseAsync(Func<ApplicationDbContext> contextFactory)
        {
            try
            {
                using var context = contextFactory();
                var connectionString = context.Database.GetConnectionString();

                if (string.IsNullOrEmpty(connectionString))
                {
                    Program.LogMessage("Error: Connection string is null or empty");
                    return;
                }

                Program.LogMessage("Starting database update...");

                using var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync();

                // Check if the Privilege column exists in the Employees table
                bool privilegeExists = await ColumnExistsAsync(connection, "Employees", "Privilege");
                bool privilegeDescriptionExists = await ColumnExistsAsync(connection, "Employees", "PrivilegeDescription");
                bool leaveDaysExists = await ColumnExistsAsync(connection, "Employees", "LeaveDays");

                // Add the Privilege column if it doesn't exist
                if (!privilegeExists)
                {
                    Program.LogMessage("Adding Privilege column to Employees table...");
                    using var command = connection.CreateCommand();
                    command.CommandText = "ALTER TABLE Employees ADD COLUMN Privilege INTEGER NOT NULL DEFAULT 0";
                    await command.ExecuteNonQueryAsync();
                    Program.LogMessage("Privilege column added successfully");
                }

                // Add the PrivilegeDescription column if it doesn't exist
                if (!privilegeDescriptionExists)
                {
                    Program.LogMessage("Adding PrivilegeDescription column to Employees table...");
                    using var command = connection.CreateCommand();
                    command.CommandText = "ALTER TABLE Employees ADD COLUMN PrivilegeDescription TEXT NULL";
                    await command.ExecuteNonQueryAsync();
                    Program.LogMessage("PrivilegeDescription column added successfully");
                }

                // Add the LeaveDays column if it doesn't exist
                if (!leaveDaysExists)
                {
                    Program.LogMessage("Adding LeaveDays column to Employees table...");
                    using var command = connection.CreateCommand();
                    command.CommandText = "ALTER TABLE Employees ADD COLUMN LeaveDays INTEGER NOT NULL DEFAULT 2";
                    await command.ExecuteNonQueryAsync();
                    Program.LogMessage("LeaveDays column added successfully");
                }

                Program.LogMessage("Database update completed successfully");
            }
            catch (Exception ex)
            {
                Program.LogMessage($"Error updating database: {ex.Message}");
                Program.LogMessage($"Stack trace: {ex.StackTrace}");
            }
        }

        private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string tableName, string columnName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = $"PRAGMA table_info({tableName})";
            
            using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                string name = reader.GetString(1);
                if (name.Equals(columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            
            return false;
        }
    }
} 