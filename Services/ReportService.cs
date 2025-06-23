using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Models;

namespace AttandenceDesktop.Services
{
    public class ReportService
    {
        private readonly Func<ApplicationDbContext> _contextFactory;
        private readonly AttendanceService _attendanceService;
        private readonly WorkCalendarService _workCalendarService;
        private readonly WorkScheduleService _workScheduleService;
        
        public ReportService(
            ApplicationDbContext context,
            AttendanceService attendanceService,
            WorkCalendarService workCalendarService,
            WorkScheduleService workScheduleService)
        {
            _contextFactory = () => context;
            _attendanceService = attendanceService;
            _workCalendarService = workCalendarService;
            _workScheduleService = workScheduleService;
        }
        
        public ReportService(
            Func<ApplicationDbContext> contextFactory,
            AttendanceService attendanceService,
            WorkCalendarService workCalendarService,
            WorkScheduleService workScheduleService)
        {
            _contextFactory = contextFactory;
            _attendanceService = attendanceService;
            _workCalendarService = workCalendarService;
            _workScheduleService = workScheduleService;
        }
        
        private ApplicationDbContext NewCtx() => _contextFactory();
        
        public async Task<List<AttendanceReportItem>> GenerateEmployeeAttendanceReportAsync(
            int employeeId, DateTime startDate, DateTime endDate)
        {
            using var ctx = NewCtx();
            var attendances = await ctx.Attendances
                .Include(a => a.Employee)
                .ThenInclude(e => e.Department)
                .Where(a => a.EmployeeId == employeeId && a.Date >= startDate && a.Date <= endDate)
                .OrderBy(a => a.Date)
                .ToListAsync();
                
            var report = new List<AttendanceReportItem>();
            var employee = await ctx.Employees
                .Include(e => e.Department)
                .FirstOrDefaultAsync(e => e.Id == employeeId);
            
            if (employee == null)
            {
                return report;
            }
            
            // Get employee's hire date
            DateTime hireDate = employee.HireDate;
            
            foreach (var attendance in attendances)
            {
                bool isHoliday = false;
                bool isNonWorkingDay = false;
                
                // Use the flags directly from the attendance record
                bool isLate = attendance.IsLateArrival;
                bool isEarlyDeparture = attendance.IsEarlyDeparture;
                bool isOvertime = attendance.OvertimeMinutes.HasValue && attendance.OvertimeMinutes.Value.TotalMinutes > 0;
                
                // Check if the day is a holiday or non-working day
                isHoliday = !await _workCalendarService.IsWorkingDateAsync(attendance.Date);
                
                // Check if the day is a working day for this employee's schedule
                var isWorkingDay = await _workScheduleService.IsWorkingDayForEmployeeAsync(employeeId, attendance.Date);
                if (!isWorkingDay)
                {
                    isNonWorkingDay = true;
                }
                
                // Determine the status based on attendance flags
                string status;
                if (!attendance.IsComplete)
                {
                    status = "Incomplete";
                }
                else if (isLate && isEarlyDeparture)
                {
                    status = "Late & Left Early";
                }
                else if (isLate)
                {
                    // Check if this is a half-day attendance rather than just late arrival
                    var expectedWorkHours = await _workScheduleService.GetExpectedWorkHoursAsync(employeeId, attendance.Date);
                    var halfDayHours = expectedWorkHours / 2.0;
                    
                    if (attendance.WorkDuration.HasValue && attendance.WorkDuration.Value.TotalHours <= halfDayHours)
                    {
                        status = "Half Day";
                    }
                    else
                    {
                        status = "Late Arrival";
                    }
                }
                else if (isEarlyDeparture)
                {
                    // Also check if early departure is actually a half day
                    var expectedWorkHours = await _workScheduleService.GetExpectedWorkHoursAsync(employeeId, attendance.Date);
                    var halfDayHours = expectedWorkHours / 2.0;
                    
                    if (attendance.WorkDuration.HasValue && attendance.WorkDuration.Value.TotalHours <= halfDayHours)
                    {
                        status = "Half Day";
                    }
                    else
                    {
                        status = "Early Departure";
                    }
                }
                else if (attendance.IsEarlyArrival)
                {
                    status = "Early Arrival";
                }
                else if (isOvertime)
                {
                    status = "Overtime";
                }
                else
                {
                    status = "Present";
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
                    Status = status,
                    IsLate = isLate,
                    IsEarlyDeparture = isEarlyDeparture,
                    IsHoliday = isHoliday,
                    IsNonWorkingDay = isNonWorkingDay,
                    IsOvertime = isOvertime,
                    IsEarlyArrival = attendance.IsEarlyArrival,
                    Notes = attendance.Notes,
                    LateMinutes = attendance.LateMinutes,
                    EarlyDepartureMinutes = attendance.EarlyDepartureMinutes,
                    OvertimeMinutes = attendance.OvertimeMinutes,
                    EarlyArrivalMinutes = attendance.EarlyArrivalMinutes
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
            var employees = await _contextFactory().Employees
                .Include(e => e.Department)
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
            var employees = await _contextFactory().Employees
                .Include(e => e.Department)
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
        
        // Get statistics for reports
        public AttendanceReportStatistics GetReportStatistics(List<AttendanceReportItem> report)
        {
            var stats = new AttendanceReportStatistics();
            
            // Count working days (excluding holidays and non-working days)
            stats.TotalDays = report.Count(r => !r.IsHoliday && !r.IsNonWorkingDay);
            
            // Count present days (with check-in and check-out)
            stats.PresentDays = report.Count(r => r.CheckInTime.HasValue && r.CheckOutTime.HasValue);
            
            // Count absent days
            stats.AbsentDays = report.Count(r => !r.IsHoliday && !r.IsNonWorkingDay && !r.CheckInTime.HasValue);
            
            // Count late arrivals and early departures
            stats.LateArrivals = report.Count(r => r.IsLate);
            stats.EarlyDepartures = report.Count(r => r.IsEarlyDeparture);
            stats.EarlyArrivals = report.Count(r => r.IsEarlyArrival);
            
            // Count days with late arrivals and early departures
            stats.LateArrivalDays = report.Count(r => r.IsLate);
            stats.EarlyDepartureDays = report.Count(r => r.IsEarlyDeparture);
            stats.EarlyArrivalDays = report.Count(r => r.IsEarlyArrival);
            
            // Count holidays and non-working days
            stats.Holidays = report.Count(r => r.IsHoliday);
            stats.NonWorkingDays = report.Count(r => r.IsNonWorkingDay);
            
            // Count overtime days
            stats.OvertimeDays = report.Count(r => r.IsOvertime);
            
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
        public bool IsEarlyArrival { get; set; }
        public string Notes { get; set; } = "";
        public TimeSpan? LateMinutes { get; set; }
        public TimeSpan? EarlyDepartureMinutes { get; set; }
        public TimeSpan? OvertimeMinutes { get; set; }
        public TimeSpan? EarlyArrivalMinutes { get; set; }
    }
    
    public class AttendanceReportStatistics
    {
        public AttendanceReportStatistics()
        {
            TotalDays = 0;
            PresentDays = 0;
            EarlyDepartureDays = 0;
            LateArrivalDays = 0;
            EarlyArrivalDays = 0;
            AbsentDays = 0;
            LateArrivals = 0;
            EarlyDepartures = 0;
            EarlyArrivals = 0;
            Holidays = 0;
            NonWorkingDays = 0;
            OvertimeDays = 0;
        }
        
        public int TotalDays { get; set; }
        public int PresentDays { get; set; }
        public int EarlyDepartureDays { get; set; }
        public int LateArrivalDays { get; set; }
        public int EarlyArrivalDays { get; set; }
        public int AbsentDays { get; set; }
        public int LateArrivals { get; set; }
        public int EarlyDepartures { get; set; }
        public int EarlyArrivals { get; set; }
        public int Holidays { get; set; }
        public int NonWorkingDays { get; set; }
        public int OvertimeDays { get; set; }
        
        public double AttendanceRate => TotalDays > 0 ? (double)PresentDays / TotalDays * 100 : 0;
        public double PunctualityRate => PresentDays > 0 ? (double)(PresentDays - LateArrivals) / PresentDays * 100 : 0;
        public double OvertimeRate => PresentDays > 0 ? (double)OvertimeDays / PresentDays * 100 : 0;
        public double EarlyArrivalRate => PresentDays > 0 ? (double)EarlyArrivals / PresentDays * 100 : 0;
    }
} 