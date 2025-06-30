using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows.Input;
using System.Linq;
using System.Collections.Generic;
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
    
    public partial class ReportViewModel : ViewModelBase, IDisposable
    {
        private readonly ReportService _reportService;
        private readonly EmployeeService _employeeService;
        private readonly DepartmentService _departmentService;
        private readonly DataRefreshService _dataRefreshService;
        private readonly ExportService _exportService;
        private readonly ReportGridExportService _gridExportService;
        
        private DateTime _startDate = DateTime.Today.AddDays(-30);
        private DateTime _endDate = DateTime.Today;
        private int? _selectedEmployeeId;
        private int? _selectedDepartmentId;
        private int _reportTypeIndex;
        private bool _isLoading;
        private AttendanceReportStatistics _statistics = new AttendanceReportStatistics();
        
        // Pagination properties
        private const int _pageSize = 10;
        private int _currentPage = 1;
        private int _totalPages = 1;
        private bool _hasSummaryPage = false;
        private List<AttendanceReportItem> _allReportItems = new List<AttendanceReportItem>();
        private TimeSpan _totalLateMinutes = TimeSpan.Zero;
        private TimeSpan _totalEarlyDepartureMinutes = TimeSpan.Zero;
        private TimeSpan _totalOvertimeMinutes = TimeSpan.Zero;
        
        public ReportViewModel(
            ReportService reportService,
            EmployeeService employeeService,
            DepartmentService departmentService,
            DataRefreshService dataRefreshService,
            ExportService exportService,
            ReportGridExportService gridExportService)
        {
            _reportService = reportService;
            _employeeService = employeeService;
            _departmentService = departmentService;
            _dataRefreshService = dataRefreshService;
            _exportService = exportService;
            _gridExportService = gridExportService;
            
            GenerateReportCommand = new AsyncRelayCommand(GenerateReportAsync);
            ExportReportCommand = new AsyncRelayCommand(async () => await ExportToFormat("excel"));
            NextPageCommand = new RelayCommand(NextPage, CanGoToNextPage);
            PreviousPageCommand = new RelayCommand(PreviousPage, CanGoToPreviousPage);
            
            // Subscribe to data change events
            _dataRefreshService.AttendanceChanged += OnAttendanceChanged;
            _dataRefreshService.EmployeesChanged += OnEmployeesChanged;
            _dataRefreshService.DepartmentsChanged += OnDepartmentsChanged;
            
            // Initialize data asynchronously
            InitializeDataAsync();
            
            // Ensure CanExport initial state
            OnPropertyChanged(nameof(CanExport));
        }
        
        // Parameterless constructor for design-time support
        public ReportViewModel()
        {
            _reportService = null!;
            _employeeService = null!;
            _departmentService = null!;
            _dataRefreshService = null!;
            _exportService = null!;
            _gridExportService = null!;
            
            GenerateReportCommand = new AsyncRelayCommand(async () => {});
            ExportReportCommand = new AsyncRelayCommand(async () => {});
            NextPageCommand = new RelayCommand(() => {});
            PreviousPageCommand = new RelayCommand(() => {});
            
            // Add design-time data
            Employees = new ObservableCollection<Employee>();
            Departments = new ObservableCollection<Department>();
            ReportItems = new ObservableCollection<AttendanceReportItem>();
            
            // Create sample statistics
            _statistics = new AttendanceReportStatistics();
            // We can't set read-only properties directly, but we can create a new instance with some values
            _statistics = new AttendanceReportStatistics
            {
                PresentDays = 20,
                AbsentDays = 2,
                LateArrivals = 3,
                EarlyDepartures = 1,
                OvertimeDays = 5
            };
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
            // If we have an active report, refresh it
            if (_allReportItems.Count > 0)
            {
                _ = GenerateReportAsync();
            }
        }
        
        private void OnEmployeesChanged(object? sender, EventArgs e)
        {
            _ = LoadEmployeesAsync();
        }
        
        private void OnDepartmentsChanged(object? sender, EventArgs e)
        {
            _ = LoadDepartmentsAsync();
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
            set
            {
                if (SetProperty(ref _isLoading, value))
                {
                    OnPropertyChanged(nameof(CanExport));
                }
            }
        }
        
        // Properties to control visibility of selection controls
        public bool IsEmployeeSelectionVisible => ReportTypeIndex == 0; // Employee
        public bool IsDepartmentSelectionVisible => ReportTypeIndex == 1; // Department
        
        // Pagination properties
        public int CurrentPage
        {
            get => _currentPage;
            set
            {
                if (_currentPage != value && value >= 1 && value <= _totalPages)
                {
                    SetProperty(ref _currentPage, value);
                    UpdateDisplayedItems();
                    
                    // Update command availability
                    ((RelayCommand)NextPageCommand).NotifyCanExecuteChanged();
                    ((RelayCommand)PreviousPageCommand).NotifyCanExecuteChanged();
                    
                    OnPropertyChanged(nameof(PageInfo));
                    OnPropertyChanged(nameof(IsLastPage));
                }
            }
        }
        
        public int TotalPages
        {
            get => _totalPages;
            private set => SetProperty(ref _totalPages, value);
        }
        
        public string PageInfo => $"{CurrentPage} / {TotalPages}";
        
        public bool IsLastPage => _hasSummaryPage && CurrentPage == TotalPages;
        
        public ICommand NextPageCommand { get; }
        public ICommand PreviousPageCommand { get; }
        
        private bool CanGoToNextPage() => CurrentPage < TotalPages && TotalPages > 1;
        private bool CanGoToPreviousPage() => CurrentPage > 1;
        
        private async void NextPage()
        {
            if (CurrentPage < TotalPages)
            {
                CurrentPage++;
                ((RelayCommand)NextPageCommand).NotifyCanExecuteChanged();
                ((RelayCommand)PreviousPageCommand).NotifyCanExecuteChanged();
                
                // Ensure complete data is loaded for the new page
                await UpdateDisplayedItemsAsync();
            }
        }
        
        private async void PreviousPage()
        {
            if (CurrentPage > 1)
            {
                CurrentPage--;
                ((RelayCommand)NextPageCommand).NotifyCanExecuteChanged();
                ((RelayCommand)PreviousPageCommand).NotifyCanExecuteChanged();
                
                // Ensure complete data is loaded for the new page
                await UpdateDisplayedItemsAsync();
            }
        }
        
        // Summary information for the last page
        public int AbsentDays => Statistics.AbsentDays;
        
        public TimeSpan TotalLateMinutes => _totalLateMinutes;
        public string TotalLateTime => $"{(int)TotalLateMinutes.TotalHours:00}:{TotalLateMinutes.Minutes:00}";
        
        public TimeSpan TotalEarlyDepartureMinutes => _totalEarlyDepartureMinutes;
        public string TotalEarlyDepartureTime => $"{(int)TotalEarlyDepartureMinutes.TotalHours:00}:{TotalEarlyDepartureMinutes.Minutes:00}";
        
        public TimeSpan TotalOvertimeMinutes => _totalOvertimeMinutes;
        public string TotalOvertimeTime => $"{(int)TotalOvertimeMinutes.TotalHours:00}:{TotalOvertimeMinutes.Minutes:00}";
        
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
            Program.LogMessage($"ReportViewModel: Starting report generation. Type: {(ReportTypeEnum)_reportTypeIndex}, StartDate: {StartDate:yyyy-MM-dd}, EndDate: {EndDate:yyyy-MM-dd}");
            
            if (StartDate > EndDate)
            {
                Program.LogMessage("ReportViewModel: Error - Start date is after end date");
                // Handle error - we could show a message to the user here
                return;
            }
            
            IsLoading = true;
            _allReportItems.Clear();
            _totalPages = 1;
            _currentPage = 1;
            
            Program.LogMessage($"ReportViewModel: Cleared previous report data. Starting data fetch...");
            
            try
            {
                // Get complete attendance data for the selected entity
                _allReportItems = await GetCompleteDataAsync();
                OnPropertyChanged(nameof(CanExport));
                
                // Calculate statistics based on all data
                Program.LogMessage("ReportViewModel: Calculating report statistics");
                _statistics = _reportService.GetReportStatistics(_allReportItems);
                
                // Calculate aggregate minutes across all data
                var timeValues = await GetCompleteTimeValuesAsync();
                _totalLateMinutes = timeValues["LateMinutes"];
                _totalEarlyDepartureMinutes = timeValues["EarlyDepartureMinutes"];
                _totalOvertimeMinutes = timeValues["OvertimeMinutes"];
                
                Program.LogMessage($"ReportViewModel: Calculated time totals - Late: {_totalLateMinutes}, Early Departure: {_totalEarlyDepartureMinutes}, Overtime: {_totalOvertimeMinutes}");
                
                // Calculate pagination
                int itemCount = _allReportItems.Count;
                _hasSummaryPage = itemCount > 0 ? true : false;
                
                // Fix the pagination calculation
                if (itemCount == 0)
                {
                    _totalPages = 1;
                }
                else
                {
                    // Calculate regular pages (full page size items per page)
                    int regularPages = (itemCount / _pageSize);
                    
                    // Add an extra page if there are remaining items
                    if (itemCount % _pageSize > 0)
                    {
                        regularPages++;
                    }
                    
                    // Add summary page if needed
                    _totalPages = _hasSummaryPage ? regularPages + 1 : regularPages;
                }
                
                Program.LogMessage($"ReportViewModel: Pagination setup - Items: {itemCount}, Pages: {_totalPages}");
                
                // Update UI bindings
                OnPropertyChanged(nameof(TotalPages));
                OnPropertyChanged(nameof(PageInfo));
                OnPropertyChanged(nameof(Statistics));
                OnPropertyChanged(nameof(TotalLateMinutes));
                OnPropertyChanged(nameof(TotalLateTime));
                OnPropertyChanged(nameof(TotalEarlyDepartureMinutes));
                OnPropertyChanged(nameof(TotalEarlyDepartureTime));
                OnPropertyChanged(nameof(TotalOvertimeMinutes));
                OnPropertyChanged(nameof(TotalOvertimeTime));
                
                // Force command can-execute refresh
                ((RelayCommand)NextPageCommand).NotifyCanExecuteChanged();
                ((RelayCommand)PreviousPageCommand).NotifyCanExecuteChanged();
                
                // Update displayed items for the current page
                await UpdateDisplayedItemsAsync();
                Program.LogMessage("ReportViewModel: Report generation completed successfully");
            }
            catch (Exception ex)
            {
                Program.LogMessage($"ReportViewModel: ERROR during report generation - {ex.Message}\n{ex.StackTrace}");
                // Handle error - could show message to user
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private void UpdateDisplayedItems()
        {
            // Use the async version by calling it and not waiting
            _ = UpdateDisplayedItemsAsync();
        }
        
        private async Task ExportReportAsync(string format)
        {
            if (_exportService == null)
                return;

            if (_allReportItems.Count == 0)
                return;

            try
            {
                IsLoading = true;

                // Determine default extension
                var defaultExt = format.ToLower() switch
                {
                    "excel" or "xlsx" => "xlsx",
                    "pdf" => "pdf",
                    "csv" => "csv",
                    "word" or "docx" => "docx",
                    "txt" => "txt",
                    _ => format
                };

                var defaultFileName = $"AttendanceReport_{DateTime.Now:yyyyMMdd_HHmmss}.{defaultExt}";
                var defaultDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

                string? filePath = null;

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(async () =>
                {
                    var topLevel = Avalonia.Application.Current?.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime;
                    var mainWindow = topLevel?.MainWindow;

                    if (mainWindow != null)
                    {
                        var fileOptions = new Avalonia.Platform.Storage.FilePickerSaveOptions
                        {
                            Title = $"Save {format.ToUpper()} Report",
                            SuggestedFileName = defaultFileName,
                            DefaultExtension = defaultExt
                        };

                        switch (format.ToLower())
                        {
                            case "excel":
                            case "xlsx":
                                fileOptions.FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Excel Files") { Patterns = new[] { "*.xlsx" } } };
                                break;
                            case "pdf":
                                fileOptions.FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("PDF Files") { Patterns = new[] { "*.pdf" } } };
                                break;
                            case "csv":
                                fileOptions.FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("CSV Files") { Patterns = new[] { "*.csv" } } };
                                break;
                            case "word":
                            case "docx":
                                fileOptions.FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Word Files") { Patterns = new[] { "*.docx" } } };
                                break;
                            case "txt":
                                fileOptions.FileTypeChoices = new[] { new Avalonia.Platform.Storage.FilePickerFileType("Text Files") { Patterns = new[] { "*.txt" } } };
                                break;
                        }

                        var file = await mainWindow.StorageProvider.SaveFilePickerAsync(fileOptions);
                        if (file != null)
                        {
                            filePath = file.Path.LocalPath;
                        }
                    }
                });

                if (string.IsNullOrWhiteSpace(filePath))
                {
                    return;
                }

                // If excel, generate grid-style export
                if (format.Equals("excel", StringComparison.OrdinalIgnoreCase) || format.Equals("xlsx", StringComparison.OrdinalIgnoreCase))
                {
                    await Task.Run(async () => await _gridExportService.ExportToExcelAsync(_allReportItems, filePath));
                }
                else
                {
                    await Task.Run(async () => await _exportService.ExportAsync(_allReportItems, format, filePath));
                }
            }
            catch (Exception ex)
            {
                Program.LogMessage($"ReportViewModel Export error: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        // Export helper that is invoked by the view (from context menu)
        public async Task ExportToFormat(string format)
        {
            if (!CanExport || string.IsNullOrWhiteSpace(format))
                return;

            await ExportReportAsync(format);
        }
        
        // Indicates whether the export button should be enabled
        public bool CanExport => _allReportItems.Count > 0 && !IsLoading;
        
        // Get complete attendance data for an entity (employee/department/company) across all date range
        private async Task<List<AttendanceReportItem>> GetCompleteDataAsync()
        {
            List<AttendanceReportItem> reportItems = new();
            
            try
            {
                // Based on the report type, call the appropriate service method
                switch ((ReportTypeEnum)ReportTypeIndex)
                {
                    case ReportTypeEnum.Employee:
                        if (SelectedEmployeeId.HasValue)
                        {
                            Program.LogMessage($"ReportViewModel: Fetching complete data for employee ID: {SelectedEmployeeId}");
                            reportItems = await _reportService.GenerateEmployeeAttendanceReportAsync(
                                SelectedEmployeeId.Value, StartDate, EndDate);
                            Program.LogMessage($"ReportViewModel: Fetched {reportItems.Count} complete records for employee");
                        }
                        break;
                        
                    case ReportTypeEnum.Department:
                        if (SelectedDepartmentId.HasValue)
                        {
                            Program.LogMessage($"ReportViewModel: Fetching complete data for department ID: {SelectedDepartmentId}");
                            reportItems = await _reportService.GenerateDepartmentAttendanceReportAsync(
                                SelectedDepartmentId.Value, StartDate, EndDate);
                            Program.LogMessage($"ReportViewModel: Fetched {reportItems.Count} complete records for department");
                        }
                        break;
                        
                    case ReportTypeEnum.Company:
                        Program.LogMessage("ReportViewModel: Fetching complete company-wide data");
                        reportItems = await _reportService.GenerateCompanyAttendanceReportAsync(
                            StartDate, EndDate);
                        Program.LogMessage($"ReportViewModel: Fetched {reportItems.Count} complete records for company report");
                        break;
                }
            }
            catch (Exception ex)
            {
                Program.LogMessage($"ReportViewModel: Error fetching complete data - {ex.Message}");
            }
            
            return reportItems;
        }
        
        // Calculate complete statistics across all date range and pages
        public async Task<AttendanceReportStatistics> GetCompleteStatisticsAsync()
        {
            // If we already have complete data, use it
            if (_allReportItems.Count > 0)
            {
                return _reportService.GetReportStatistics(_allReportItems);
            }
            
            // Otherwise, fetch all data and calculate statistics
            var completeData = await GetCompleteDataAsync();
            return _reportService.GetReportStatistics(completeData);
        }
        
        // Calculate aggregate time values across all dates
        public async Task<Dictionary<string, TimeSpan>> GetCompleteTimeValuesAsync()
        {
            var result = new Dictionary<string, TimeSpan>
            {
                { "LateMinutes", TimeSpan.Zero },
                { "EarlyDepartureMinutes", TimeSpan.Zero },
                { "OvertimeMinutes", TimeSpan.Zero }
            };
            
            // If we already have complete data, use it
            List<AttendanceReportItem> dataToProcess;
            if (_allReportItems.Count > 0)
            {
                dataToProcess = _allReportItems;
            }
            else
            {
                // Otherwise, fetch all data
                dataToProcess = await GetCompleteDataAsync();
            }
            
            // Calculate totals
            foreach (var item in dataToProcess)
            {
                if (item.LateMinutes.HasValue)
                    result["LateMinutes"] += item.LateMinutes.Value;
                if (item.EarlyDepartureMinutes.HasValue)
                    result["EarlyDepartureMinutes"] += item.EarlyDepartureMinutes.Value;
                if (item.OvertimeMinutes.HasValue)
                    result["OvertimeMinutes"] += item.OvertimeMinutes.Value;
            }
            
            return result;
        }
        
        private async Task UpdateDisplayedItemsAsync()
        {
            Program.LogMessage($"ReportViewModel: Updating displayed items for page {CurrentPage} of {TotalPages}");
            
            ReportItems.Clear();
            
            if (_allReportItems.Count == 0)
            {
                Program.LogMessage("ReportViewModel: No items to display");
                return;
            }
            
            // Check if we're showing the summary page (last page)
            if (_hasSummaryPage && CurrentPage == TotalPages)
            {
                Program.LogMessage("ReportViewModel: Displaying summary page");
                // This is the summary page - we'll just leave it empty for now
                // The statistics are shown separately in the UI
                
                // Make sure Next/Previous commands are updated
                ((RelayCommand)NextPageCommand).NotifyCanExecuteChanged();
                ((RelayCommand)PreviousPageCommand).NotifyCanExecuteChanged();
                return;
            }
            
            // Calculate start and end index for the current page
            int startIndex = (CurrentPage - 1) * _pageSize;
            if (startIndex < 0) startIndex = 0;
            
            // If startIndex is beyond our collection, show empty page
            if (startIndex >= _allReportItems.Count)
            {
                Program.LogMessage("ReportViewModel: Start index beyond collection bounds");
                return;
            }
            
            // Calculate end index, ensuring we don't go beyond collection bounds
            int endIndex = Math.Min(startIndex + _pageSize - 1, _allReportItems.Count - 1);
            
            Program.LogMessage($"ReportViewModel: Displaying items {startIndex} to {endIndex} (Total: {_allReportItems.Count})");
            
            // Add items for the current page
            for (int i = startIndex; i <= endIndex; i++)
            {
                ReportItems.Add(_allReportItems[i]);
            }
            
            // Make sure Next/Previous commands are updated
            ((RelayCommand)NextPageCommand).NotifyCanExecuteChanged();
            ((RelayCommand)PreviousPageCommand).NotifyCanExecuteChanged();
            
            Program.LogMessage($"ReportViewModel: Added {ReportItems.Count} items to display");
        }
        
        // Call these methods from the View to ensure complete statistics
        public async Task RefreshStatisticsAsync()
        {
            try
            {
                // Calculate statistics from all data, not just current page
                _statistics = await GetCompleteStatisticsAsync();
                
                // Update time values
                var timeValues = await GetCompleteTimeValuesAsync();
                _totalLateMinutes = timeValues["LateMinutes"];
                _totalEarlyDepartureMinutes = timeValues["EarlyDepartureMinutes"];
                _totalOvertimeMinutes = timeValues["OvertimeMinutes"];
                
                // Notify properties
                OnPropertyChanged(nameof(Statistics));
                OnPropertyChanged(nameof(TotalLateMinutes));
                OnPropertyChanged(nameof(TotalLateTime));
                OnPropertyChanged(nameof(TotalEarlyDepartureMinutes));
                OnPropertyChanged(nameof(TotalEarlyDepartureTime));
                OnPropertyChanged(nameof(TotalOvertimeMinutes));
                OnPropertyChanged(nameof(TotalOvertimeTime));
            }
            catch (Exception ex)
            {
                Program.LogMessage($"ReportViewModel: Error refreshing statistics - {ex.Message}");
            }
        }
    }
} 