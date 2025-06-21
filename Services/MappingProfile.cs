using AutoMapper;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;

namespace AttandenceDesktop.Services
{
    public class MappingProfile : Profile
    {
        public MappingProfile()
        {
            // Create basic mapping configurations for the models
            CreateMap<Department, Department>();
            CreateMap<Employee, Employee>();
            CreateMap<Attendance, Attendance>();
            CreateMap<WorkSchedule, WorkSchedule>();
            CreateMap<WorkCalendar, WorkCalendar>();
            
            // Map Attendance to AttendanceReportItem
            CreateMap<Attendance, AttendanceReportItem>()
                .ForMember(dest => dest.EmployeeId, opt => opt.MapFrom(src => src.EmployeeId))
                .ForMember(dest => dest.EmployeeName, opt => opt.MapFrom(src => src.Employee.FullName))
                .ForMember(dest => dest.DepartmentName, opt => opt.MapFrom(src => src.Employee.Department.Name))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.IsComplete ? "Present" : "Incomplete"));
            
            // Map WorkSchedule to a display model if needed
            CreateMap<WorkSchedule, WorkScheduleDto>()
                .ForMember(dest => dest.DepartmentName, 
                    opt => opt.MapFrom(src => src.Department != null ? src.Department.Name : "All Departments"))
                .ForMember(dest => dest.WorkingDays, opt => opt.MapFrom(src => GetWorkingDaysString(src)));
            
            // Map AttendanceStatistics to a DTO
            CreateMap<AttendanceStatistics, AttendanceStatisticsDto>();
        }
        
        // Helper method to get working days as a readable string
        private string GetWorkingDaysString(WorkSchedule schedule)
        {
            var days = new List<string>();
            
            if (schedule.IsWorkingDaySunday) days.Add("Sun");
            if (schedule.IsWorkingDayMonday) days.Add("Mon");
            if (schedule.IsWorkingDayTuesday) days.Add("Tue");
            if (schedule.IsWorkingDayWednesday) days.Add("Wed");
            if (schedule.IsWorkingDayThursday) days.Add("Thu");
            if (schedule.IsWorkingDayFriday) days.Add("Fri");
            if (schedule.IsWorkingDaySaturday) days.Add("Sat");
            
            return string.Join(", ", days);
        }
    }
    
    // DTO classes for view models
    public class WorkScheduleDto
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public string WorkingDays { get; set; }
        public string DepartmentName { get; set; }
        public string Description { get; set; }
        public int FlexTimeAllowanceMinutes { get; set; }
    }
    
    public class AttendanceStatisticsDto
    {
        public int TotalWorkingDays { get; set; }
        public int DaysPresent { get; set; }
        public int DaysAbsent { get; set; }
        public int LateArrivals { get; set; }
        public int EarlyDepartures { get; set; }
        public double AttendancePercentage { get; set; }
        public double AbsencePercentage { get; set; }
        public double PunctualityPercentage { get; set; }
    }
} 