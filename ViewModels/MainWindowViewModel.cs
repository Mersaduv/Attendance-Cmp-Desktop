using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Services;
using System.Windows.Input;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AttandenceDesktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    private ViewModelBase _currentView;
    
    public ViewModelBase CurrentView
    {
        get => _currentView;
        set => SetProperty(ref _currentView, value);
    }

    // Design-time constructor
    public MainWindowViewModel() : this(CreateDesignDepartmentService())
    {
    }

    public MainWindowViewModel(DepartmentService departmentService)
    {
        // Use same in-memory DbContext factory as departmentService
        var factoryField = departmentService.GetType().GetField("_contextFactory", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var contextFactory = factoryField?.GetValue(departmentService) as Func<ApplicationDbContext>;

        if (contextFactory == null)
        {
            // Fallback: create new factory for design time
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("DesignTimeDB_Fallback").Options;
            contextFactory = () => new ApplicationDbContext(options);
        }

        var workScheduleService = new WorkScheduleService(contextFactory);
        var employeeService = new EmployeeService(contextFactory, workScheduleService);
        var workCalendarService = new WorkCalendarService(contextFactory);
        var attendanceService = new AttendanceService(contextFactory, workScheduleService, workCalendarService);
        var reportService = new ReportService(contextFactory(), attendanceService, workCalendarService, workScheduleService);

        Dashboard = new DashboardViewModel(employeeService, departmentService, attendanceService);
        Departments = new DepartmentViewModel(departmentService);
        Attendance = new AttendanceViewModel(employeeService, attendanceService);
        Employees = new EmployeeViewModel(employeeService, departmentService);
        WorkSchedules = new WorkScheduleViewModel(workScheduleService, departmentService);
        WorkCalendars = new WorkCalendarViewModel(workCalendarService);
        Reports = new ReportViewModel(reportService, employeeService, departmentService);

        CurrentView = Dashboard;

        ShowDashboardCommand = new RelayCommand(() => CurrentView = Dashboard);
        ShowDepartmentsCommand = new RelayCommand(() => CurrentView = Departments);
        ShowEmployeesCommand = new RelayCommand(() => CurrentView = Employees);
        ShowAttendanceCommand = new RelayCommand(() => CurrentView = Attendance);
        ShowWorkSchedulesCommand = new RelayCommand(() => CurrentView = WorkSchedules);
        ShowWorkCalendarsCommand = new RelayCommand(() => CurrentView = WorkCalendars);
        ShowReportsCommand = new RelayCommand(() => CurrentView = Reports);
    }

    // Dependency Injection constructor for production use
    public MainWindowViewModel(
        DashboardViewModel dashboardViewModel,
        DepartmentViewModel departmentViewModel,
        AttendanceViewModel attendanceViewModel,
        EmployeeViewModel employeeViewModel,
        WorkScheduleViewModel workScheduleViewModel,
        WorkCalendarViewModel workCalendarViewModel,
        ReportViewModel reportViewModel)
    {
        Dashboard = dashboardViewModel;
        Departments = departmentViewModel;
        Attendance = attendanceViewModel;
        Employees = employeeViewModel;
        WorkSchedules = workScheduleViewModel;
        WorkCalendars = workCalendarViewModel;
        Reports = reportViewModel;
        
        // Default to Dashboard
        CurrentView = Dashboard;
        
        // Set up navigation commands
        ShowDashboardCommand = new RelayCommand(() => CurrentView = Dashboard);
        ShowDepartmentsCommand = new RelayCommand(() => CurrentView = Departments);
        ShowEmployeesCommand = new RelayCommand(() => CurrentView = Employees);
        ShowAttendanceCommand = new RelayCommand(() => CurrentView = Attendance);
        ShowWorkSchedulesCommand = new RelayCommand(() => CurrentView = WorkSchedules);
        ShowWorkCalendarsCommand = new RelayCommand(() => CurrentView = WorkCalendars);
        ShowReportsCommand = new RelayCommand(() => CurrentView = Reports);
    }

    public DashboardViewModel Dashboard { get; }
    public DepartmentViewModel Departments { get; }
    public AttendanceViewModel Attendance { get; }
    public EmployeeViewModel Employees { get; }
    public WorkScheduleViewModel WorkSchedules { get; }
    public WorkCalendarViewModel WorkCalendars { get; }
    public ReportViewModel Reports { get; }

    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowDepartmentsCommand { get; }
    public ICommand ShowEmployeesCommand { get; }
    public ICommand ShowAttendanceCommand { get; }
    public ICommand ShowWorkSchedulesCommand { get; }
    public ICommand ShowWorkCalendarsCommand { get; }
    public ICommand ShowReportsCommand { get; }

    private static DepartmentService CreateDesignDepartmentService()
    {
        var options = new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Data.ApplicationDbContext>()
            .UseInMemoryDatabase("DesignTimeDB").Options;
        return new Services.DepartmentService(() => new Data.ApplicationDbContext(options));
    }
}
