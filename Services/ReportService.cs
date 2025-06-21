using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Models;

namespace AttandenceDesktop.Services
{
    public class ReportService
    {
        private readonly ApplicationDbContext _context;
        private readonly AttendanceService _attendanceService;
        private readonly WorkCalendarService _workCalendarService;
        private readonly WorkScheduleService _workScheduleService;
        
        public ReportService(
            ApplicationDbContext context,
            AttendanceService attendanceService,
            WorkCalendarService workCalendarService,
            WorkScheduleService workScheduleService)
        {
            _context = context;
            _attendanceService = attendanceService;
            _workCalendarService = workCalendarService;
            _workScheduleService = workScheduleService;
        }
        
        public async Task<List<AttendanceReportItem>> GenerateEmployeeAttendanceReportAsync(
            int employeeId, DateTime startDate, DateTime endDate)
        {
            var attendances = await _context.Attendances
                .Include(a => a.Employee)
                .Where(a => a.EmployeeId == employeeId && a.Date >= startDate && a.Date <= endDate)
                .OrderBy(a => a.Date)
                .ToListAsync();
                
            var report = new List<AttendanceReportItem>();
            var employee = await _context.Employees.FindAsync(employeeId);
            
            if (employee == null)
            {
                return report;
            }
            
            // Get employee's hire date
            DateTime hireDate = employee.HireDate;
            
            foreach (var attendance in attendances)
            {
                var isLate = false;
                var isEarlyDeparture = false;
                var isHoliday = false;
                var isNonWorkingDay = false;
                var isOvertime = false;
                
                if (attendance.CheckInTime.HasValue)
                {
                    isLate = await _attendanceService.IsLateCheckInAsync(employeeId, attendance.CheckInTime.Value);
                }
                
                if (attendance.CheckOutTime.HasValue)
                {
                    isEarlyDeparture = await _attendanceService.IsEarlyCheckOutAsync(employeeId, attendance.CheckOutTime.Value);
                }
                
                // Check if the day is a holiday or non-working day
                isHoliday = !await _workCalendarService.IsWorkingDateAsync(attendance.Date);
                
                // Check if the day is a working day for this employee's schedule
                var isWorkingDay = await _workScheduleService.IsWorkingDayForEmployeeAsync(employeeId, attendance.Date);
                if (!isWorkingDay)
                {
                    isNonWorkingDay = true;
                }
                
                // Calculate overtime (if worked more than expected hours on a working day)
                if (attendance.CheckInTime.HasValue && attendance.CheckOutTime.HasValue && isWorkingDay)
                {
                    var expectedHours = await _workScheduleService.GetExpectedWorkHoursAsync(employeeId, attendance.Date);
                    var workedHours = attendance.WorkDuration?.TotalHours ?? 0;
                    isOvertime = workedHours > expectedHours;
                }
                
                report.Add(new AttendanceReportItem
                {
                    Date = attendance.Date,
                    EmployeeId = employeeId,
                    EmployeeName = attendance.Employee.FullName,
                    DepartmentName = attendance.Employee.Department?.Name,
                    CheckInTime = attendance.CheckInTime,
                    CheckOutTime = attendance.CheckOutTime,
                    WorkDuration = attendance.WorkDuration,
                    Status = attendance.IsComplete 
                        ? (isEarlyDeparture ? "Early Departure" : (isLate ? "Late Arrival" : "Present"))
                        : "Incomplete",
                    IsLate = isLate,
                    IsEarlyDeparture = isEarlyDeparture,
                    IsHoliday = isHoliday,
                    IsNonWorkingDay = isNonWorkingDay,
                    IsOvertime = isOvertime,
                    Notes = attendance.Notes
                });
            }
            
            // Fill in missing dates in the range
            var currentDate = startDate;
            var today = DateTime.Today;
            while (currentDate <= endDate)
            {
                if (!report.Any(r => r.Date.Date == currentDate.Date))
                {
                    // Check if the date is before employee's hire date
                    if (currentDate.Date < hireDate.Date)
                    {
                        // Skip dates before hire date - don't add them to the report
                        currentDate = currentDate.AddDays(1);
                        continue;
                    }
                    
                    // Check if the day is a holiday or non-working day
                    var isHoliday = !await _workCalendarService.IsWorkingDateAsync(currentDate);
                    
                    // Check if the day is a working day for this employee's schedule
                    var isWorkingDay = await _workScheduleService.IsWorkingDayForEmployeeAsync(employeeId, currentDate);
                    var isNonWorkingDay = !isWorkingDay;
                    
                    var status = "Absent";
                    if (isHoliday)
                    {
                        status = "Holiday";
                    }
                    else if (isNonWorkingDay)
                    {
                        status = "Non-Working Day";
                    }
                    else if (currentDate.Date > today)
                    {
                        // Don't mark future dates as "Absent"
                        status = "Scheduled";
                    }
                    
                    report.Add(new AttendanceReportItem
                    {
                        Date = currentDate,
                        EmployeeId = employeeId,
                        EmployeeName = employee?.FullName,
                        DepartmentName = employee?.Department?.Name,
                        CheckInTime = null,
                        CheckOutTime = null,
                        WorkDuration = null,
                        Status = status,
                        IsHoliday = isHoliday,
                        IsNonWorkingDay = isNonWorkingDay
                    });
                }
                currentDate = currentDate.AddDays(1);
            }
            
            return report.OrderBy(r => r.Date).ToList();
        }
        
        public async Task<List<AttendanceReportItem>> GenerateDepartmentAttendanceReportAsync(
            int departmentId, DateTime startDate, DateTime endDate)
        {
            var employees = await _context.Employees
                .Where(e => e.DepartmentId == departmentId)
                .ToListAsync();
                
            var report = new List<AttendanceReportItem>();
            
            foreach (var employee in employees)
            {
                var employeeReport = await GenerateEmployeeAttendanceReportAsync(
                    employee.Id, startDate, endDate);
                report.AddRange(employeeReport);
            }
            
            return report.OrderBy(r => r.Date).ThenBy(r => r.EmployeeName).ToList();
        }
        
        public async Task<List<AttendanceReportItem>> GenerateCompanyAttendanceReportAsync(
            DateTime startDate, DateTime endDate)
        {
            var employees = await _context.Employees.ToListAsync();
            var report = new List<AttendanceReportItem>();
            
            foreach (var employee in employees)
            {
                var employeeReport = await GenerateEmployeeAttendanceReportAsync(
                    employee.Id, startDate, endDate);
                report.AddRange(employeeReport);
            }
            
            return report.OrderBy(r => r.Date).ThenBy(r => r.EmployeeName).ToList();
        }
        
        // Get statistics for reports
        public AttendanceReportStatistics GetReportStatistics(List<AttendanceReportItem> report)
        {
            var stats = new AttendanceReportStatistics
            {
                TotalDays = report.Select(r => r.Date.Date).Distinct().Count(),
                PresentDays = report.Count(r => r.Status == "Present"),
                EarlyDepartureDays = report.Count(r => r.Status == "Early Departure"),
                LateArrivalDays = report.Count(r => r.Status == "Late Arrival"),
                AbsentDays = report.Count(r => r.Status == "Absent"),
                LateArrivals = report.Count(r => r.IsLate),
                EarlyDepartures = report.Count(r => r.IsEarlyDeparture),
                Holidays = report.Count(r => r.IsHoliday),
                NonWorkingDays = report.Count(r => r.IsNonWorkingDay),
                OvertimeDays = report.Count(r => r.IsOvertime)
            };
            
            return stats;
        }
    }
    
    public class AttendanceReportItem
    {
        public DateTime Date { get; set; }
        public int EmployeeId { get; set; }
        public string EmployeeName { get; set; }
        public string DepartmentName { get; set; }
        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }
        public TimeSpan? WorkDuration { get; set; }
        public string Status { get; set; } = "Present";
        public bool IsLate { get; set; }
        public bool IsEarlyDeparture { get; set; }
        public bool IsHoliday { get; set; }
        public bool IsNonWorkingDay { get; set; }
        public bool IsOvertime { get; set; }
        public string Notes { get; set; } = "";
    }
    
    public class AttendanceReportStatistics
    {
        public AttendanceReportStatistics()
        {
            TotalDays = 0;
            PresentDays = 0;
            EarlyDepartureDays = 0;
            LateArrivalDays = 0;
            AbsentDays = 0;
            LateArrivals = 0;
            EarlyDepartures = 0;
            Holidays = 0;
            NonWorkingDays = 0;
            OvertimeDays = 0;
        }
        
        public int TotalDays { get; set; }
        public int PresentDays { get; set; }
        public int EarlyDepartureDays { get; set; }
        public int LateArrivalDays { get; set; }
        public int AbsentDays { get; set; }
        public int LateArrivals { get; set; }
        public int EarlyDepartures { get; set; }
        public int Holidays { get; set; }
        public int NonWorkingDays { get; set; }
        public int OvertimeDays { get; set; }
        
        public double AttendanceRate => TotalDays > 0 ? (double)PresentDays / TotalDays * 100 : 0;
        public double PunctualityRate => PresentDays > 0 ? (double)(PresentDays - LateArrivals) / PresentDays * 100 : 0;
        public double OvertimeRate => PresentDays > 0 ? (double)OvertimeDays / PresentDays * 100 : 0;
    }
} 