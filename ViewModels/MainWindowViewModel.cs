using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Services;
using System.Windows.Input;
using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AttandenceDesktop.Models;

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
    public MainWindowViewModel()
    {
        // Default empty constructor for design-time
        // All properties will be initialized with default values
        Dashboard = new DashboardViewModel();
        Departments = new DepartmentViewModel();
        Attendance = new AttendanceViewModel();
        Employees = new EmployeeViewModel();
        WorkSchedules = new WorkScheduleViewModel();
        WorkCalendars = new WorkCalendarViewModel();
        Reports = new ReportViewModel();
        
        // Create mock services for design-time
        var mockReportService = new ReportService(
            () => new Data.ApplicationDbContext(new Microsoft.EntityFrameworkCore.DbContextOptionsBuilder<Data.ApplicationDbContext>().UseInMemoryDatabase("DesignTimeDB").Options),
            null!,
            null!,
            null!);
        var mockEmployeeService = new Services.EmployeeService(() => null!, null!, null!);
        var mockDepartmentService = new Services.DepartmentService(() => null!);
        var mockDataRefreshService = new Services.DataRefreshService();
        
        OverviewReports = new OverviewReportViewModel(
            mockReportService, 
            mockEmployeeService, 
            mockDepartmentService, 
            mockDataRefreshService);
        
        // Set current view
        CurrentView = Dashboard;
        
        ShowDashboardCommand = new RelayCommand(() => CurrentView = Dashboard);
        ShowDepartmentsCommand = new RelayCommand(() => CurrentView = Departments);
        ShowEmployeesCommand = new RelayCommand(() => CurrentView = Employees);
        ShowAttendanceCommand = new RelayCommand(() => CurrentView = Attendance);
        ShowWorkSchedulesCommand = new RelayCommand(() => CurrentView = WorkSchedules);
        ShowWorkCalendarsCommand = new RelayCommand(() => CurrentView = WorkCalendars);
        ShowReportsCommand = new RelayCommand(() => CurrentView = Reports);
        ShowOverviewReportsCommand = new RelayCommand(() => CurrentView = OverviewReports);
    }

    // Dependency Injection constructor for production use
    public MainWindowViewModel(
        DashboardViewModel dashboardViewModel,
        DepartmentViewModel departmentViewModel,
        AttendanceViewModel attendanceViewModel,
        EmployeeViewModel employeeViewModel,
        WorkScheduleViewModel workScheduleViewModel,
        WorkCalendarViewModel workCalendarViewModel,
        ReportViewModel reportViewModel,
        OverviewReportViewModel overviewReportViewModel)
    {
        Dashboard = dashboardViewModel;
        Departments = departmentViewModel;
        Attendance = attendanceViewModel;
        Employees = employeeViewModel;
        WorkSchedules = workScheduleViewModel;
        WorkCalendars = workCalendarViewModel;
        Reports = reportViewModel;
        OverviewReports = overviewReportViewModel;
        
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
        ShowOverviewReportsCommand = new RelayCommand(() => CurrentView = OverviewReports);
    }

    public DashboardViewModel Dashboard { get; }
    public DepartmentViewModel Departments { get; }
    public AttendanceViewModel Attendance { get; }
    public EmployeeViewModel Employees { get; }
    public WorkScheduleViewModel WorkSchedules { get; }
    public WorkCalendarViewModel WorkCalendars { get; }
    public ReportViewModel Reports { get; }
    public OverviewReportViewModel OverviewReports { get; }

    public ICommand ShowDashboardCommand { get; }
    public ICommand ShowDepartmentsCommand { get; }
    public ICommand ShowEmployeesCommand { get; }
    public ICommand ShowAttendanceCommand { get; }
    public ICommand ShowWorkSchedulesCommand { get; }
    public ICommand ShowWorkCalendarsCommand { get; }
    public ICommand ShowReportsCommand { get; }
    public ICommand ShowOverviewReportsCommand { get; }
}
