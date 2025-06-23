using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AttandenceDesktop.ViewModels
{
    public class OverviewReportViewModel : ViewModelBase, IDisposable
    {
        private readonly ReportService _reportService;
        private readonly EmployeeService _employeeService;
        private readonly DepartmentService _departmentService;
        private readonly DataRefreshService _dataRefreshService;
        
        private DateTime _startDate = DateTime.Today.AddDays(-30);
        private DateTime _endDate = DateTime.Today;
        private bool _isLoading;
        private Dictionary<int, List<AttendanceReportItem>> _employeeAttendanceData = new Dictionary<int, List<AttendanceReportItem>>();
        
        public OverviewReportViewModel(
            ReportService reportService,
            EmployeeService employeeService,
            DepartmentService departmentService,
            DataRefreshService dataRefreshService)
        {
            _reportService = reportService;
            _employeeService = employeeService;
            _departmentService = departmentService;
            _dataRefreshService = dataRefreshService;
            
            // Set up data refresh handlers
            _dataRefreshService.AttendanceChanged += OnAttendanceChanged;
            _dataRefreshService.EmployeesChanged += OnEmployeesChanged;
            _dataRefreshService.DepartmentsChanged += OnDepartmentsChanged;
            
            // Initialize commands
            GenerateReportCommand = new AsyncRelayCommand(GenerateReportAsync);
            ExportReportCommand = new AsyncRelayCommand(ExportReportAsync);
            
            // Load initial data
            Task.Run(InitializeDataAsync);
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
            // Refresh the report if it's already generated
            if (_employeeAttendanceData.Count > 0)
            {
                Task.Run(GenerateReportAsync);
            }
        }
        
        private void OnEmployeesChanged(object? sender, EventArgs e)
        {
            Task.Run(LoadDepartmentTreeAsync);
        }
        
        private void OnDepartmentsChanged(object? sender, EventArgs e)
        {
            Task.Run(LoadDepartmentTreeAsync);
        }
        
        private async Task InitializeDataAsync()
        {
            await LoadDepartmentTreeAsync();
        }
        
        public DateTime StartDate
        {
            get => _startDate;
            set => SetProperty(ref _startDate, value);
        }
        
        public DateTime EndDate
        {
            get => _endDate;
            set => SetProperty(ref _endDate, value);
        }
        
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        // Department tree nodes for the side panel
        public ObservableCollection<DepartmentTreeNode> DepartmentTree { get; } = new();
        
        // Collection of dates in the selected range (for calendar view columns)
        public ObservableCollection<DateTime> DateRange { get; } = new();
        
        // Selected employees for report generation
        public ObservableCollection<int> SelectedEmployeeIds { get; } = new();
        
        // Commands
        public ICommand GenerateReportCommand { get; }
        public ICommand ExportReportCommand { get; }
        
        // Method to load department and employee hierarchy
        private async Task LoadDepartmentTreeAsync()
        {
            var departments = await _departmentService.GetAllAsync();
            DepartmentTree.Clear();
            
            foreach (var department in departments)
            {
                var employees = await _employeeService.GetByDepartmentAsync(department.Id);
                var employeeNodes = new ObservableCollection<EmployeeTreeNode>();
                
                foreach (var employee in employees)
                {
                    employeeNodes.Add(new EmployeeTreeNode
                    {
                        Id = employee.Id,
                        Name = employee.FullName,
                        IsChecked = false
                    });
                }
                
                DepartmentTree.Add(new DepartmentTreeNode
                {
                    Id = department.Id,
                    Name = department.Name,
                    Employees = employeeNodes,
                    IsExpanded = false,
                    IsChecked = false
                });
            }
        }
        
        // Generate the report based on selected departments/employees
        private async Task GenerateReportAsync()
        {
            Program.LogMessage($"OverviewReport: Starting report generation, Date range: {StartDate:yyyy-MM-dd} to {EndDate:yyyy-MM-dd}");
            
            if (StartDate > EndDate)
            {
                Program.LogMessage("OverviewReport: Error - Start date is after end date");
                // Show error message
                return;
            }
            
            IsLoading = true;
            _employeeAttendanceData.Clear();
            
            try
            {
                // Get selected employees first
                SelectedEmployeeIds.Clear();
                foreach (var deptNode in DepartmentTree)
                {
                    foreach (var empNode in deptNode.Employees)
                    {
                        if (empNode.IsChecked)
                        {
                            SelectedEmployeeIds.Add(empNode.Id);
                        }
                    }
                }
                Program.LogMessage($"OverviewReport: {SelectedEmployeeIds.Count} employees selected");
                
                // Generate attendance data for each selected employee
                foreach (var employeeId in SelectedEmployeeIds)
                {
                    Program.LogMessage($"OverviewReport: Generating report for employee ID: {employeeId}");
                    var employeeReport = await _reportService.GenerateEmployeeAttendanceReportAsync(
                        employeeId, StartDate, EndDate);
                    _employeeAttendanceData[employeeId] = employeeReport;
                    Program.LogMessage($"OverviewReport: Retrieved {employeeReport.Count} attendance records for employee ID: {employeeId}");
                    
                    // Log some sample data for debugging
                    if (employeeReport.Count > 0)
                    {
                        var sample = employeeReport.First();
                        Program.LogMessage($"OverviewReport: Sample data for employee {employeeId}: Date={sample.Date:yyyy-MM-dd}, Status={sample.Status}, IsLate={sample.IsLate}, IsAbsent={sample.CheckInTime == null}, HasCheckIn={sample.CheckInTime != null}");
                    }
                }
                
                Program.LogMessage($"OverviewReport: Generated reports for {_employeeAttendanceData.Count} employees");
                
                // Now build the date range for the calendar view - AFTER data is loaded
                DateRange.Clear();
                for (var date = StartDate; date <= EndDate; date = date.AddDays(1))
                {
                    DateRange.Add(date);
                }
                Program.LogMessage($"OverviewReport: Added {DateRange.Count} dates to date range");
                
                OnPropertyChanged(nameof(GetAttendanceStatus));
            }
            catch (Exception ex)
            {
                Program.LogMessage($"OverviewReport: ERROR - {ex.Message}\n{ex.StackTrace}");
            }
            finally
            {
                IsLoading = false;
                Program.LogMessage("OverviewReport: Report generation completed");
            }
        }
        
        // Get attendance status for an employee on a specific date
        public AttendanceReportItem? GetAttendanceStatus(int employeeId, DateTime date)
        {
            try
            {
                if (_employeeAttendanceData.TryGetValue(employeeId, out var employeeData))
                {
                    var result = employeeData.FirstOrDefault(a => a.Date.Date == date.Date);
                    // Log only occasionally to avoid huge log files
                    if (date.Day == 1 || date.Day == 15)
                    {
                        string status = result == null ? "null" : result.Status;
                        string hasCheckIn = result?.CheckInTime == null ? "No" : "Yes";
                        Program.LogMessage($"GetAttendanceStatus: Employee {employeeId}, Date {date:yyyy-MM-dd}, Found: {result != null}, Status: {status}, HasCheckIn: {hasCheckIn}");
                    }
                    return result;
                }
                
                // Log when employee data is missing
                if (date.Day == 1)
                {
                    Program.LogMessage($"GetAttendanceStatus: WARNING - No data found for employee {employeeId}");
                }
                return null;
            }
            catch (Exception ex)
            {
                Program.LogMessage($"GetAttendanceStatus: ERROR - {ex.Message} for employee {employeeId}, date {date:yyyy-MM-dd}");
                return null;
            }
        }
        
        // Export report to Excel or PDF
        private async Task ExportReportAsync()
        {
            if (_employeeAttendanceData.Count == 0)
            {
                // Show error message
                return;
            }
            
            // Implementation for exporting to Excel or PDF would go here
            await Task.Delay(100); // Placeholder
        }
        
        // Handle department checkbox changes
        public void OnDepartmentCheckedChanged(DepartmentTreeNode department)
        {
            // When a department is checked/unchecked, propagate to all its employees
            foreach (var employee in department.Employees)
            {
                employee.IsChecked = department.IsChecked;
            }
        }
        
        // Handle employee checkbox changes
        public void OnEmployeeCheckedChanged(DepartmentTreeNode department)
        {
            // Update the department's checked state based on its employees
            department.IsChecked = department.Employees.All(e => e.IsChecked);
        }
    }
    
    // Tree node classes
    public class DepartmentTreeNode : ObservableObject
    {
        private int _id;
        private string _name;
        private bool _isExpanded;
        private bool _isChecked;
        private ObservableCollection<EmployeeTreeNode> _employees = new();
        
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }
        
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        
        public bool IsExpanded
        {
            get => _isExpanded;
            set => SetProperty(ref _isExpanded, value);
        }
        
        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }
        
        public ObservableCollection<EmployeeTreeNode> Employees
        {
            get => _employees;
            set => SetProperty(ref _employees, value);
        }
    }
    
    public class EmployeeTreeNode : ObservableObject
    {
        private int _id;
        private string _name;
        private bool _isChecked;
        
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }
        
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }
        
        public bool IsChecked
        {
            get => _isChecked;
            set => SetProperty(ref _isChecked, value);
        }
    }
} 