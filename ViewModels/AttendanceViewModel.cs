using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;

namespace AttandenceDesktop.ViewModels;

public partial class AttendanceViewModel : ViewModelBase, IDisposable
{
    private readonly EmployeeService _employeeService;
    private readonly AttendanceService _attendanceService;
    private readonly DataRefreshService _dataRefreshService;

    public AttendanceViewModel(EmployeeService employeeService,
                               AttendanceService attendanceService,
                               DataRefreshService dataRefreshService)
    {
        _employeeService = employeeService;
        _attendanceService = attendanceService;
        _dataRefreshService = dataRefreshService;

        Employees = new ObservableCollection<Employee>();
        TodayAttendance = new ObservableCollection<Attendance>();
        FilteredAttendance = new ObservableCollection<Attendance>();

        CheckInCommand = new AsyncRelayCommand(CheckInAsync, CanCheckIn);
        CheckOutCommand = new AsyncRelayCommand(CheckOutAsync, CanCheckOut);
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        SetFilterCommand = new RelayCommand<string>(s => ApplyFilter(Enum.Parse<AttendanceStatus>(s)));
        RecalculateMetricsCommand = new AsyncRelayCommand(RecalculateMetricsAsync);

        var timer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Normal,
            (_, _) => CurrentTime = DateTime.Now.ToString("HH:mm:ss"));
        timer.Start();

        // Subscribe to data change events
        _dataRefreshService.AttendanceChanged += OnAttendanceChanged;
        _dataRefreshService.EmployeesChanged += OnEmployeesChanged;

        _ = InitializeAsync();
    }

    // Parameterless constructor for design-time support
    public AttendanceViewModel()
    {
        _employeeService = null!;
        _attendanceService = null!;
        _dataRefreshService = null!;
        
        Employees = new ObservableCollection<Employee>();
        TodayAttendance = new ObservableCollection<Attendance>();
        FilteredAttendance = new ObservableCollection<Attendance>();
        
        CheckInCommand = new AsyncRelayCommand(async () => { }, () => false);
        CheckOutCommand = new AsyncRelayCommand(async () => { }, () => false);
        RefreshCommand = new AsyncRelayCommand(async () => { });
        SetFilterCommand = new RelayCommand<string>(_ => { });
        RecalculateMetricsCommand = new AsyncRelayCommand(async () => { });
        
        // Set current time for design-time
        CurrentTime = "12:00:00";
    }

    public void Dispose()
    {
        // Unsubscribe from events
        _dataRefreshService.AttendanceChanged -= OnAttendanceChanged;
        _dataRefreshService.EmployeesChanged -= OnEmployeesChanged;
    }

    private void OnAttendanceChanged(object? sender, EventArgs e)
    {
        _ = RefreshAsync();
    }

    private void OnEmployeesChanged(object? sender, EventArgs e)
    {
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var emps = await _employeeService.GetAllAsync();
        Employees.Clear();
        foreach (var e in emps) Employees.Add(e);

        await RefreshAsync();
    }

    #region Bindable Properties

    [ObservableProperty] private int _selectedEmployeeId;
    [ObservableProperty] private Employee? _selectedEmployee;

    [ObservableProperty] private AttendanceStatus _selectedEmployeeStatus = AttendanceStatus.NotCheckedIn;

    [ObservableProperty] private Attendance? _selectedEmployeeAttendance;

    [ObservableProperty] private string _currentTime = DateTime.Now.ToString("HH:mm:ss");

    [ObservableProperty] private string _message = string.Empty;

    [ObservableProperty] private string _alertKind = "Info"; // Info, Success, Warning, Error

    public ObservableCollection<Employee> Employees { get; }
    public ObservableCollection<Attendance> TodayAttendance { get; }
    public ObservableCollection<Attendance> FilteredAttendance { get; }

    #endregion

    #region Commands

    public IAsyncRelayCommand CheckInCommand { get; }
    public IAsyncRelayCommand CheckOutCommand { get; }
    public IAsyncRelayCommand RefreshCommand { get; }
    public IRelayCommand<string> SetFilterCommand { get; }
    public IAsyncRelayCommand RecalculateMetricsCommand { get; }

    #endregion

    private async Task RefreshAsync()
    {
        TodayAttendance.Clear();
        var todays = await _attendanceService.GetByDateRangeAsync(DateTime.Today, DateTime.Today);
        foreach (var att in todays)
            TodayAttendance.Add(att);

        ApplyFilter(_currentFilter);
        await UpdateSelectedEmployeeStatusAsync();
    }

    private AttendanceStatus _currentFilter = AttendanceStatus.All;

    private void ApplyFilter(AttendanceStatus status)
    {
        _currentFilter = status;
        FilteredAttendance.Clear();
        var list = status switch
        {
            AttendanceStatus.CheckedIn => TodayAttendance.Where(a => a.CheckInTime.HasValue && !a.CheckOutTime.HasValue),
            AttendanceStatus.CheckedOut => TodayAttendance.Where(a => a.CheckInTime.HasValue && a.CheckOutTime.HasValue),
            AttendanceStatus.Late => TodayAttendance.Where(a => a.IsLateArrival),
            AttendanceStatus.EarlyDeparture => TodayAttendance.Where(a => a.IsEarlyDeparture),
            AttendanceStatus.Overtime => TodayAttendance.Where(a => a.IsOvertime),
            _ => TodayAttendance
        };
        foreach (var att in list) FilteredAttendance.Add(att);
    }

    partial void OnSelectedEmployeeIdChanged(int value)
    {
        _ = UpdateSelectedEmployeeStatusAsync();
        CheckInCommand.NotifyCanExecuteChanged();
        CheckOutCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedEmployeeChanged(Employee? value)
    {
        if (value != null)
            SelectedEmployeeId = value.Id;
    }

    private async Task UpdateSelectedEmployeeStatusAsync()
    {
        if (SelectedEmployeeId == 0)
        {
            SelectedEmployeeStatus = AttendanceStatus.NotCheckedIn;
            SelectedEmployeeAttendance = null;
            return;
        }

        var att = await _attendanceService.GetByEmployeeAndDateAsync(SelectedEmployeeId, DateTime.Today);
        SelectedEmployeeAttendance = att;
        if (att == null)
            SelectedEmployeeStatus = AttendanceStatus.NotCheckedIn;
        else if (att.CheckInTime.HasValue && !att.CheckOutTime.HasValue)
            SelectedEmployeeStatus = AttendanceStatus.CheckedIn;
        else if (att.CheckInTime.HasValue && att.CheckOutTime.HasValue)
            SelectedEmployeeStatus = AttendanceStatus.CheckedOut;
    }

    private bool CanCheckIn() => SelectedEmployeeId > 0 && (SelectedEmployeeStatus == AttendanceStatus.NotCheckedIn || SelectedEmployeeStatus == AttendanceStatus.CheckedOut);

    private bool CanCheckOut() => SelectedEmployeeId > 0 && SelectedEmployeeStatus == AttendanceStatus.CheckedIn;

    private async Task CheckInAsync()
    {
        if (SelectedEmployeeId == 0) return;
        try
        {
            await _attendanceService.CheckInAsync(SelectedEmployeeId);
            AlertKind = "Success";
            Message = "Check-in recorded successfully";
            await RefreshAsync();
            
            // Immediately update UI to allow check out
            SelectedEmployeeStatus = AttendanceStatus.CheckedIn;
            CheckOutCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            AlertKind = "Error";
            Message = ex.Message;
        }
    }

    private async Task CheckOutAsync()
    {
        if (SelectedEmployeeId == 0) return;
        try
        {
            await _attendanceService.CheckOutAsync(SelectedEmployeeId);
            AlertKind = "Success";
            Message = "Check-out recorded successfully";
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            AlertKind = "Error";
            Message = $"Error during check-out: {ex.Message}";
        }
    }

    private async Task RecalculateMetricsAsync()
    {
        try
        {
            Message = "Recalculating attendance metrics...";
            AlertKind = "Warning";
            
            await _attendanceService.RecalculateAllAttendanceMetricsAsync();
            
            Message = "Metrics recalculated successfully!";
            AlertKind = "Success";
            
            // Reload attendance data
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            Message = $"Error recalculating metrics: {ex.Message}";
            AlertKind = "Error";
        }
    }

    public enum AttendanceStatus { All, NotCheckedIn, CheckedIn, CheckedOut, Late, EarlyDeparture, Overtime }
} 