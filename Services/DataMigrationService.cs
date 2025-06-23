using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace AttandenceDesktop.Services
{
    /// <summary>
    /// Service to help migrate data from SQL Server to SQLite
    /// </summary>
    public class DataMigrationService
    {
        private readonly Func<ApplicationDbContext> _contextFactory;
        
        public DataMigrationService(Func<ApplicationDbContext> contextFactory)
        {
            _contextFactory = contextFactory;
        }
        
        /// <summary>
        /// Imports data from a SQL Server export file (CSV format)
        /// </summary>
        public async Task<bool> ImportFromCsv(string filePath, string entityType)
        {
            try
            {
                using var ctx = _contextFactory();
                
                if (!File.Exists(filePath))
                {
                    throw new FileNotFoundException("Import file not found", filePath);
                }
                
                var lines = await File.ReadAllLinesAsync(filePath);
                if (lines.Length <= 1) // Just header or empty
                {
                    return true; // Nothing to import
                }
                
                // Skip header row
                var dataRows = lines.Skip(1).ToArray();
                
                switch (entityType.ToLower())
                {
                    case "departments":
                        await ImportDepartments(ctx, dataRows);
                        break;
                    case "employees":
                        await ImportEmployees(ctx, dataRows);
                        break;
                    case "workschedules":
                        await ImportWorkSchedules(ctx, dataRows);
                        break;
                    case "attendances":
                        await ImportAttendances(ctx, dataRows);
                        break;
                    case "workcalendars":
                        await ImportWorkCalendars(ctx, dataRows);
                        break;
                    default:
                        throw new ArgumentException($"Unknown entity type: {entityType}");
                }
                
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error importing data: {ex.Message}");
                throw;
            }
        }
        
        private async Task ImportDepartments(ApplicationDbContext ctx, string[] rows)
        {
            foreach (var row in rows)
            {
                var columns = ParseCsvRow(row);
                if (columns.Length < 2) continue;
                
                int.TryParse(columns[0], out int id);
                var name = columns[1];
                
                if (id > 0 && !string.IsNullOrEmpty(name))
                {
                    if (!await ctx.Departments.AnyAsync(d => d.Id == id))
                    {
                        ctx.Departments.Add(new Department
                        {
                            Id = id,
                            Name = name
                        });
                    }
                }
            }
            
            await ctx.SaveChangesAsync();
        }
        
        private async Task ImportEmployees(ApplicationDbContext ctx, string[] rows)
        {
            foreach (var row in rows)
            {
                var columns = ParseCsvRow(row);
                if (columns.Length < 8) continue;
                
                int.TryParse(columns[0], out int id);
                var firstName = columns[1];
                var lastName = columns[2];
                var email = columns[3];
                var phone = columns[4];
                DateTime.TryParse(columns[5], out DateTime hireDate);
                var position = columns[6];
                var code = columns[7];
                int.TryParse(columns[8], out int deptId);
                int.TryParse(columns[9], out int scheduleId);
                
                if (id > 0 && !string.IsNullOrEmpty(firstName) && !string.IsNullOrEmpty(lastName))
                {
                    if (!await ctx.Employees.AnyAsync(e => e.Id == id))
                    {
                        ctx.Employees.Add(new Employee
                        {
                            Id = id,
                            FirstName = firstName,
                            LastName = lastName,
                            Email = email,
                            PhoneNumber = phone,
                            HireDate = hireDate,
                            Position = position,
                            EmployeeCode = code,
                            DepartmentId = deptId,
                            WorkScheduleId = scheduleId > 0 ? scheduleId : null
                        });
                    }
                }
            }
            
            await ctx.SaveChangesAsync();
        }
        
        private async Task ImportWorkSchedules(ApplicationDbContext ctx, string[] rows)
        {
            foreach (var row in rows)
            {
                var columns = ParseCsvRow(row);
                if (columns.Length < 12) continue;
                
                int.TryParse(columns[0], out int id);
                var name = columns[1];
                TimeSpan.TryParse(columns[2], out TimeSpan startTime);
                TimeSpan.TryParse(columns[3], out TimeSpan endTime);
                bool.TryParse(columns[4], out bool isSunday);
                bool.TryParse(columns[5], out bool isMonday);
                bool.TryParse(columns[6], out bool isTuesday);
                bool.TryParse(columns[7], out bool isWednesday);
                bool.TryParse(columns[8], out bool isThursday);
                bool.TryParse(columns[9], out bool isFriday);
                bool.TryParse(columns[10], out bool isSaturday);
                int.TryParse(columns[11], out int flexTime);
                var description = columns.Length > 12 ? columns[12] : "";
                int.TryParse(columns.Length > 13 ? columns[13] : "0", out int deptId);
                
                if (id > 0 && !string.IsNullOrEmpty(name))
                {
                    if (!await ctx.WorkSchedules.AnyAsync(ws => ws.Id == id))
                    {
                        ctx.WorkSchedules.Add(new WorkSchedule
                        {
                            Id = id,
                            Name = name,
                            StartTime = startTime,
                            EndTime = endTime,
                            IsWorkingDaySunday = isSunday,
                            IsWorkingDayMonday = isMonday,
                            IsWorkingDayTuesday = isTuesday,
                            IsWorkingDayWednesday = isWednesday,
                            IsWorkingDayThursday = isThursday,
                            IsWorkingDayFriday = isFriday,
                            IsWorkingDaySaturday = isSaturday,
                            FlexTimeAllowanceMinutes = flexTime,
                            Description = description,
                            DepartmentId = deptId > 0 ? deptId : null
                        });
                    }
                }
            }
            
            await ctx.SaveChangesAsync();
        }
        
        private async Task ImportAttendances(ApplicationDbContext ctx, string[] rows)
        {
            foreach (var row in rows)
            {
                var columns = ParseCsvRow(row);
                if (columns.Length < 5) continue;
                
                int.TryParse(columns[0], out int id);
                int.TryParse(columns[1], out int employeeId);
                DateTime.TryParse(columns[2], out DateTime date);
                DateTime? checkIn = null;
                if (DateTime.TryParse(columns[3], out DateTime checkInDt))
                {
                    checkIn = checkInDt;
                }
                
                DateTime? checkOut = null;
                if (DateTime.TryParse(columns[4], out DateTime checkOutDt))
                {
                    checkOut = checkOutDt;
                }
                
                var notes = columns.Length > 5 ? columns[5] : "";
                
                TimeSpan? workDuration = null;
                if (TimeSpan.TryParse(columns.Length > 6 ? columns[6] : null, out TimeSpan duration))
                {
                    workDuration = duration;
                }
                
                bool.TryParse(columns.Length > 7 ? columns[7] : "false", out bool isComplete);
                bool.TryParse(columns.Length > 8 ? columns[8] : "false", out bool isLate);
                bool.TryParse(columns.Length > 9 ? columns[9] : "false", out bool isEarly);
                
                TimeSpan? lateMinutes = null;
                if (TimeSpan.TryParse(columns.Length > 10 ? columns[10] : null, out TimeSpan late))
                {
                    lateMinutes = late;
                }
                
                TimeSpan? earlyMinutes = null;
                if (TimeSpan.TryParse(columns.Length > 11 ? columns[11] : null, out TimeSpan early))
                {
                    earlyMinutes = early;
                }
                
                TimeSpan? overtimeMinutes = null;
                if (TimeSpan.TryParse(columns.Length > 12 ? columns[12] : null, out TimeSpan overtime))
                {
                    overtimeMinutes = overtime;
                }
                
                if (id > 0 && employeeId > 0)
                {
                    if (!await ctx.Attendances.AnyAsync(a => a.Id == id))
                    {
                        ctx.Attendances.Add(new Attendance
                        {
                            Id = id,
                            EmployeeId = employeeId,
                            Date = date,
                            CheckInTime = checkIn,
                            CheckOutTime = checkOut,
                            Notes = notes,
                            WorkDuration = workDuration,
                            IsComplete = isComplete,
                            IsLateArrival = isLate,
                            IsEarlyDeparture = isEarly,
                            LateMinutes = lateMinutes,
                            EarlyDepartureMinutes = earlyMinutes,
                            OvertimeMinutes = overtimeMinutes
                        });
                    }
                }
            }
            
            await ctx.SaveChangesAsync();
        }
        
        private async Task ImportWorkCalendars(ApplicationDbContext ctx, string[] rows)
        {
            foreach (var row in rows)
            {
                var columns = ParseCsvRow(row);
                if (columns.Length < 6) continue;
                
                int.TryParse(columns[0], out int id);
                DateTime.TryParse(columns[1], out DateTime calendarDate);
                int.TryParse(columns[2], out int dayType);
                var description = columns[3];
                var name = columns[4];
                int.TryParse(columns[5], out int entryTypeInt);
                bool.TryParse(columns.Length > 6 ? columns[6] : "false", out bool isRecurring);
                
                // Convert int to CalendarEntryType enum
                var entryType = (CalendarEntryType)entryTypeInt;
                
                if (id > 0)
                {
                    if (!await ctx.WorkCalendars.AnyAsync(wc => wc.Id == id))
                    {
                        ctx.WorkCalendars.Add(new WorkCalendar
                        {
                            Id = id,
                            Date = calendarDate,
                            Description = description,
                            Name = name,
                            EntryType = entryType,
                            IsRecurringAnnually = isRecurring
                        });
                    }
                }
            }
            
            await ctx.SaveChangesAsync();
        }
        
        private string[] ParseCsvRow(string row)
        {
            // Simple CSV parser - doesn't handle quoted values with commas inside
            return row.Split(',').Select(s => s.Trim()).ToArray();
        }
    }
} 