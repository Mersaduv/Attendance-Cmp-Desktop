using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AttandenceDesktop.ViewModels
{
    public enum ReportTypeEnum
    {
        Employee,
        Department,
        Company
    }
    
    public partial class ReportViewModel : ViewModelBase
    {
        private readonly ReportService _reportService;
        private readonly EmployeeService _employeeService;
        private readonly DepartmentService _departmentService;
        
        private DateTime _startDate = DateTime.Today.AddDays(-30);
        private DateTime _endDate = DateTime.Today;
        private int? _selectedEmployeeId;
        private int? _selectedDepartmentId;
        private int _reportTypeIndex;
        private bool _isLoading;
        private AttendanceReportStatistics _statistics = new AttendanceReportStatistics();
        
        public ReportViewModel(
            ReportService reportService,
            EmployeeService employeeService,
            DepartmentService departmentService)
        {
            _reportService = reportService;
            _employeeService = employeeService;
            _departmentService = departmentService;
            
            GenerateReportCommand = new AsyncRelayCommand(GenerateReportAsync);
            ExportReportCommand = new AsyncRelayCommand(ExportReportAsync);
            
            // Initialize data asynchronously
            InitializeDataAsync();
        }
        
        private async Task InitializeDataAsync()
        {
            await LoadEmployeesAsync();
            await LoadDepartmentsAsync();
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
        
        public int? SelectedEmployeeId
        {
            get => _selectedEmployeeId;
            set => SetProperty(ref _selectedEmployeeId, value);
        }
        
        public int? SelectedDepartmentId
        {
            get => _selectedDepartmentId;
            set => SetProperty(ref _selectedDepartmentId, value);
        }
        
        public int ReportTypeIndex
        {
            get => _reportTypeIndex;
            set 
            {
                if (SetProperty(ref _reportTypeIndex, value))
                {
                    // Reset selected values when report type changes
                    if (_reportTypeIndex == 0) // Employee
                    {
                        SelectedDepartmentId = null;
                    }
                    else if (_reportTypeIndex == 1) // Department
                    {
                        SelectedEmployeeId = null;
                    }
                    else if (_reportTypeIndex == 2) // Company
                    {
                        SelectedEmployeeId = null;
                        SelectedDepartmentId = null;
                    }
                    
                    // Notify UI that IsEmployeeSelectionVisible and IsDepartmentSelectionVisible might have changed
                    OnPropertyChanged(nameof(IsEmployeeSelectionVisible));
                    OnPropertyChanged(nameof(IsDepartmentSelectionVisible));
                }
            }
        }
        
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        // Properties to control visibility of selection controls
        public bool IsEmployeeSelectionVisible => ReportTypeIndex == 0; // Employee
        public bool IsDepartmentSelectionVisible => ReportTypeIndex == 1; // Department
        
        public ObservableCollection<Employee> Employees { get; } = new();
        public ObservableCollection<Department> Departments { get; } = new();
        public ObservableCollection<AttendanceReportItem> ReportItems { get; } = new();
        
        public AttendanceReportStatistics Statistics
        {
            get => _statistics;
            private set => SetProperty(ref _statistics, value);
        }
        
        public ICommand GenerateReportCommand { get; }
        public ICommand ExportReportCommand { get; }
        
        private async Task LoadEmployeesAsync()
        {
            var employees = await _employeeService.GetAllAsync();
            Employees.Clear();
            foreach (var employee in employees)
            {
                Employees.Add(employee);
            }
        }
        
        private async Task LoadDepartmentsAsync()
        {
            var departments = await _departmentService.GetAllAsync();
            Departments.Clear();
            foreach (var department in departments)
            {
                Departments.Add(department);
            }
        }
        
        private async Task GenerateReportAsync()
        {
            if (StartDate > EndDate)
            {
                // Show error message
                return;
            }
            
            IsLoading = true;
            ReportItems.Clear();
            
            try
            {
                var report = new System.Collections.Generic.List<AttendanceReportItem>();
                
                switch (ReportTypeIndex)
                {
                    case 0: // Employee
                        if (SelectedEmployeeId.HasValue)
                        {
                            report = await _reportService.GenerateEmployeeAttendanceReportAsync(
                                SelectedEmployeeId.Value, StartDate, EndDate);
                        }
                        break;
                        
                    case 1: // Department
                        if (SelectedDepartmentId.HasValue)
                        {
                            report = await _reportService.GenerateDepartmentAttendanceReportAsync(
                                SelectedDepartmentId.Value, StartDate, EndDate);
                        }
                        break;
                        
                    case 2: // Company
                        report = await _reportService.GenerateCompanyAttendanceReportAsync(
                            StartDate, EndDate);
                        break;
                }
                
                foreach (var item in report)
                {
                    ReportItems.Add(item);
                }
                
                Statistics = _reportService.GetReportStatistics(report);
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private async Task ExportReportAsync()
        {
            if (ReportItems.Count == 0)
            {
                // Show error message
                return;
            }
            
            // Implementation for exporting to Excel or PDF would go here
            await Task.Delay(100); // Placeholder
        }
    }
} 