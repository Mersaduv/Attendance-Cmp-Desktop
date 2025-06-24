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
                    
                int startIndex = CurrentEmployeePage * _employeesPerPage;
                return allEmployees
                    .Skip(startIndex)
                    .Take(_employeesPerPage)
                    .ToList();
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
        private void NextEmployeePage()
        {
            VerifyPaginationState();
            if (CurrentEmployeePage < TotalEmployeePages - 1)
            {
                CurrentEmployeePage++;
            }
        }
        
        private void PreviousEmployeePage()
        {
            VerifyPaginationState();
            if (CurrentEmployeePage > 0)
            {
                CurrentEmployeePage--;
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
                
                // Calculate which employees are on the current page
                var employeesToLoad = CurrentPageEmployees;
                
                // Load data for each employee for this date page
                int counter = 0;
                foreach (var employeeId in employeesToLoad)
                {
                    var existingData = _employeeAttendanceData.ContainsKey(employeeId) ? 
                        _employeeAttendanceData[employeeId] : new List<AttendanceReportItem>();
                    
                    // Only fetch dates that are missing for this page
                    var missingDates = dateRangeForPage
                        .Where(date => !existingData.Any(item => item.Date.Date == date.Date))
                        .ToList();
                    
                    if (missingDates.Count > 0)
                    {
                        Program.LogMessage($"OverviewReport: Fetching {missingDates.Count} missing dates for employee {employeeId}");
                        
                        var employeeReport = await _reportService.GenerateEmployeeAttendanceReportAsync(
                            employeeId, startDateForPage, endDateForPage);
                            
                        // Merge new data with existing data
                        if (!_employeeAttendanceData.ContainsKey(employeeId))
                        {
                            _employeeAttendanceData[employeeId] = employeeReport;
                        }
                        else
                        {
                            var existingDates = _employeeAttendanceData[employeeId].Select(item => item.Date.Date).ToHashSet();
                            foreach (var item in employeeReport)
                            {
                                if (!existingDates.Contains(item.Date.Date))
                                {
                                    _employeeAttendanceData[employeeId].Add(item);
                                }
                            }
                        }
                    }
                    
                    counter++;
                    ProgressPercentage = (counter * 100.0) / employeesToLoad.Count;
                }
                
                // Mark this page as loaded
                _loadedDatePages.Add(datePageToLoad);
                Program.LogMessage($"OverviewReport: Loaded data for date page {datePageToLoad + 1}");
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
                    
                    DepartmentTree.Add(new DepartmentTreeNode
                    {
                        Id = department.Id,
                        Name = department.Name,
                        Employees = employeeNodes,
                        IsExpanded = anyEmployeeSelected, // Auto-expand if any employee is selected
                        IsChecked = anyEmployeeSelected   // Auto-check if any employee is selected
                    });
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
                
                // Synchronize pagination state and update UI
                SyncPaginationState();
                
                // Only load data for the first page initially
                await LoadDatePageDataAsync(0);
                
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
                // Check cache first
                var cacheKey = (employeeId, date.Date);
                if (_attendanceStatusCache.TryGetValue(cacheKey, out var cachedResult))
                {
                    return cachedResult;
                }

                // Cache miss, look up in employee data
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
                    
                    // Store in cache
                    _attendanceStatusCache[cacheKey] = result;
                    return result;
                }
                
                // Log when employee data is missing
                if (date.Day == 1)
                {
                    Program.LogMessage($"GetAttendanceStatus: WARNING - No data found for employee {employeeId}");
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
            Program.LogMessage($"OnDepartmentCheckedChanged: {department.Name}, IsChecked={department.IsChecked}");
            
            // Make a backup of the current state to ensure we detect changes
            bool isChecked = department.IsChecked;
            
            // When a department is checked/unchecked, forcefully propagate to ALL its employees
            if (department.Employees != null)
            {
                // First count how many employees we will update
                int employeeCount = department.Employees.Count;
                Program.LogMessage($"  Setting {employeeCount} employees to {isChecked} state");
                
                // Now apply the state to each employee
                foreach (var employee in department.Employees)
                {
                    // Force employee checked state to match the department
                    employee.IsChecked = isChecked;
                    Program.LogMessage($"  Forcing employee {employee.Name} checked state to {isChecked}");
                }
            }
            
            // Manage expanded state based on checked state
            if (isChecked)
            {
                // When checked, expand the department to show the employees
                department.IsExpanded = true;
                Program.LogMessage($"  Expanding department {department.Name}");
            }
            
            // Update the selected employees collection
            SynchronizeSelections();
            Program.LogMessage($"  Department {department.Name} checked state synchronized with {department.Employees?.Count ?? 0} employees");
        }
        
        /// <summary>
        /// Synchronizes the selection state between the tree view and the selected employees collection
        /// </summary>
        private void SynchronizeSelections()
        {
            SelectedEmployeeIds.Clear();
            
            // Track departments with checked employees
            HashSet<int> departmentsWithCheckedEmployees = new HashSet<int>();
            
            // First pass - collect all checked employees
            foreach (var dept in DepartmentTree)
            {
                foreach (var emp in dept.Employees)
                {
                    if (emp.IsChecked)
                    {
                        SelectedEmployeeIds.Add(emp.Id);
                        departmentsWithCheckedEmployees.Add(dept.Id);
                    }
                }
            }
            
            // Second pass - verify department checkbox states
            foreach (var dept in DepartmentTree)
            {
                bool hasCheckedEmployees = departmentsWithCheckedEmployees.Contains(dept.Id);
                
                // If department checkbox state doesn't match its employees, update it
                if (dept.IsChecked != hasCheckedEmployees)
                {
                    Program.LogMessage($"  Fixing department {dept.Name} checkbox state: {dept.IsChecked} -> {hasCheckedEmployees}");
                    dept.IsChecked = hasCheckedEmployees;
                }
            }
            
            Program.LogMessage($"SynchronizeSelections: {SelectedEmployeeIds.Count} employees selected");
        }
        
        // Handle employee checkbox changes
        public void OnEmployeeCheckedChanged(DepartmentTreeNode department)
        {
            Program.LogMessage($"OnEmployeeCheckedChanged: Department {department.Name}");
            
            // Count checked employees
            int checkedCount = department.Employees.Count(e => e.IsChecked);
            Program.LogMessage($"  {checkedCount} of {department.Employees.Count} employees checked");
            
            // If any employee is checked, also check the department
            if (checkedCount > 0)
            {
                department.IsChecked = true;
                Program.LogMessage($"  Setting department {department.Name} checked state to true");
            }
            else
            {
                // If no employees are checked, uncheck the department
                department.IsChecked = false;
                Program.LogMessage($"  Setting department {department.Name} checked state to false");
            }
            
            // Update the selected employees collection
            SynchronizeSelections();
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
        }
        
        // Initialize department tree check state from selected employees
        public void InitializeDepartmentTreeCheckState()
        {
            Program.LogMessage("InitializeDepartmentTreeCheckState: Starting");
            
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
            
            Program.LogMessage($"InitializeDepartmentTreeCheckState: {SelectedEmployeeIds.Count} employees selected");
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