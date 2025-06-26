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
using System.Threading;

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
        
        // Flag to prevent circular event handling
        private bool _isUpdatingCheckStates = false;
        
        // Pagination properties
        private int _employeesPerPage = 10;
        private int _daysPerPage = 30;
        private int _currentEmployeePage = 0;
        private int _currentDatePage = 0;
        private int _totalEmployeePages = 0;
        private int _totalDatePages = 0;
        
        // Private fields for progress tracking
        private int _totalItems;
        private int _processedItems;
        private string _progressStatus = string.Empty;
        private double _progressPercentage = 0;
        private CancellationTokenSource? _cancellationTokenSource;
        
        // Lazy loading tracking
        private HashSet<int> _loadedDatePages = new HashSet<int>();
        private bool _isLoadingPage = false;
        
        // Progress properties
        public string ProgressStatus 
        {
            get => _progressStatus;
            private set => SetProperty(ref _progressStatus, value);
        }
        
        public double ProgressPercentage
        {
            get => _progressPercentage;
            private set => SetProperty(ref _progressPercentage, value);
        }
        
        public ICommand CancelReportCommand { get; }
        
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
            CancelReportCommand = new RelayCommand(CancelReport);
            SelectAllCommand = new RelayCommand(SelectAllEmployees);
            
            // Initialize pagination commands
            NextEmployeePageCommand = new RelayCommand(NextEmployeePage, CanGoToNextEmployeePage);
            PreviousEmployeePageCommand = new RelayCommand(PreviousEmployeePage, CanGoToPreviousEmployeePage);
            NextDatePageCommand = new RelayCommand(async () => await NextDatePageAsync(), CanGoToNextDatePage);
            PreviousDatePageCommand = new RelayCommand(async () => await PreviousDatePageAsync(), CanGoToPreviousDatePage);
            
            // Load initial data
            Task.Run(InitializeDataAsync);
        }
        
        // Employee pagination commands
        public ICommand NextEmployeePageCommand { get; }
        public ICommand PreviousEmployeePageCommand { get; }
        public ICommand NextDatePageCommand { get; }
        public ICommand PreviousDatePageCommand { get; }
        
        // Employee pagination properties
        public int CurrentEmployeePage
        {
            get => _currentEmployeePage;
            set
            {
                if (SetProperty(ref _currentEmployeePage, value))
                {
                    OnPropertyChanged(nameof(CurrentPageEmployees));
                    OnPropertyChanged(nameof(EmployeePageInfo));
                    ((RelayCommand)NextEmployeePageCommand).NotifyCanExecuteChanged();
                    ((RelayCommand)PreviousEmployeePageCommand).NotifyCanExecuteChanged();
                }
            }
        }
        
        public int TotalEmployeePages
        {
            get => _totalEmployeePages;
            set => SetProperty(ref _totalEmployeePages, value);
        }
        
        public int CurrentDatePage
        {
            get => _currentDatePage;
            set
            {
                if (SetProperty(ref _currentDatePage, value))
                {
                    OnPropertyChanged(nameof(CurrentPageDates));
                    OnPropertyChanged(nameof(DatePageInfo));
                    ((RelayCommand)NextDatePageCommand).NotifyCanExecuteChanged();
                    ((RelayCommand)PreviousDatePageCommand).NotifyCanExecuteChanged();
                }
            }
        }
        
        public int TotalDatePages
        {
            get => _totalDatePages;
            set => SetProperty(ref _totalDatePages, value);
        }
        
        // Page info text properties
        public string EmployeePageInfo => $"Page {CurrentEmployeePage + 1} of {(TotalEmployeePages > 0 ? TotalEmployeePages : 1)}";
        public string DatePageInfo => $"Page {CurrentDatePage + 1} of {(TotalDatePages > 0 ? TotalDatePages : 1)}";
        
        // Get current page employee IDs
        public List<int> CurrentPageEmployees
        {
            get
            {
                var allEmployees = SelectedEmployeeIds.ToList();
                if (allEmployees.Count == 0)
                    return new List<int>();
                    
                // Log page information for debugging
                Program.LogMessage($"CurrentPageEmployees: Page {CurrentEmployeePage + 1}/{TotalEmployeePages}, Total employees: {allEmployees.Count}");
                    
                int startIndex = CurrentEmployeePage * _employeesPerPage;

                // Ensure startIndex is within valid range
                if (startIndex >= allEmployees.Count)
                {
                    // If somehow we got an invalid page, reset to first page
                    if (TotalEmployeePages > 0)
                    {
                        Program.LogMessage($"CurrentPageEmployees: Invalid page {CurrentEmployeePage + 1}, resetting to page 1");
                        CurrentEmployeePage = 0;
                        startIndex = 0;
                    }
                    else
                    {
                        return new List<int>();
                    }
                }
                
                var pageEmployees = allEmployees
                    .Skip(startIndex)
                    .Take(_employeesPerPage)
                    .ToList();
                    
                Program.LogMessage($"CurrentPageEmployees: Retrieved {pageEmployees.Count} employees for page {CurrentEmployeePage + 1}");
                
                return pageEmployees;
            }
        }
        
        // Get current page dates
        public List<DateTime> CurrentPageDates
        {
            get
            {
                var allDates = DateRange.ToList();
                if (allDates.Count == 0)
                    return new List<DateTime>();
                    
                int startIndex = CurrentDatePage * _daysPerPage;
                return allDates
                    .Skip(startIndex)
                    .Take(_daysPerPage)
                    .ToList();
            }
        }
        
        // Navigation methods
        private async void NextEmployeePage()
        {
            VerifyPaginationState();
            if (CurrentEmployeePage < TotalEmployeePages - 1)
            {
                // First make sure we have a transition indicator of some sort - optional UI effect
                IsLoading = true;
                ProgressStatus = "Loading next page...";
                
                CurrentEmployeePage++;
                
                // Reset cached attendance status to ensure fresh data
                ClearAttendanceCache();
                
                // Force immediate loading of data for the new current page 
                // We use 'await' here (instead of Task.Run) to ensure loading is complete before UI updates
                await LoadDatePageDataForAllEmployeesAsync(CurrentDatePage);
                
                // Pre-load next page data if available
                if (CurrentEmployeePage < TotalEmployeePages - 1)
                {
                    Task.Run(async () => await PreloadEmployeePageDataAsync(CurrentEmployeePage + 1, CurrentDatePage));
                }
                
                IsLoading = false;
            }
        }
        
        private async void PreviousEmployeePage()
        {
            VerifyPaginationState();
            if (CurrentEmployeePage > 0)
            {
                // First make sure we have a transition indicator of some sort
                IsLoading = true;
                ProgressStatus = "Loading previous page...";
                
                CurrentEmployeePage--;
                
                // Reset cached attendance status to ensure fresh data
                ClearAttendanceCache();
                
                // Force immediate loading of data for the new current page
                // We use 'await' here to ensure loading is complete before UI updates
                await LoadDatePageDataForAllEmployeesAsync(CurrentDatePage);
                
                IsLoading = false;
            }
        }
        
        private async Task NextDatePageAsync()
        {
            VerifyPaginationState();
            if (CurrentDatePage < TotalDatePages - 1)
            {
                int nextPage = CurrentDatePage + 1;
                // Load data for the next page if not already loaded
                if (!_loadedDatePages.Contains(nextPage))
                {
                    await LoadDatePageDataAsync(nextPage);
                }
                CurrentDatePage = nextPage;
            }
        }
        
        private async Task PreviousDatePageAsync()
        {
            VerifyPaginationState();
            if (CurrentDatePage > 0)
            {
                int prevPage = CurrentDatePage - 1;
                // Load data for the previous page if not already loaded
                if (!_loadedDatePages.Contains(prevPage))
                {
                    await LoadDatePageDataAsync(prevPage);
                }
                CurrentDatePage = prevPage;
            }
        }
        
        // Check which pages need to be loaded
        public bool IsDatePageLoaded(int page)
        {
            return _loadedDatePages.Contains(page);
        }
        
        // Verify pagination state to ensure accurate navigation
        private void VerifyPaginationState()
        {
            // Recalculate total pages based on current data
            int calculatedEmployeePages = SelectedEmployeeIds.Count > 0 
                ? (int)Math.Ceiling(SelectedEmployeeIds.Count / (double)_employeesPerPage) 
                : 0;
                
            int calculatedDatePages = DateRange.Count > 0 
                ? (int)Math.Ceiling(DateRange.Count / (double)_daysPerPage) 
                : 0;
            
            // If there's a mismatch, update the values
            bool needSync = false;
            
            if (TotalEmployeePages != calculatedEmployeePages)
            {
                TotalEmployeePages = calculatedEmployeePages;
                needSync = true;
            }
            
            if (TotalDatePages != calculatedDatePages)
            {
                TotalDatePages = calculatedDatePages;
                needSync = true;
            }
            
            // If we updated page counts, synchronize UI
            if (needSync)
            {
                SyncPaginationState();
            }
        }
        
        // Load data for a specific date page
        private async Task LoadDatePageDataAsync(int datePageToLoad)
        {
            // Prevent multiple loads at the same time
            if (_isLoadingPage || datePageToLoad < 0 || datePageToLoad >= TotalDatePages)
                return;
                
            try
            {
                _isLoadingPage = true;
                Program.LogMessage($"OverviewReport: Loading data for date page {datePageToLoad + 1}");
                ProgressStatus = $"Loading page {datePageToLoad + 1}...";
                
                // Calculate date range for this page
                int startIdx = datePageToLoad * _daysPerPage;
                var dateRangeForPage = DateRange.Skip(startIdx).Take(_daysPerPage).ToList();
                if (dateRangeForPage.Count == 0)
                    return;
                    
                var startDateForPage = dateRangeForPage.First();
                var endDateForPage = dateRangeForPage.Last();
                
                // Always use current page employees - this fixes the pagination bug
                var employeesToLoad = CurrentPageEmployees;
                
                // Debug log
                Program.LogMessage($"OverviewReport: Loading data for {employeesToLoad.Count} employees on page {CurrentEmployeePage + 1}");
                
                // Load data for each employee for this date page
                int counter = 0;
                foreach (var employeeId in employeesToLoad)
                {
                    // Ensure we have a collection for this employee
                    if (!_employeeAttendanceData.ContainsKey(employeeId))
                    {
                        _employeeAttendanceData[employeeId] = new List<AttendanceReportItem>();
                    }
                    
                    var existingData = _employeeAttendanceData[employeeId];
                    
                    // Check which dates we already have data for
                    var existingDates = existingData
                        .Select(item => item.Date.Date)
                        .ToHashSet();
                        
                    // Only fetch dates that are missing for this page
                    var missingDates = dateRangeForPage
                        .Where(date => !existingDates.Contains(date.Date))
                        .ToList();
                    
                    if (missingDates.Count > 0)
                    {
                        Program.LogMessage($"OverviewReport: Fetching {missingDates.Count} missing dates for employee {employeeId}");
                        
                        // Get attendance data for the entire page date range
                        // This is more efficient than getting individual days
                        var employeeReport = await _reportService.GenerateEmployeeAttendanceReportAsync(
                            employeeId, startDateForPage, endDateForPage);
                            
                            // Merge new data with existing data
                            foreach (var item in employeeReport)
                            {
                                if (!existingDates.Contains(item.Date.Date))
                                {
                                    existingData.Add(item);
                                    existingDates.Add(item.Date.Date);
                                }
                            }
                    }
                    else
                    {
                        Program.LogMessage($"OverviewReport: No missing dates for employee {employeeId} on current page");
                    }
                    
                    counter++;
                    ProgressPercentage = (counter * 100.0) / employeesToLoad.Count;
                }
                
                // Mark this page as loaded
                if (!_loadedDatePages.Contains(datePageToLoad))
                {
                    _loadedDatePages.Add(datePageToLoad);
                }
                
                Program.LogMessage($"OverviewReport: Loaded data for date page {datePageToLoad + 1} for {employeesToLoad.Count} employees");
                ProgressStatus = "Page loaded";
                ClearAttendanceCache();
            }
            finally
            {
                _isLoadingPage = false;
                OnPropertyChanged(nameof(GetAttendanceStatus));
            }
        }
        
        // Can-execute methods
        private bool CanGoToNextEmployeePage() => TotalEmployeePages > 0 && CurrentEmployeePage < TotalEmployeePages - 1;
        private bool CanGoToPreviousEmployeePage() => TotalEmployeePages > 0 && CurrentEmployeePage > 0;
        private bool CanGoToNextDatePage() => TotalDatePages > 0 && CurrentDatePage < TotalDatePages - 1;
        private bool CanGoToPreviousDatePage() => TotalDatePages > 0 && CurrentDatePage > 0;
        
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
                // Clear pagination state before regenerating
                _loadedDatePages.Clear();
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
            set 
            {
                if (_startDate != value)
                {
                    SetProperty(ref _startDate, value);
                    // Reset pagination info if we have existing data
                    if (_employeeAttendanceData.Count > 0)
                    {
                        _loadedDatePages.Clear();
                    }
                }
            }
        }
        
        public DateTime EndDate
        {
            get => _endDate;
            set 
            {
                if (_endDate != value)
                {
                    SetProperty(ref _endDate, value);
                    // Reset pagination info if we have existing data
                    if (_employeeAttendanceData.Count > 0)
                    {
                        _loadedDatePages.Clear();
                    }
                }
            }
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
        public ICommand SelectAllCommand { get; }
        
        // Method to load department and employee hierarchy
        private async Task LoadDepartmentTreeAsync()
        {
            Program.LogMessage("LoadDepartmentTreeAsync: Loading departments and employees");
            
            // Get all departments
            var departments = await _departmentService.GetAllAsync();
            
            // Save current selected employee IDs before clearing
            var currentlySelectedEmployees = new HashSet<int>(SelectedEmployeeIds);
            
            // Clear department tree
            DepartmentTree.Clear();
            
            foreach (var department in departments)
            {
                // Get employees for this department
                var employees = await _employeeService.GetByDepartmentAsync(department.Id);
                var employeeNodes = new ObservableCollection<EmployeeTreeNode>();
                
                // Create employee nodes
                foreach (var employee in employees)
                {
                    bool wasSelected = currentlySelectedEmployees.Contains(employee.Id);
                    
                    employeeNodes.Add(new EmployeeTreeNode
                    {
                        Id = employee.Id,
                        Name = employee.FullName,
                        IsChecked = wasSelected
                    });
                }
                
                // Only add departments that have employees
                if (employeeNodes.Count > 0)
                {
                    // Check if any employees in this department were selected
                    bool anyEmployeeSelected = employeeNodes.Any(e => e.IsChecked);
                    
                    var deptNode = new DepartmentTreeNode
                    {
                        Id = department.Id,
                        Name = department.Name,
                        Employees = employeeNodes,
                        IsExpanded = anyEmployeeSelected, // Auto-expand if any employee is selected
                        IsChecked = anyEmployeeSelected   // Auto-check if any employee is selected
                    };

                    // Subscribe to department checkbox changes
                    deptNode.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(DepartmentTreeNode.IsChecked) && !_isUpdatingCheckStates)
                        {
                            OnDepartmentCheckedChanged(deptNode);
                        }
                    };
                    
                    // Subscribe to employee checkbox changes
                    foreach (var empNode in employeeNodes)
                    {
                        empNode.PropertyChanged += (s, e) =>
                        {
                            if (e.PropertyName == nameof(EmployeeTreeNode.IsChecked) && !_isUpdatingCheckStates)
                            {
                                OnEmployeeCheckedChanged(deptNode, empNode);
                            }
                        };
                    }
                    
                    DepartmentTree.Add(deptNode);
                }
            }
            
            // Initialize check state after loading
            InitializeDepartmentTreeCheckState();
            
            Program.LogMessage($"LoadDepartmentTreeAsync: Loaded {DepartmentTree.Count} departments with employees");
        }
        
        private void CancelReport()
        {
            _cancellationTokenSource?.Cancel();
            Program.LogMessage("OverviewReport: Report generation cancelled by user");
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
            
            // Cancel any ongoing operation
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();
            var cancellationToken = _cancellationTokenSource.Token;
            
            IsLoading = true;
            
            // Reset all state from previous reports
            _employeeAttendanceData.Clear();
            ClearAttendanceCache();
            _loadedDatePages.Clear();
            _recoveryAttempted.Clear(); // Reset recovery attempts
            TotalEmployeePages = 0;
            TotalDatePages = 0;
            CurrentEmployeePage = 0;
            CurrentDatePage = 0;
            DateRange.Clear();
            
            // Reset pagination and notify UI
            OnPropertyChanged(nameof(DatePageInfo));
            OnPropertyChanged(nameof(EmployeePageInfo));
            
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
                
                int selectedEmployeeCount = SelectedEmployeeIds.Count;
                Program.LogMessage($"OverviewReport: {selectedEmployeeCount} employees selected");
                
                if (selectedEmployeeCount == 0)
                {
                    IsLoading = false;
                    return;
                }
                
                // Initialize progress tracking
                _totalItems = selectedEmployeeCount;
                _processedItems = 0;
                ProgressPercentage = 0;
                ProgressStatus = "Preparing data...";
                
                // Create date range for the new report
                
                // Check if we have too many days - optimize for large date ranges
                var totalDays = (EndDate - StartDate).Days + 1;
                var maxDaysToProcess = Math.Min(totalDays, 366); // Cap at 1 year to prevent excessive memory usage
                
                for (var date = StartDate; date <= EndDate && (date - StartDate).Days < maxDaysToProcess; date = date.AddDays(1))
                {
                    DateRange.Add(date);
                }
                
                Program.LogMessage($"OverviewReport: Added {DateRange.Count} dates to date range");
                
                // Calculate pagination values
                TotalEmployeePages = SelectedEmployeeIds.Count > 0 ? 
                    (int)Math.Ceiling(SelectedEmployeeIds.Count / (double)_employeesPerPage) : 0;
                TotalDatePages = DateRange.Count > 0 ? 
                    (int)Math.Ceiling(DateRange.Count / (double)_daysPerPage) : 0;
                
                // Reset to first page
                CurrentEmployeePage = 0;
                CurrentDatePage = 0;
                
                // Load data for first page, then preload second page
                ProgressStatus = "Loading first page data...";
                await LoadDatePageDataForAllEmployeesAsync(0);
                
                // Synchronize pagination state and update UI
                SyncPaginationState();
                
                // If we have more pages, try to preload the second employee page in the background
                if (TotalEmployeePages > 1)
                {
                    ProgressStatus = "Preloading next page data...";
                    Task.Run(async () => await PreloadEmployeePageDataAsync(1, 0));
                }
                
                // Also preload complete statistics for the first few employees
                // This will make summaries show correctly without waiting
                Task.Run(async () => 
                {
                    try 
                    {
                        var firstPageEmployees = CurrentPageEmployees;
                        Program.LogMessage($"Preloading complete statistics for {Math.Min(5, firstPageEmployees.Count)} employees");
                        
                        // Load first 5 employees or less
                        int count = 0;
                        foreach (var empId in firstPageEmployees.Take(5))
                        {
                            if (!_isLoadingPage) // Don't interfere with active loading
                            {
                                await GetCompleteAttendanceDataForEmployeeAsync(empId);
                                count++;
                            }
                        }
                        
                        Program.LogMessage($"Preloaded statistics for {count} employees");
                    }
                    catch (Exception ex)
                    {
                        Program.LogMessage($"Error preloading statistics: {ex.Message}");
                    }
                });
                
                // Force refresh pagination controls
                ((RelayCommand)NextEmployeePageCommand).NotifyCanExecuteChanged();
                ((RelayCommand)PreviousEmployeePageCommand).NotifyCanExecuteChanged();
                ((RelayCommand)NextDatePageCommand).NotifyCanExecuteChanged();
                ((RelayCommand)PreviousDatePageCommand).NotifyCanExecuteChanged();
                
                OnPropertyChanged(nameof(GetAttendanceStatus));
                OnPropertyChanged(nameof(CurrentPageEmployees));
                OnPropertyChanged(nameof(CurrentPageDates));
                
                ProgressStatus = "Report generation complete";
            }
            catch (OperationCanceledException)
            {
                Program.LogMessage("OverviewReport: Operation was cancelled");
                ProgressStatus = "Report generation cancelled";
            }
            catch (Exception ex)
            {
                Program.LogMessage($"OverviewReport: ERROR - {ex.Message}\n{ex.StackTrace}");
                ProgressStatus = $"Error: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
                Program.LogMessage("OverviewReport: Report generation completed");
                
                // Make sure pagination state is properly synced
                await Task.Delay(100); // Small delay to ensure UI updates
                SyncPaginationState();
                
                // Log final pagination state
                Program.LogMessage($"OverviewReport: Final pagination state - Employee pages: {TotalEmployeePages}, Date pages: {TotalDatePages}");
            }
        }
        
        // Dictionary for caching attendance status lookups by employee and date
        private readonly Dictionary<(int EmployeeId, DateTime Date), AttendanceReportItem?> _attendanceStatusCache = new();

        public AttendanceReportItem? GetAttendanceStatus(int employeeId, DateTime date)
        {
            try
            {
                // If still loading, don't cache or retry attempts
                if (_isLoadingPage)
                {
                    return null;
                }
                
                // Check cache first for performance
                var cacheKey = (employeeId, date.Date);
                if (_attendanceStatusCache.TryGetValue(cacheKey, out var cachedResult))
                {
                    return cachedResult;
                }

                // Cache miss, look up in employee data
                if (_employeeAttendanceData.TryGetValue(employeeId, out var employeeData))
                {
                    var result = employeeData.FirstOrDefault(a => a.Date.Date == date.Date);
                    
                    // If we found data, log and return it
                    if (result != null)
                    {
                        // Log only occasionally to reduce log size
                        if (date.Day == 1 || date.Day == 15)
                        {
                            string status = result.Status ?? "null";
                            string hasCheckIn = result.CheckInTime == null ? "No" : "Yes";
                            Program.LogMessage($"GetAttendanceStatus: Employee {employeeId}, Date {date:yyyy-MM-dd}, Found: True, Status: {status}, HasCheckIn: {hasCheckIn}");
                        }
                        
                        // Store in cache and return
                        _attendanceStatusCache[cacheKey] = result;
                        return result;
                    }
                    
                    // Data for employee exists but not for this specific date
                    // If this is a current page date, trigger recovery
                    if (IsDateInCurrentPage(date) && IsEmployeeInCurrentPage(employeeId))
                    {
                        Program.LogMessage($"GetAttendanceStatus: Missing date data for employee {employeeId} on {date:yyyy-MM-dd} - triggering recovery");
                        TryRecoverMissingData(employeeId, date);
                    }
                }
                else
                {
                    // We don't have any data for this employee
                    if (IsEmployeeInCurrentPage(employeeId))
                    {
                        // This employee should be loaded since they're on current page
                        Program.LogMessage($"GetAttendanceStatus: Missing ALL data for employee {employeeId} - triggering recovery");
                        TryRecoverMissingData(employeeId, date);
                    }
                    else if (date.Day == 1) // Log occasionally
                    {
                        Program.LogMessage($"GetAttendanceStatus: No data for employee {employeeId} (not on current page)");
                    }
                }
                
                // Store null result in cache to prevent repeated lookups
                _attendanceStatusCache[cacheKey] = null;
                return null;
            }
            catch (Exception ex)
            {
                Program.LogMessage($"GetAttendanceStatus: ERROR - {ex.Message} for employee {employeeId}, date {date:yyyy-MM-dd}");
                return null;
            }
        }
        
        // Clear attendance status cache when new data is loaded
        private void ClearAttendanceCache()
        {
            _attendanceStatusCache.Clear();
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
            if (_isUpdatingCheckStates) return;
            
            Program.LogMessage($"OnDepartmentCheckedChanged: {department.Name}, IsChecked={department.IsChecked}");
            
            // Prevent recursive calls
            _isUpdatingCheckStates = true;
            
            try
            {
                bool isChecked = department.IsChecked;
                
                // When a department is checked/unchecked, apply to ALL its employees
                if (department.Employees != null)
                {
                    foreach (var employee in department.Employees)
                    {
                        employee.IsChecked = isChecked;
                    }
                }
                
                // Manage expanded state based on checked state
                if (isChecked)
                {
                    department.IsExpanded = true;
                }
                
                // Update the selected employees collection
                SynchronizeSelections();
            }
            finally
            {
                _isUpdatingCheckStates = false;
            }
        }
        
        // Handle employee checkbox changes - modified to accept specific employee
        public void OnEmployeeCheckedChanged(DepartmentTreeNode department, EmployeeTreeNode employee)
        {
            if (_isUpdatingCheckStates) return;
            
            Program.LogMessage($"OnEmployeeCheckedChanged: Employee {employee.Name} in Department {department.Name}, IsChecked={employee.IsChecked}");
            
            // Prevent recursive calls
            _isUpdatingCheckStates = true;
            
            try
            {
                // Count checked employees in this department
                int checkedCount = department.Employees.Count(e => e.IsChecked);
                
                if (checkedCount == department.Employees.Count)
                {
                    // All employees selected -> check department
                    department.IsChecked = true;
                }
                else if (checkedCount == 0)
                {
                    // None selected -> uncheck department
                    department.IsChecked = false;
                }
                // For partial selection, don't change department checkbox state
                
                // Update the selected employees collection
                SynchronizeSelections();
            }
            finally
            {
                _isUpdatingCheckStates = false;
            }
        }
        
        /// <summary>
        /// Synchronizes the selection state between the tree view and the selected employees collection
        /// </summary>
        private void SynchronizeSelections()
        {
            SelectedEmployeeIds.Clear();
            
            // Collect all checked employees
            foreach (var dept in DepartmentTree)
            {
                foreach (var emp in dept.Employees)
                {
                    if (emp.IsChecked)
                    {
                        SelectedEmployeeIds.Add(emp.Id);
                    }
                }
            }
            
            Program.LogMessage($"SynchronizeSelections: {SelectedEmployeeIds.Count} employees selected");
        }
        
        // Synchronize pagination state and update UI
        private void SyncPaginationState()
        {
            // Ensure current page is valid
            if (CurrentEmployeePage >= TotalEmployeePages && TotalEmployeePages > 0)
            {
                CurrentEmployeePage = TotalEmployeePages - 1;
            }
            
            if (CurrentDatePage >= TotalDatePages && TotalDatePages > 0)
            {
                CurrentDatePage = TotalDatePages - 1;
            }
            
            // Update UI
            OnPropertyChanged(nameof(DatePageInfo));
            OnPropertyChanged(nameof(EmployeePageInfo));
            OnPropertyChanged(nameof(CurrentPageEmployees));
            OnPropertyChanged(nameof(CurrentPageDates));
            
            // Force refresh pagination controls
            ((RelayCommand)NextEmployeePageCommand).NotifyCanExecuteChanged();
            ((RelayCommand)PreviousEmployeePageCommand).NotifyCanExecuteChanged();
            ((RelayCommand)NextDatePageCommand).NotifyCanExecuteChanged();
            ((RelayCommand)PreviousDatePageCommand).NotifyCanExecuteChanged();
            
            // Ensure data for current page is loaded (this fixes pagination issues)
            if (!_isLoadingPage && _employeeAttendanceData.Count > 0)
            {
                Program.LogMessage($"SyncPaginationState: Ensuring data for page {CurrentDatePage + 1} is loaded for all current page employees");
                
                // Force wait for data to load using async/await to ensure data is ready when UI updates
                Task.Run(async () => 
                {
                    try 
                    {
                        // Using the more thorough loading method to ensure all employees have their data
                        await LoadDatePageDataForAllEmployeesAsync(CurrentDatePage);
                        
                        // Clear cache to force refresh with new data
                        ClearAttendanceCache();
                        
                        // Notify UI of changes
                        OnPropertyChanged(nameof(GetAttendanceStatus));
                    }
                    catch (Exception ex)
                    {
                        Program.LogMessage($"Error in SyncPaginationState: {ex.Message}");
                    }
                });
            }
        }
        
        // Initialize department tree check state from selected employees
        public void InitializeDepartmentTreeCheckState()
        {
            Program.LogMessage("InitializeDepartmentTreeCheckState: Starting");
            
            _isUpdatingCheckStates = true;
            
            try
            {
                // First uncheck everything
                foreach (var dept in DepartmentTree)
                {
                    dept.IsChecked = false;
                    foreach (var emp in dept.Employees)
                    {
                        emp.IsChecked = false;
                    }
                }
                
                // If we have selected employees, check them and their departments
                if (SelectedEmployeeIds.Count > 0)
                {
                    foreach (var dept in DepartmentTree)
                    {
                        bool anyEmployeeChecked = false;
                        
                        foreach (var emp in dept.Employees)
                        {
                            if (SelectedEmployeeIds.Contains(emp.Id))
                            {
                                emp.IsChecked = true;
                                anyEmployeeChecked = true;
                                Program.LogMessage($"  Checking employee {emp.Name} (ID: {emp.Id})");
                            }
                        }
                        
                        // If any employee is checked, check the department too
                        if (anyEmployeeChecked)
                        {
                            dept.IsChecked = true;
                            dept.IsExpanded = true; // Expand the department to show checked employees
                            Program.LogMessage($"  Checking department {dept.Name} (ID: {dept.Id}) and expanding it");
                        }
                    }
                }
            }
            finally
            {
                _isUpdatingCheckStates = false;
            }
            
            Program.LogMessage($"InitializeDepartmentTreeCheckState: {SelectedEmployeeIds.Count} employees selected");
        }
        
        public void SelectAllEmployees()
        {
            Program.LogMessage("SelectAllEmployees: Starting");
            
            _isUpdatingCheckStates = true;
            
            try
            {
                // Check all departments and all employees
                foreach (var dept in DepartmentTree)
                {
                    dept.IsChecked = true;
                    foreach (var emp in dept.Employees)
                    {
                        emp.IsChecked = true;
                    }
                }
                
                // Update the selected employees collection
                SynchronizeSelections();
            }
            finally
            {
                _isUpdatingCheckStates = false;
            }
            
            Program.LogMessage("SelectAllEmployees: Completed");
        }

        // Uncheck all departments and employees
        public void ClearAllEmployees()
        {
            Program.LogMessage("ClearAllEmployees: Starting");
            _isUpdatingCheckStates = true;
            try
            {
                foreach (var dept in DepartmentTree)
                {
                    dept.IsChecked = false;
                    foreach (var emp in dept.Employees)
                    {
                        emp.IsChecked = false;
                    }
                }

                SynchronizeSelections();

                // Also clear any previously generated report data and pagination
                _employeeAttendanceData.Clear();
                DateRange.Clear();
                TotalEmployeePages = 0;
                TotalDatePages = 0;
                CurrentEmployeePage = 0;
                CurrentDatePage = 0;
                OnPropertyChanged(nameof(EmployeePageInfo));
                OnPropertyChanged(nameof(DatePageInfo));
                OnPropertyChanged(nameof(CurrentPageEmployees));
                OnPropertyChanged(nameof(CurrentPageDates));
            }
            finally
            {
                _isUpdatingCheckStates = false;
            }
            Program.LogMessage("ClearAllEmployees: Completed");
        }

        // Load data for all employees on the current employee page
        private async Task LoadDatePageDataForAllEmployeesAsync(int datePageToLoad)
        {
            // Prevent multiple loads at the same time
            if (datePageToLoad < 0 || datePageToLoad >= TotalDatePages)
                return;
                
            try
            {
                // Get all current page employees
                var employeesToLoad = CurrentPageEmployees;
                if (employeesToLoad.Count == 0)
                    return;
                
                // Set loading indicators
                _isLoadingPage = true;
                
                Program.LogMessage($"OverviewReport: Loading date page {datePageToLoad + 1} for {employeesToLoad.Count} employees on page {CurrentEmployeePage + 1}");
                
                // Calculate date range for this page
                int startIdx = datePageToLoad * _daysPerPage;
                var dateRangeForPage = DateRange.Skip(startIdx).Take(_daysPerPage).ToList();
                if (dateRangeForPage.Count == 0)
                    return;
                    
                var startDateForPage = dateRangeForPage.First();
                var endDateForPage = dateRangeForPage.Last();
                
                // Track progress
                int totalEmployees = employeesToLoad.Count;
                ProgressPercentage = 0;
                
                // Load data for each employee using a more efficient bulk loading approach
                for (int i = 0; i < employeesToLoad.Count; i++)
                {
                    int employeeId = employeesToLoad[i];
                    
                    // Update progress
                    ProgressStatus = $"Loading data for employee {i + 1} of {totalEmployees}...";
                    ProgressPercentage = ((i + 1) * 100.0) / totalEmployees;
                    
                    // Get a direct reference to or create an entry for this employee
                    if (!_employeeAttendanceData.ContainsKey(employeeId))
                    {
                        _employeeAttendanceData[employeeId] = new List<AttendanceReportItem>();
                    }
                    
                    var employeeData = _employeeAttendanceData[employeeId];
                    
                    // Get set of dates we already have data for
                    var existingDates = employeeData
                        .Select(item => item.Date.Date)
                        .ToHashSet();
                    
                    // Check if we need to load data
                    bool needToLoadData = dateRangeForPage.Any(date => !existingDates.Contains(date.Date));
                    
                    if (needToLoadData)
                    {
                        try
                        {
                            // Load all data for the date range at once
                            var report = await _reportService.GenerateEmployeeAttendanceReportAsync(
                                employeeId, startDateForPage, endDateForPage);
                                
                            // Merge with existing data
                            foreach (var item in report)
                            {
                                if (!existingDates.Contains(item.Date.Date))
                                {
                                    employeeData.Add(item);
                                    existingDates.Add(item.Date.Date);
                                }
                            }
                            
                            // Log success
                            Program.LogMessage($"OverviewReport: Loaded {report.Count} attendance records for employee {employeeId}");
                        }
                        catch (Exception ex)
                        {
                            Program.LogMessage($"OverviewReport: ERROR loading data for employee {employeeId}: {ex.Message}");
                        }
                    }
                    else
                    {
                        Program.LogMessage($"OverviewReport: All dates already loaded for employee {employeeId}");
                    }
                }
                
                // Mark this page as loaded
                if (!_loadedDatePages.Contains(datePageToLoad))
                {
                    _loadedDatePages.Add(datePageToLoad);
                }
                
                // Clear cache to ensure fresh data
                ClearAttendanceCache();
                
                // Notify UI to refresh
                OnPropertyChanged(nameof(GetAttendanceStatus));
            }
            finally
            {
                _isLoadingPage = false;
            }
        }
        
        // Preload data for a specific employee page (for smoother navigation)
        private async Task PreloadEmployeePageDataAsync(int employeePage, int datePage)
        {
            // Skip if loading or invalid page
            if (_isLoadingPage || employeePage < 0 || employeePage >= TotalEmployeePages)
                return;
            
            try
            {
                // Calculate employees for the specified page
                var allEmployees = SelectedEmployeeIds.ToList();
                int startIndex = employeePage * _employeesPerPage;
                var employeesToLoad = allEmployees
                    .Skip(startIndex)
                    .Take(_employeesPerPage)
                    .ToList();
                
                if (employeesToLoad.Count == 0)
                    return;
                
                Program.LogMessage($"OverviewReport: Preloading date page {datePage + 1} for {employeesToLoad.Count} employees on page {employeePage + 1}");
                
                // Calculate date range for this page
                int startIdx = datePage * _daysPerPage;
                var dateRangeForPage = DateRange.Skip(startIdx).Take(_daysPerPage).ToList();
                if (dateRangeForPage.Count == 0)
                    return;
                    
                var startDateForPage = dateRangeForPage.First();
                var endDateForPage = dateRangeForPage.Last();
                
                // Background loading data for each employee without UI updates
                foreach (var employeeId in employeesToLoad)
                {
                    if (!_employeeAttendanceData.ContainsKey(employeeId))
                    {
                        _employeeAttendanceData[employeeId] = new List<AttendanceReportItem>();
                    }
                    
                    var employeeData = _employeeAttendanceData[employeeId];
                    var existingDates = employeeData.Select(item => item.Date.Date).ToHashSet();
                    
                    // Only load if we're missing dates
                    if (dateRangeForPage.Any(date => !existingDates.Contains(date.Date)))
                    {
                        try
                        {
                            var report = await _reportService.GenerateEmployeeAttendanceReportAsync(
                                employeeId, startDateForPage, endDateForPage);
                            
                            // Merge data
                            foreach (var item in report)
                            {
                                if (!existingDates.Contains(item.Date.Date))
                                {
                                    employeeData.Add(item);
                                }
                            }
                        }
                        catch (Exception ex) 
                        {
                            // Just log errors, don't interrupt the flow
                            Program.LogMessage($"PreloadEmployeePage: Error loading data for employee {employeeId}: {ex.Message}");
                        }
                    }
                }
                
                Program.LogMessage($"OverviewReport: Preloaded data for employee page {employeePage + 1}");
            }
            catch (Exception ex)
            {
                Program.LogMessage($"PreloadEmployeePage: General error: {ex.Message}");
            }
        }

        // More robust interface for UI to get attendance status as string
        public string GetAttendanceStatusAsString(int employeeId, DateTime day)
        {
            var status = GetAttendanceStatus(employeeId, day);
            
            // If no status or loading, return placeholder
            if (status == null || _isLoadingPage)
            {
                return "-";
            }
            
            // Adaptation based on AttendanceReportItem's actual properties
            if (status.IsNonWorkingDay)
            {
                return "W"; // Weekend or non-working day
            }
            else if (status.IsHoliday)
            {
                return "H"; // Holiday
            }
            else if (status.Status == "Absent" || status.CheckInTime == null)
            {
                return "A"; // Absent
            }
            else if (status.CheckInTime != null) // Present in some form
            {
                // Check for various status types
                if (status.IsEarlyArrival && !status.IsLate && !status.IsEarlyDeparture)
                {
                    return "EA"; // Early Arrival only
                }
                else if (status.IsLate && status.IsEarlyDeparture)
                {
                    return "L+E"; // Both Late and Early Departure
                }
                else if (status.IsLate)
                {
                    return "L"; // Late Arrival
                }
                else if (status.IsEarlyDeparture)
                {
                    return "E"; // Early Departure
                }
                else if (status.IsOvertime)
                {
                    return "O"; // Overtime
                }
                else
                {
                    return "P"; // Present (regular)
                }
            }
            
            return "-"; // Default case for undefined states
        }
        
        // Initialize auto-recovery system for missing data
        private readonly HashSet<string> _recoveryAttempted = new HashSet<string>();
        private readonly object _recoveryLock = new object();
        
        // Public interface to trigger data recovery when attendance data is missing
        public void TryRecoverMissingData(int employeeId, DateTime date)
        {
            try
            {
                // Create a unique key for this recovery attempt
                string recoveryKey = $"{employeeId}_{date.ToString("yyyy-MM-dd")}";
                
                // Only attempt recovery once per employee/date combination
                lock (_recoveryLock)
                {
                    if (_recoveryAttempted.Contains(recoveryKey))
                    {
                        return;
                    }
                    _recoveryAttempted.Add(recoveryKey);
                }
                
                // Log recovery attempt
                Program.LogMessage($"Auto-recovery: Attempting data recovery for employee {employeeId} on {date:yyyy-MM-dd}");
                
                // Find the date page containing this date
                int datePageIndex = GetDatePageForDate(date);
                if (datePageIndex < 0)
                {
                    Program.LogMessage($"Auto-recovery: Failed - Date {date:yyyy-MM-dd} not found in any page");
                    return;
                }
                
                // Calculate date range for this page
                int startIdx = datePageIndex * _daysPerPage;
                var dateRangeForPage = DateRange.Skip(startIdx).Take(_daysPerPage).ToList();
                if (dateRangeForPage.Count == 0)
                {
                    Program.LogMessage($"Auto-recovery: Failed - No dates in page {datePageIndex + 1}");
                    return;
                }
                
                // Trigger data load for the entire page
                var startDateForPage = dateRangeForPage.First();
                var endDateForPage = dateRangeForPage.Last();
                
                Task.Run(async () =>
                {
                    try
                    {
                        // Ensure we're not already loading data
                        if (!_isLoadingPage)
                        {
                            // Get just this employee's data
                            var report = await _reportService.GenerateEmployeeAttendanceReportAsync(
                                employeeId, startDateForPage, endDateForPage);
                                
                            // Initialize or get employee data collection
                            if (!_employeeAttendanceData.ContainsKey(employeeId))
                            {
                                _employeeAttendanceData[employeeId] = new List<AttendanceReportItem>();
                            }
                            
                            var employeeData = _employeeAttendanceData[employeeId];
                            var existingDates = employeeData.Select(item => item.Date.Date).ToHashSet();
                            
                            // Merge new data with existing data
                            int newItemCount = 0;
                            foreach (var item in report)
                            {
                                if (!existingDates.Contains(item.Date.Date))
                                {
                                    employeeData.Add(item);
                                    existingDates.Add(item.Date.Date);
                                    newItemCount++;
                                }
                            }
                            
                            // Clear cache entries for this employee to ensure fresh data
                            ClearAttendanceCache();
                            
                            // Notify UI to refresh
                            OnPropertyChanged(nameof(GetAttendanceStatus));
                            
                            Program.LogMessage($"Auto-recovery: Successfully loaded {newItemCount} new items for employee {employeeId}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Program.LogMessage($"Auto-recovery: Error - {ex.Message}");
                    }
                });
            }
            catch (Exception ex)
            {
                Program.LogMessage($"TryRecoverMissingData: Error - {ex.Message}");
            }
        }

        // Helper methods for checking dates, employees, and pages
        private bool IsDateInCurrentPage(DateTime date)
        {
            int startIdx = CurrentDatePage * _daysPerPage;
            var dateRangeForPage = DateRange.Skip(startIdx).Take(_daysPerPage).ToList();
            return dateRangeForPage.Any(d => d.Date == date.Date);
        }
        
        private bool IsEmployeeInCurrentPage(int employeeId)
        {
            return CurrentPageEmployees.Contains(employeeId);
        }
        
        private int GetDatePageForDate(DateTime date)
        {
            for (int i = 0; i < TotalDatePages; i++)
            {
                int startIdx = i * _daysPerPage;
                var dateRangeForPage = DateRange.Skip(startIdx).Take(_daysPerPage).ToList();
                if (dateRangeForPage.Any(d => d.Date == date.Date))
                {
                    return i;
                }
            }
            return -1; // Not found in any page
        }
        
        private int GetEmployeePageForEmployee(int employeeId)
        {
            var allEmployees = SelectedEmployeeIds.ToList();
            int employeeIndex = allEmployees.IndexOf(employeeId);
            
            if (employeeIndex >= 0)
            {
                return employeeIndex / _employeesPerPage;
            }
            
            return -1; // Not found
        }
        
        private void ClearAttendanceCacheForEmployee(int employeeId)
        {
            var keysToRemove = _attendanceStatusCache.Keys
                .Where(k => k.Item1 == employeeId)
                .ToList();
                
            foreach (var key in keysToRemove)
            {
                _attendanceStatusCache.Remove(key);
            }
        }
        
        private void ClearAttendanceCacheForDate(DateTime date)
        {
            var dateOnly = date.Date;
            var keysToRemove = _attendanceStatusCache.Keys
                .Where(k => k.Item2 == dateOnly)
                .ToList();
                
            foreach (var key in keysToRemove)
            {
                _attendanceStatusCache.Remove(key);
            }
        }

        // Get complete attendance data for an employee across all date pages
        public async Task<List<AttendanceReportItem>> GetCompleteAttendanceDataForEmployeeAsync(int employeeId)
        {
            var result = new List<AttendanceReportItem>();
            
            // If we already have data, return it
            if (_employeeAttendanceData.TryGetValue(employeeId, out var existingData) && 
                existingData.Count > 0 && 
                existingData.Count >= DateRange.Count * 0.9) // If we have at least 90% of dates
            {
                return existingData;
            }
            
            try
            {
                // We need to load all data for this employee for the entire date range
                var startDate = DateRange.FirstOrDefault();
                var endDate = DateRange.LastOrDefault();
                
                if (startDate == default || endDate == default)
                {
                    return result; // Empty date range
                }
                
                // Load all data at once from the report service
                Program.LogMessage($"Loading complete attendance data for employee {employeeId} from {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}");
                var report = await _reportService.GenerateEmployeeAttendanceReportAsync(employeeId, startDate, endDate);
                
                // Make sure we have a place to store this data
                if (!_employeeAttendanceData.ContainsKey(employeeId))
                {
                    _employeeAttendanceData[employeeId] = new List<AttendanceReportItem>();
                }
                
                // Get existing dates to avoid duplicates
                var existingDates = _employeeAttendanceData[employeeId]
                    .Select(item => item.Date.Date)
                    .ToHashSet();
                    
                // Add all new data to the cache
                foreach (var item in report)
                {
                    if (!existingDates.Contains(item.Date.Date))
                    {
                        _employeeAttendanceData[employeeId].Add(item);
                        existingDates.Add(item.Date.Date);
                    }
                }
                
                return _employeeAttendanceData[employeeId];
            }
            catch (Exception ex)
            {
                Program.LogMessage($"Error getting complete attendance data for employee {employeeId}: {ex.Message}");
                return result;
            }
        }
        
        // Get attendance statistics for an employee for the entire date range
        public async Task<Dictionary<string, double>> GetEmployeeStatisticsAsync(int employeeId)
        {
            var result = new Dictionary<string, double>
            {
                { "LateEarlyHours", 0 },
                { "OvertimeHours", 0 },
                { "PresentHours", 0 },
                { "ExpectedHours", 0 },
                { "AbsentDays", 0 }
            };
            
            try
            {
                // Get the complete data for this employee
                var employeeData = await GetCompleteAttendanceDataForEmployeeAsync(employeeId);
                
                // Calculate statistics
                double lateEarlyHours = 0;
                double overtimeHours = 0;
                double presentHours = 0;
                double expectedHours = 0;
                int absentDays = 0;
                
                // Process all attendance records
                foreach (var item in employeeData)
                {
                    // Skip holidays and non-working days for some calculations
                    if (item.IsHoliday || item.IsNonWorkingDay)
                        continue;
                    
                    // Count absent days
                    if (item.Status == "Absent" || !item.CheckInTime.HasValue)
                    {
                        absentDays++;
                        continue;
                    }
                    
                    // Calculate hours from actual records
                    if (item.WorkDuration.HasValue)
                    {
                        presentHours += item.WorkDuration.Value.TotalHours;
                    }
                    
                    // Add expected hours
                    expectedHours += item.ExpectedWorkHours;
                    
                    // Add late/early hours
                    if (item.LateMinutes.HasValue)
                    {
                        lateEarlyHours += item.LateMinutes.Value.TotalHours;
                    }
                    
                    if (item.EarlyDepartureMinutes.HasValue)
                    {
                        lateEarlyHours += item.EarlyDepartureMinutes.Value.TotalHours;
                    }
                    
                    // Add overtime hours
                    if (item.OvertimeMinutes.HasValue)
                    {
                        overtimeHours += item.OvertimeMinutes.Value.TotalHours;
                    }
                }
                
                // Update the result
                result["LateEarlyHours"] = Math.Round(lateEarlyHours, 1);
                result["OvertimeHours"] = Math.Round(overtimeHours, 1);
                result["PresentHours"] = Math.Round(presentHours, 1);
                result["ExpectedHours"] = Math.Round(expectedHours, 1);
                result["AbsentDays"] = absentDays;
            }
            catch (Exception ex)
            {
                Program.LogMessage($"Error calculating employee statistics: {ex.Message}");
            }
            
            return result;
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