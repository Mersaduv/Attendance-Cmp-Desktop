using AttandenceDesktop.Data;
using AttandenceDesktop.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AttandenceDesktop.Services
{
    public class DeviceService
    {
        private readonly Func<ApplicationDbContext> _ctxFactory;
        private readonly DataRefreshService _refresh;
        public DeviceService(Func<ApplicationDbContext> ctxFactory, DataRefreshService refresh)
        {
            _ctxFactory = ctxFactory;
            _refresh = refresh;
        }

        private ApplicationDbContext New()=> _ctxFactory();

        public async Task<List<Device>> GetAllAsync()
        {
            using var ctx = New();
            return await ctx.Devices.ToListAsync();
        }

        public async Task AddAsync(Device d)
        {
            using var ctx = New();
            ctx.Devices.Add(d);
            await ctx.SaveChangesAsync();
            _refresh.NotifyDevicesChanged();
        }

        public async Task UpdateAsync(Device d)
        {
            using var ctx = New();
            ctx.Devices.Update(d);
            await ctx.SaveChangesAsync();
            _refresh.NotifyDevicesChanged();
        }

        public async Task DeleteAsync(int id)
        {
            using var ctx = New();
            var dev = await ctx.Devices.FindAsync(id);
            if (dev != null)
            {
                ctx.Devices.Remove(dev);
                await ctx.SaveChangesAsync();
                _refresh.NotifyDevicesChanged();
            }
        }

        public async Task<bool> ImportAttendanceFromJsonAsync(int deviceId, int employeeId = 0)
        {
            try
            {
                // Path to the JSON file
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"device_{deviceId}_attendance.json");
                
                if (!File.Exists(filePath))
                {
                    Program.LogMessage($"DeviceService: JSON file not found: {filePath}");
                    return false;
                }
                
                // Load JSON data
                string jsonContent = await File.ReadAllTextAsync(filePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (data == null)
                {
                    Program.LogMessage("DeviceService: Failed to deserialize JSON data");
                    return false;
                }
                
                // Get employee mapping to match user IDs from device
                var employees = await New().Employees.ToListAsync();
                var employeesByNumber = employees.Where(e => !string.IsNullOrEmpty(e.EmployeeNumber))
                    .ToDictionary(e => e.EmployeeNumber!, e => e);
                
                // Filter to specific employee if provided
                if (employeeId > 0)
                {
                    var employee = await New().Employees.FindAsync(employeeId);
                    if (employee != null && !string.IsNullOrEmpty(employee.EmployeeNumber))
                    {
                        employeesByNumber = new Dictionary<string, Employee> 
                        { 
                            { employee.EmployeeNumber, employee } 
                        };
                    }
                }
                
                int importedCount = 0;
                int skippedCount = 0;
                
                // Extract logs from JSON
                if (data.TryGetValue("logs", out var logsObj) && logsObj is JsonElement logsElement && logsElement.ValueKind == JsonValueKind.Array)
                {
                    var logs = logsElement.EnumerateArray().ToList();
                    
                    // Group logs by user ID and date
                    var groupedLogs = logs
                        .Select(log => new
                        {
                            UserId = log.GetProperty("userId").GetString() ?? "",
                            DateTime = ParseDateTime(log.GetProperty("dateTime").GetString() ?? DateTime.Now.ToString()),
                            InOutMode = log.GetProperty("inOutMode").GetInt32(),
                            Date = ParseDateTime(log.GetProperty("dateTime").GetString() ?? DateTime.Now.ToString()).Date
                        })
                        .GroupBy(log => new { log.UserId, log.Date });
                    
                    // Process each group to create or update attendance records
                    foreach (var group in groupedLogs)
                    {
                        var userId = group.Key.UserId;
                        var date = group.Key.Date;
                        
                        // Find matching employee
                        if (!employeesByNumber.TryGetValue(userId, out var employee))
                        {
                            Program.LogMessage($"DeviceService: No employee found for user ID: {userId}");
                            skippedCount++;
                            continue;
                        }
                        
                        // Check if an attendance record already exists for this employee and date
                        var existingAttendance = await New().Attendances
                            .FirstOrDefaultAsync(a => a.EmployeeId == employee.Id && a.Date == date);
                        
                        // Find check-in and check-out times
                        var checkIns = group.Where(l => l.InOutMode == 0).OrderBy(l => l.DateTime).ToList();
                        var checkOuts = group.Where(l => l.InOutMode == 1).OrderByDescending(l => l.DateTime).ToList();
                        
                        DateTime? checkInTime = checkIns.Any() ? checkIns.First().DateTime : null;
                        DateTime? checkOutTime = checkOuts.Any() ? checkOuts.First().DateTime : null;
                        
                        // Calculate work duration
                        TimeSpan? workDuration = null;
                        if (checkInTime.HasValue && checkOutTime.HasValue)
                        {
                            workDuration = checkOutTime.Value - checkInTime.Value;
                        }
                        
                        if (existingAttendance == null)
                        {
                            // Create a new attendance record
                            var attendance = new Attendance
                            {
                                EmployeeId = employee.Id,
                                Date = date,
                                CheckInTime = checkInTime,
                                CheckOutTime = checkOutTime,
                                WorkDuration = workDuration,
                                IsComplete = checkInTime.HasValue && checkOutTime.HasValue,
                                Notes = $"Imported from device {deviceId} JSON"
                            };
                            
                            New().Attendances.Add(attendance);
                            importedCount++;
                        }
                        else
                        {
                            // Update existing record
                            existingAttendance.CheckInTime = checkInTime ?? existingAttendance.CheckInTime;
                            existingAttendance.CheckOutTime = checkOutTime ?? existingAttendance.CheckOutTime;
                            existingAttendance.WorkDuration = workDuration ?? existingAttendance.WorkDuration;
                            existingAttendance.IsComplete = (checkInTime ?? existingAttendance.CheckInTime).HasValue && 
                                                           (checkOutTime ?? existingAttendance.CheckOutTime).HasValue;
                            existingAttendance.Notes += $"\nUpdated from device {deviceId} JSON";
                            
                            New().Attendances.Update(existingAttendance);
                            importedCount++;
                        }
                    }
                    
                    await New().SaveChangesAsync();
                    Program.LogMessage($"DeviceService: Imported {importedCount} attendance records, skipped {skippedCount}");
                    return true;
                }
                
                Program.LogMessage("DeviceService: No logs found in JSON data");
                return false;
            }
            catch (Exception ex)
            {
                Program.LogMessage($"DeviceService: Error importing JSON attendance data: {ex.Message}");
                return false;
            }
        }
        
        private DateTime ParseDateTime(string dateTimeString)
        {
            if (DateTime.TryParse(dateTimeString, out DateTime result))
            {
                return result;
            }
            return DateTime.Now;
        }
    }
} 