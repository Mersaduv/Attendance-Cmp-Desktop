using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AttandenceDesktop.Services;
using System.Collections.ObjectModel;
using System;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;

namespace AttandenceDesktop.ViewModels;

public partial class DashboardViewModel : ViewModelBase, IDisposable
{
    private readonly EmployeeService _employeeService;
    private readonly DepartmentService _departmentService;
    private readonly AttendanceService _attendanceService;
    private readonly DataRefreshService _dataRefreshService;

    public DashboardViewModel(EmployeeService employeeService,
                               DepartmentService departmentService,
                               AttendanceService attendanceService,
                               DataRefreshService dataRefreshService)
    {
        _employeeService = employeeService;
        _departmentService = departmentService;
        _attendanceService = attendanceService;
        _dataRefreshService = dataRefreshService;

        RecentAttendance = new ObservableCollection<RecentAttendanceItem>();
        RefreshCommand = new AsyncRelayCommand(LoadDashboardDataAsync);

        // Subscribe to data change events
        _dataRefreshService.AttendanceChanged += OnAttendanceChanged;
        _dataRefreshService.EmployeesChanged += OnEmployeesChanged;
        _dataRefreshService.DepartmentsChanged += OnDepartmentsChanged;

        // Live clock
        var timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal,
            (_, _) => CurrentTime = DateTime.Now.ToString("HH:mm:ss"));
        timer.Start();

        _ = LoadDashboardDataAsync();
    }

    // Parameterless constructor for design-time support
    public DashboardViewModel() 
    {
        _employeeService = null!;
        _departmentService = null!;
        _attendanceService = null!;
        _dataRefreshService = null!;
        
        RecentAttendance = new ObservableCollection<RecentAttendanceItem>();
        RefreshCommand = new AsyncRelayCommand(async () => { });
        
        // Add some design-time data
        RecentAttendance.Add(new RecentAttendanceItem("John Doe", "IT", "2023-10-01", "09:00", "17:00", "Complete"));
        RecentAttendance.Add(new RecentAttendanceItem("Jane Smith", "HR", "2023-10-01", "08:30", "16:30", "Complete"));
    }

    public void Dispose()
    {
        // Unsubscribe from events
        _dataRefreshService.AttendanceChanged -= OnAttendanceChanged;
        _dataRefreshService.EmployeesChanged -= OnEmployeesChanged;
        _dataRefreshService.DepartmentsChanged -= OnDepartmentsChanged;
    }

    private void OnAttendanceChanged(object? sender, EventArgs e)
    {
        _ = LoadDashboardDataAsync();
    }

    private void OnEmployeesChanged(object? sender, EventArgs e)
    {
        _ = LoadDashboardDataAsync();
    }

    private void OnDepartmentsChanged(object? sender, EventArgs e)
    {
        _ = LoadDashboardDataAsync();
    }

    [ObservableProperty] private string _currentDate = DateTime.Now.ToString("dddd, MMMM d, yyyy");
    [ObservableProperty] private string _currentTime = DateTime.Now.ToString("HH:mm:ss");

    [ObservableProperty] private int _employeeCount;
    [ObservableProperty] private int _departmentCount;
    [ObservableProperty] private int _todayAttendanceCount;

    public ObservableCollection<RecentAttendanceItem> RecentAttendance { get; }

    public IAsyncRelayCommand RefreshCommand { get; }

    private async Task LoadDashboardDataAsync()
    {
        var employees = await _employeeService.GetAllAsync();
        EmployeeCount = employees.Count;

        var departments = await _departmentService.GetAllAsync();
        DepartmentCount = departments.Count;

        var endDate = DateTime.Today;
        var startDate = endDate.AddDays(-7);
        var allAttendance = await _attendanceService.GetByDateRangeAsync(startDate, endDate);

        RecentAttendance.Clear();
        foreach (var att in allAttendance.OrderByDescending(a => a.Date).Take(5))
        {
            var status = att.IsComplete ? "Complete" : att.CheckInTime.HasValue ? "Checked In" : "Incomplete";
            RecentAttendance.Add(new RecentAttendanceItem(
                att.Employee?.FirstName + " " + att.Employee?.LastName,
                att.Employee?.Department?.Name ?? string.Empty,
                att.Date.ToString("yyyy-MM-dd"),
                att.CheckInTime?.ToString("HH:mm:ss") ?? "-",
                att.CheckOutTime?.ToString("HH:mm:ss") ?? "-",
                status));
        }

        TodayAttendanceCount = allAttendance.Count(a => a.Date == DateTime.Today);
    }
} 