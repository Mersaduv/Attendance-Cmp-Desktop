using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AttandenceDesktop.ViewModels
{
    public class EmployeeViewModel : ViewModelBase, IDisposable
    {
        private readonly EmployeeService _employeeService;
        private readonly DepartmentService _departmentService;
        private readonly DeviceService _deviceService;
        private readonly DataRefreshService _dataRefreshService;
        
        private ObservableCollection<Employee> _employees;
        private List<Department> _departments;
        private Employee _selectedEmployee;
        private bool _isLoading = false;
        private string _errorMessage;
        
        public ObservableCollection<Employee> Employees
        {
            get => _employees;
            set => SetProperty(ref _employees, value);
        }
        
        public List<Department> Departments
        {
            get => _departments;
            set => SetProperty(ref _departments, value);
        }
        
        public Employee SelectedEmployee
        {
            get => _selectedEmployee;
            set => SetProperty(ref _selectedEmployee, value);
        }
        
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }
        
        public ICommand LoadEmployeesCommand { get; }
        public IRelayCommand AddFingerprintCommand { get; }
        
        public EmployeeViewModel(EmployeeService employeeService, DepartmentService departmentService, DeviceService deviceService, DataRefreshService dataRefreshService)
        {
            _employeeService = employeeService;
            _departmentService = departmentService;
            _deviceService = deviceService;
            _dataRefreshService = dataRefreshService;
            
            Employees = new ObservableCollection<Employee>();
            LoadEmployeesCommand = new AsyncRelayCommand(LoadEmployeesAsync);
            AddFingerprintCommand = new AsyncRelayCommand<Employee>(AddFingerprintAsync);
            
            // Subscribe to data change events
            _dataRefreshService.EmployeesChanged += OnEmployeesChanged;
            _dataRefreshService.DepartmentsChanged += OnDepartmentsChanged;
            
            // Load data automatically when view model is created
            _ = InitializeAsync();
        }
        
        // Parameterless constructor for design-time support
        public EmployeeViewModel()
        {
            _employeeService = null!;
            _departmentService = null!;
            _deviceService = null!;
            _dataRefreshService = null!;
            
            _employees = new ObservableCollection<Employee>();
            _departments = new List<Department>();
            _selectedEmployee = null!;
            _errorMessage = string.Empty;
            
            LoadEmployeesCommand = new AsyncRelayCommand(async () => { });
            AddFingerprintCommand = new RelayCommand(() => { });
            
            // Add some design-time data
            Employees.Add(new Employee { 
                Id = 1, 
                FirstName = "John", 
                LastName = "Doe", 
                Email = "john@example.com",
                Position = "Developer"
            });
            Employees.Add(new Employee { 
                Id = 2, 
                FirstName = "Jane", 
                LastName = "Smith", 
                Email = "jane@example.com",
                Position = "Manager"
            });
        }
        
        public void Dispose()
        {
            // Unsubscribe from events
            _dataRefreshService.EmployeesChanged -= OnEmployeesChanged;
            _dataRefreshService.DepartmentsChanged -= OnDepartmentsChanged;
        }
        
        private void OnEmployeesChanged(object? sender, EventArgs e)
        {
            _ = LoadEmployeesAsync();
        }
        
        private void OnDepartmentsChanged(object? sender, EventArgs e)
        {
            _ = LoadDepartmentsAsync();
        }
        
        private async Task InitializeAsync()
        {
            IsLoading = true;
            try
            {
                await LoadDepartmentsAsync();
                await LoadEmployeesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading data: {ex.Message}");
                ErrorMessage = $"Failed to load data: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        public async Task LoadEmployeesAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;
                
                var employeesList = await _employeeService.GetAllAsync();
                Employees = new ObservableCollection<Employee>(employeesList ?? new List<Employee>());
                
                Debug.WriteLine($"Loaded {Employees.Count} employees");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading employees: {ex.Message}");
                ErrorMessage = $"Failed to load employees: {ex.Message}";
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private async Task LoadDepartmentsAsync()
        {
            try
            {
                var departmentsList = await _departmentService.GetAllAsync();
                Departments = departmentsList ?? new List<Department>();
                
                Debug.WriteLine($"Loaded {Departments.Count} departments");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading departments: {ex.Message}");
                ErrorMessage = $"Failed to load departments: {ex.Message}";
            }
        }
        
        public async Task CreateEmployeeAsync(Employee employee)
        {
            try
            {
                await _employeeService.CreateAsync(employee);
                await LoadEmployeesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating employee: {ex.Message}");
                ErrorMessage = $"Failed to create employee: {ex.Message}";
                throw;
            }
        }
        
        public async Task UpdateEmployeeAsync(Employee employee)
        {
            try
            {
                await _employeeService.UpdateAsync(employee);
                await LoadEmployeesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating employee: {ex.Message}");
                ErrorMessage = $"Failed to update employee: {ex.Message}";
                throw;
            }
        }
        
        public async Task DeleteEmployeeAsync(int id)
        {
            try
            {
                await _employeeService.DeleteAsync(id);
                await LoadEmployeesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting employee: {ex.Message}");
                ErrorMessage = $"Failed to delete employee: {ex.Message}";
                throw;
            }
        }
        
        private async Task AddFingerprintAsync(Employee emp)
        {
            if(emp == null) return;
            var devices = _deviceService == null ? new List<Device>() : await _deviceService.GetAllAsync();
            var dlgVm = new FingerprintDialogViewModel(emp, devices);
            var dlg = new Views.FingerprintDialog{ DataContext = dlgVm };
            var main = (App.Current.ApplicationLifetime as Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)?.MainWindow;
            var ok = await dlg.ShowDialog<bool>(main);
            if(ok)
            {
                await _employeeService.UpdateFingerprintAsync(emp.Id, dlgVm.ZkUserId!, dlgVm.FingerprintTemplate1);
            }
        }
    }
} 