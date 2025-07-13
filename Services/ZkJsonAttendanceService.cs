using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using AttandenceDesktop.Models;

namespace AttandenceDesktop.Services
{
    /// <summary>
    /// Service for loading and processing attendance data from JSON files exported from ZK devices
    /// </summary>
    public class ZkJsonAttendanceService
    {
        private readonly EmployeeService _employeeService;
        private readonly DepartmentService _departmentService;

        public ZkJsonAttendanceService(
            EmployeeService employeeService,
            DepartmentService departmentService)
        {
            _employeeService = employeeService;
            _departmentService = departmentService;
        }

        /// <summary>
        /// Loads attendance data from JSON file for specified device and date range
        /// </summary>
        public async Task<List<AttendanceReportItem>> LoadAttendanceFromJsonAsync(int deviceId, DateTime startDate, DateTime endDate)
        {
            try
            {
                var result = new List<AttendanceReportItem>();
                
                // Path to the JSON file
                string filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"device_{deviceId}_attendance.json");
                
                if (!File.Exists(filePath))
                {
                    Program.LogMessage($"ZkJsonAttendanceService: JSON file not found: {filePath}");
                    return result;
                }
                
                // Load JSON data
                string jsonContent = await File.ReadAllTextAsync(filePath);
                var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(jsonContent, new JsonSerializerOptions 
                { 
                    PropertyNameCaseInsensitive = true 
                });
                
                if (data == null)
                {
                    Program.LogMessage("ZkJsonAttendanceService: Failed to deserialize JSON data");
                    return result;
                }
                
                // Extract logs from JSON
                if (data.TryGetValue("logs", out var logsElement) && logsElement.ValueKind == JsonValueKind.Array)
                {
                    var logs = logsElement.EnumerateArray().ToList();
                    
                    // Get all employees for mapping user IDs to names
                    var employees = await _employeeService.GetAllAsync();
                    var employeeDict = employees.ToDictionary(e => e.EmployeeNumber ?? "", e => e);
                    
                    Program.LogMessage($"ZkJsonAttendanceService: Processing {logs.Count} log entries");
                    
                    // Group logs by user ID and date
                    var groupedLogs = logs
                        .Select(log => new
                        {
                            UserId = log.GetProperty("userId").GetString() ?? "",
                            DateTime = ParseDateTime(log.GetProperty("dateTime").GetString() ?? DateTime.Now.ToString()),
                            InOutMode = log.GetProperty("inOutMode").GetInt32(),
                            InOutModeDescription = log.GetProperty("inOutModeDescription").GetString(),
                            VerifyMode = log.GetProperty("verifyMode").GetInt32(),
                            VerifyModeDescription = log.GetProperty("verifyModeDescription").GetString(),
                            Date = ParseDateTime(log.GetProperty("dateTime").GetString() ?? DateTime.Now.ToString()).Date
                        })
                        .Where(log => log.DateTime.Date >= startDate.Date && log.DateTime.Date <= endDate.Date)
                        .GroupBy(log => new { log.UserId, log.Date });
                    
                    // Process each group to create attendance report items
                    foreach (var group in groupedLogs)
                    {
                        var userId = group.Key.UserId;
                        var date = group.Key.Date;
                        
                        // Find matching employee
                        if (!employeeDict.TryGetValue(userId, out var employee))
                        {
                            Program.LogMessage($"ZkJsonAttendanceService: No employee found for user ID: {userId}");
                            continue;
                        }
                        
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
                        
                        // Create report item
                        var reportItem = new AttendanceReportItem
                        {
                            Date = date,
                            EmployeeId = employee.Id,
                            EmployeeName = employee.FullName,
                            DepartmentName = employee.Department?.Name ?? "Unknown",
                            CheckInTime = checkInTime,
                            CheckOutTime = checkOutTime,
                            WorkDuration = workDuration,
                            Status = DetermineAttendanceStatus(checkInTime, checkOutTime),
                            Notes = $"Data from device {deviceId}"
                        };
                        
                        result.Add(reportItem);
                    }
                }
                
                Program.LogMessage($"ZkJsonAttendanceService: Loaded {result.Count} attendance records from JSON");
                return result;
            }
            catch (Exception ex)
            {
                Program.LogMessage($"ZkJsonAttendanceService: Error loading JSON attendance data: {ex.Message}");
                return new List<AttendanceReportItem>();
            }
        }
        
        /// <summary>
        /// Gets a list of device IDs for which we have JSON attendance data
        /// </summary>
        public List<int> GetAvailableDeviceIds()
        {
            try
            {
                var deviceIds = new List<int>();
                var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                var files = Directory.GetFiles(baseDir, "device_*_attendance.json");
                
                foreach (var file in files)
                {
                    var fileName = Path.GetFileNameWithoutExtension(file);
                    var parts = fileName.Split('_');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int deviceId))
                    {
                        deviceIds.Add(deviceId);
                    }
                }
                
                return deviceIds;
            }
            catch (Exception ex)
            {
                Program.LogMessage($"ZkJsonAttendanceService: Error getting available device IDs: {ex.Message}");
                return new List<int>();
            }
        }
        
        /// <summary>
        /// Determines the attendance status based on check-in and check-out times
        /// </summary>
        private string DetermineAttendanceStatus(DateTime? checkIn, DateTime? checkOut)
        {
            if (checkIn.HasValue && checkOut.HasValue)
            {
                return "Present";
            }
            else if (checkIn.HasValue)
            {
                return "Checked In";
            }
            else
            {
                return "Absent";
            }
        }
        
        /// <summary>
        /// Safely parses a DateTime string
        /// </summary>
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