using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;
using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using System;
using System.Linq;
using System.Collections.Generic;

namespace AttandenceDesktop.ViewModels;

public partial class DepartmentViewModel : ViewModelBase, IDisposable
{
    private readonly DepartmentService _departmentService;
    private readonly DataRefreshService _dataRefreshService;
    private readonly DeviceService _deviceService;
    private readonly ZkDataExtractionService _zkDataService;

    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;

    internal DepartmentService DepartmentService => _departmentService;

    public DepartmentViewModel(DepartmentService departmentService, DataRefreshService dataRefreshService, DeviceService deviceService)
    {
        _departmentService = departmentService;
        _dataRefreshService = dataRefreshService;
        _deviceService = deviceService;
        _zkDataService = new ZkDataExtractionService();
        
        Departments = new ObservableCollection<Department>();
        LoadDepartmentsCommand = new AsyncRelayCommand(LoadDepartmentsAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand<Department>(EditAsync);
        DeleteCommand = new AsyncRelayCommand<Department>(DeleteAsync);
        SyncDepartmentsCommand = new AsyncRelayCommand(SyncDepartmentsFromDeviceAsync);

        // Subscribe to data change events
        _dataRefreshService.DepartmentsChanged += OnDepartmentsChanged;

        // Initial load
        _ = LoadDepartmentsAsync();
        
        // Log for debugging
        Program.LogMessage("DepartmentViewModel initialized");
    }

    public void Dispose()
    {
        // Unsubscribe from events
        _dataRefreshService.DepartmentsChanged -= OnDepartmentsChanged;
    }

    private void OnDepartmentsChanged(object? sender, EventArgs e)
    {
        _ = LoadDepartmentsAsync();
    }

    // Parameterless constructor for design-time support
    public DepartmentViewModel() : this(
        new DepartmentService(() => new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase("DesignTimeDB").Options)),
        null!,
        null!)
    {
    }

    [ObservableProperty]
    private ObservableCollection<Department> _departments;

    [ObservableProperty]
    private Department? _selectedDepartment;

    [ObservableProperty]
    private string _newDepartmentName = string.Empty;

    public IAsyncRelayCommand LoadDepartmentsCommand { get; }
    public IAsyncRelayCommand AddCommand { get; }
    public IAsyncRelayCommand<Department> EditCommand { get; }
    public IAsyncRelayCommand<Department> DeleteCommand { get; }
    public IAsyncRelayCommand SyncDepartmentsCommand { get; }

    private async Task LoadDepartmentsAsync()
    {
        // Debug logging
        System.Diagnostics.Debug.WriteLine("Loading departments started");
        Program.LogMessage("LoadDepartmentsAsync started");
        
        try
        {
            var list = await _departmentService.GetAllAsync();
            System.Diagnostics.Debug.WriteLine($"Loaded {list?.Count ?? 0} departments");
            Program.LogMessage($"Loaded {list?.Count ?? 0} departments from database");
            
            // Clear and add items one by one to ensure proper UI updates
            Departments.Clear();
            if (list != null)
            {
                foreach (var dept in list)
                {
                    Departments.Add(dept);
                }
            }
            
            // Log the final count
            Program.LogMessage($"Department collection now contains {Departments.Count} items");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading departments: {ex.Message}");
            Program.LogMessage($"Error loading departments: {ex.Message}");
            Program.LogMessage($"Stack trace: {ex.StackTrace}");
            
            // Add departments directly in case DB connection fails
            Departments.Clear();
            var fallbackDepts = new List<Department>
            {
                new Department { Id = 1, Name = "HR" },
                new Department { Id = 2, Name = "IT" },
                new Department { Id = 3, Name = "Finance" },
                new Department { Id = 4, Name = "Operations" },
                new Department { Id = 5, Name = "Marketing" }
            };
            
            foreach (var dept in fallbackDepts)
            {
                Departments.Add(dept);
            }
            
            Program.LogMessage($"Added {fallbackDepts.Count} fallback departments");
        }
    }

    private async Task AddAsync()
    {
        if (string.IsNullOrWhiteSpace(NewDepartmentName)) return;
        var department = new Department { Name = NewDepartmentName };
        await _departmentService.CreateAsync(department);
        NewDepartmentName = string.Empty;
        await LoadDepartmentsAsync();
    }

    private async Task EditAsync(Department? department)
    {
        if (department == null) return;
        await _departmentService.UpdateAsync(department);
        await LoadDepartmentsAsync();
    }

    private async Task DeleteAsync(Department? department)
    {
        if (department == null) return;
        await _departmentService.DeleteAsync(department.Id);
        await LoadDepartmentsAsync();
    }
    
    /// <summary>
    /// Synchronizes departments from ZKTeco devices to the application database
    /// </summary>
    private async Task SyncDepartmentsFromDeviceAsync()
    {
        try
        {
            IsBusy = true;
            StatusMessage = "Fetching devices...";
            
            // Get all available devices
            var devices = await _deviceService.GetAllAsync();
            if (devices == null || !devices.Any())
            {
                StatusMessage = "No devices found. Please add a device first.";
                IsBusy = false;
                return;
            }
            
            StatusMessage = $"Found {devices.Count} device(s). Syncing departments...";
            
            // Create device sync service
            var deviceSyncService = new DeviceSyncService(
                () => ApplicationDbContext.Create(),
                _dataRefreshService);
            
            int totalDepartments = 0;
            int newDepartments = 0;
            var departmentsBefore = await _departmentService.GetAllAsync();
            
            // Process each device
            foreach (var device in devices)
            {
                StatusMessage = $"Extracting departments from {device.Name}...";
                
                // Extract departments from this device
                var extractedDepartments = await deviceSyncService.ExtractDepartmentsFromDevice(device);
                
                if (extractedDepartments.Any())
                {
                    totalDepartments += extractedDepartments.Count;
                    // Count new departments (those that didn't exist before)
                    newDepartments += extractedDepartments.Count(d => !departmentsBefore.Any(existing => existing.Id == d.Id));
                    StatusMessage = $"Extracted {extractedDepartments.Count} departments from {device.Name}";
                }
                else
                {
                    StatusMessage = $"No departments found in device {device.Name}";
                }
            }
            
            // Reload departments
            await LoadDepartmentsAsync();
            
            // Show final status
            if (totalDepartments > 0)
            {
                StatusMessage = $"Sync completed. Found {totalDepartments} departments ({newDepartments} new).";
            }
            else
            {
                StatusMessage = "No departments found in any device.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            System.Diagnostics.Debug.WriteLine($"Error syncing departments: {ex.Message}");
            Program.LogMessage($"Error syncing departments: {ex.Message}");
            Program.LogMessage($"Stack trace: {ex.StackTrace}");
        }
        finally
        {
            IsBusy = false;
        }
    }
} 