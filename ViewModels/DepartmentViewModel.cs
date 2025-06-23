using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;
using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using System;

namespace AttandenceDesktop.ViewModels;

public partial class DepartmentViewModel : ViewModelBase, IDisposable
{
    private readonly DepartmentService _departmentService;
    private readonly DataRefreshService _dataRefreshService;

    internal DepartmentService DepartmentService => _departmentService;

    public DepartmentViewModel(DepartmentService departmentService, DataRefreshService dataRefreshService)
    {
        _departmentService = departmentService;
        _dataRefreshService = dataRefreshService;
        Departments = new ObservableCollection<Department>();
        LoadDepartmentsCommand = new AsyncRelayCommand(LoadDepartmentsAsync);
        AddCommand = new AsyncRelayCommand(AddAsync);
        EditCommand = new AsyncRelayCommand<Department>(EditAsync);
        DeleteCommand = new AsyncRelayCommand<Department>(DeleteAsync);

        // Subscribe to data change events
        _dataRefreshService.DepartmentsChanged += OnDepartmentsChanged;

        // Initial load
        _ = LoadDepartmentsAsync();
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

    private async Task LoadDepartmentsAsync()
    {
        // Debug logging
        System.Diagnostics.Debug.WriteLine("Loading departments started");
        try
        {
            var list = await _departmentService.GetAllAsync();
            System.Diagnostics.Debug.WriteLine($"Loaded {list?.Count ?? 0} departments");
            Departments = new ObservableCollection<Department>(list);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Error loading departments: {ex.Message}");
            // Add departments directly in case DB connection fails
            Departments = new ObservableCollection<Department>
            {
                new Department { Id = 1, Name = "HR" },
                new Department { Id = 2, Name = "IT" },
                new Department { Id = 3, Name = "Finance" },
                new Department { Id = 4, Name = "Operations" },
                new Department { Id = 5, Name = "Marketing" }
            };
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
} 