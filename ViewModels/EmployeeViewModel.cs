using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;
using AttandenceDesktop.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Layout;

namespace AttandenceDesktop.ViewModels
{
    public partial class EmployeeViewModel : ViewModelBase, IDisposable
    {
        private readonly EmployeeService _employeeService;
        private readonly DepartmentService _departmentService;
        private readonly DeviceService _deviceService;
        private readonly DataRefreshService _dataRefreshService;
        private readonly DeviceSyncService _deviceSyncService;
        
        [ObservableProperty] private ObservableCollection<Employee> _employees;
        [ObservableProperty] private List<Department> _departments;
        [ObservableProperty] private Employee? _selectedEmployee;
        [ObservableProperty] private bool _isLoading;
        [ObservableProperty] private string _errorMessage;
        [ObservableProperty] private bool _isSyncing;
        [ObservableProperty] private string _syncStatus;
        [ObservableProperty] private ObservableCollection<Device> _devices;
        [ObservableProperty] private Device? _selectedDevice;
        
        public ICommand LoadEmployeesCommand { get; }
        public IRelayCommand AddFingerprintCommand { get; }
        public IRelayCommand SyncFromDeviceCommand { get; }
        public IRelayCommand AddCommand { get; }
        public IRelayCommand EditCommand { get; }
        public IRelayCommand DeleteCommand { get; }
        public IRelayCommand DeleteAllCommand { get; }
        
        public EmployeeViewModel(
            EmployeeService employeeService, 
            DepartmentService departmentService, 
            DeviceService deviceService, 
            DataRefreshService dataRefreshService,
            DeviceSyncService deviceSyncService)
        {
            _employeeService = employeeService;
            _departmentService = departmentService;
            _deviceService = deviceService;
            _dataRefreshService = dataRefreshService;
            _deviceSyncService = deviceSyncService;
            
            Employees = new ObservableCollection<Employee>();
            Departments = new List<Department>();
            Devices = new ObservableCollection<Device>();
            IsLoading = false;
            IsSyncing = false;
            SyncStatus = string.Empty;
            ErrorMessage = string.Empty;
            
            // Initialize commands
            LoadEmployeesCommand = new AsyncRelayCommand(LoadEmployeesAsync);
            AddFingerprintCommand = new AsyncRelayCommand<Employee>(AddFingerprintAsync);
            SyncFromDeviceCommand = new AsyncRelayCommand(SyncFromDeviceAsync);
            AddCommand = new AsyncRelayCommand(AddEmployeeAsync);
            EditCommand = new AsyncRelayCommand<Employee>(EditEmployeeAsync);
            DeleteCommand = new AsyncRelayCommand<Employee>(DeleteEmployeeAsync);
            DeleteAllCommand = new AsyncRelayCommand(DeleteAllEmployeesAsync);
            
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
            _deviceSyncService = null!;
            
            _employees = new ObservableCollection<Employee>();
            _departments = new List<Department>();
            _devices = new ObservableCollection<Device>();
            _selectedEmployee = null;
            _selectedDevice = null;
            _errorMessage = string.Empty;
            _syncStatus = string.Empty;
            
            LoadEmployeesCommand = new AsyncRelayCommand(async () => { });
            AddFingerprintCommand = new AsyncRelayCommand<Employee>(async (e) => { });
            SyncFromDeviceCommand = new AsyncRelayCommand(async () => { });
            AddCommand = new AsyncRelayCommand(async () => { });
            EditCommand = new AsyncRelayCommand<Employee>(async (e) => { });
            DeleteCommand = new AsyncRelayCommand<Employee>(async (e) => { });
            
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
                await LoadDevicesAsync();
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
        
        private async Task LoadDevicesAsync()
        {
            try
            {
                var devicesList = await _deviceService.GetAllAsync();
                Devices = new ObservableCollection<Device>(devicesList ?? new List<Device>());
                
                Debug.WriteLine($"Loaded {Devices.Count} devices");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading devices: {ex.Message}");
                ErrorMessage = $"Failed to load devices: {ex.Message}";
            }
        }
        
        private async Task AddEmployeeAsync()
        {
            try
            {
                var dlgVm = new EmployeeDialogViewModel
                {
                    AvailableDepartments = Departments
                };
                
                // Generate unique employee ID
                string employeeId = await _employeeService.GenerateUniqueEmployeeIdAsync();
                dlgVm.EmployeeNumber = employeeId;
                dlgVm.ZkUserId = employeeId;

                var dlg = new Views.EmployeeDialog { DataContext = dlgVm };
                var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow : null;
                
                var result = await dlg.ShowDialog<bool>(mainWindow);
                if (result)
                {
                    var employee = dlgVm.ToEmployee();
                    
                    // Create employee in database
                    await _employeeService.CreateAsync(employee);
                    
                    // Add employee to all active devices
                    foreach (var device in Devices)
                    {
                        try
                        {
                            await _deviceSyncService.AddEmployeeToDeviceAsync(device, employee);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failed to add employee to device {device.Name}: {ex.Message}");
                        }
                    }
                    
                    await LoadEmployeesAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error creating employee: {ex.Message}");
                ErrorMessage = $"Failed to create employee: {ex.Message}";
            }
        }
        
        private async Task EditEmployeeAsync(Employee? employee)
        {
            if (employee == null) return;
            
            try
            {
                var dlgVm = new EmployeeDialogViewModel
                {
                    AvailableDepartments = Departments
                };
                dlgVm.LoadFromEmployee(employee);
                
                // Set the selected department based on the employee's department ID
                dlgVm.SelectedDepartment = Departments.FirstOrDefault(d => d.Id == employee.DepartmentId);
                
                var dlg = new Views.EmployeeDialog { DataContext = dlgVm };
                var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow : null;
                
                var result = await dlg.ShowDialog<bool>(mainWindow);
                if (result)
                {
                    var updatedEmployee = dlgVm.ToEmployee();
                    
                    // Preserve fingerprint data if it wasn't modified
                    if (updatedEmployee.FingerprintTemplate1 == null && employee.FingerprintTemplate1 != null)
                    {
                        updatedEmployee.FingerprintTemplate1 = employee.FingerprintTemplate1;
                    }
                    if (updatedEmployee.FingerprintTemplate2 == null && employee.FingerprintTemplate2 != null)
                    {
                        updatedEmployee.FingerprintTemplate2 = employee.FingerprintTemplate2;
                    }
                    
                    await _employeeService.UpdateAsync(updatedEmployee);
                await LoadEmployeesAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error updating employee: {ex.Message}");
                ErrorMessage = $"Failed to update employee: {ex.Message}";
            }
        }
        
        private async Task DeleteEmployeeAsync(Employee? employee)
        {
            if (employee == null) return;
            
            try
            {
                // Ask for confirmation
                var result = await ShowConfirmationDialogAsync($"Delete {employee.FullName}?", 
                    $"Are you sure you want to delete {employee.FullName}? This action cannot be undone.");
                
                if (!result) return;
                
                // Delete the employee
                await _employeeService.DeleteAsync(employee.Id);
                
                // Reload the list
                await LoadEmployeesAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting employee: {ex.Message}");
                ErrorMessage = $"Failed to delete employee: {ex.Message}";
            }
        }
        
        private async Task DeleteAllEmployeesAsync()
        {
            if (Employees.Count == 0) 
            {
                await ShowMessageAsync("No Employees", "There are no employees to delete.");
                return;
            }
            
            try
            {
                // Ask for confirmation with extra warning
                var result = await ShowConfirmationDialogAsync("Delete ALL Employees?", 
                    $"Are you sure you want to delete ALL {Employees.Count} employees? This action CANNOT be undone and will remove all employee data from the system.");
                
                if (!result) return;
                
                // Double-check with another confirmation
                result = await ShowConfirmationDialogAsync("FINAL WARNING", 
                    "This will permanently delete ALL employee records. Are you absolutely sure?");
                
                if (!result) return;
                
                // Show loading indicator
                IsLoading = true;
                ErrorMessage = string.Empty;
                
                // Delete all employees one by one
                foreach (var employee in Employees.ToList())
                {
                    await _employeeService.DeleteAsync(employee.Id);
                }
                
                // Reload the list
                await LoadEmployeesAsync();
                
                // Show success message
                await ShowMessageAsync("Success", "All employees have been deleted successfully.");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error deleting all employees: {ex.Message}");
                ErrorMessage = $"Failed to delete all employees: {ex.Message}";
                
                // Reload to see what's left
                await LoadEmployeesAsync();
            }
            finally
            {
                IsLoading = false;
            }
        }
        
        private async Task AddFingerprintAsync(Employee? emp)
        {
            if (emp == null) return;
            
            try
            {
                // Check if we have devices configured
                if (Devices.Count == 0)
                {
                    await ShowMessageAsync("No Devices", "Please configure at least one device first to register fingerprints");
                    return;
                }
                
                var dlgVm = new FingerprintDialogViewModel(emp, Devices.ToList());
                var dlg = new Views.FingerprintDialog { DataContext = dlgVm };
                var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow : null;
                
                var result = await dlg.ShowDialog<bool>(mainWindow);
                if (result)
                {
                    // Get all registered templates from the dialog
                    var registeredTemplates = dlg.RegisteredTemplates;
                    
                    if (registeredTemplates != null && registeredTemplates.Count > 0)
                    {
                        // Update the employee with the registered templates
                        Program.LogMessage($"Updating employee {emp.Id} ({emp.FullName}) with {registeredTemplates.Count} fingerprint templates");
                        
                        // Default templates for Employee model (only supports 2 templates)
                        if (registeredTemplates.TryGetValue(4, out byte[]? leftThumb))
                        {
                            emp.FingerprintTemplate1 = leftThumb;
                            Program.LogMessage($"Set Left Thumb (4) template: {leftThumb.Length} bytes");
                        }
                        
                        if (registeredTemplates.TryGetValue(5, out byte[]? rightThumb))
                        {
                            emp.FingerprintTemplate2 = rightThumb;
                            Program.LogMessage($"Set Right Thumb (5) template: {rightThumb.Length} bytes");
                        }
                        
                        // Save the employee to the database
                        await _employeeService.UpdateAsync(emp);
                        Program.LogMessage($"Successfully saved fingerprint templates for employee {emp.Id} ({emp.FullName})");
                    }
                    
                    await LoadEmployeesAsync();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error registering fingerprint: {ex.Message}");
                ErrorMessage = $"Failed to register fingerprint: {ex.Message}";
            }
        }
        
        /// <summary>
        /// Syncs employee data from a device
        /// </summary>
        private async Task SyncFromDeviceAsync()
        {
            if (SelectedDevice == null)
            {
                // If no device is selected, show device selection dialog
                await ShowDeviceSelectionDialogAsync();
                if (SelectedDevice == null) return;
            }
            
            IsSyncing = true;
            SyncStatus = "Connecting to device...";
            
            try
            {
                SyncStatus = $"Syncing users from {SelectedDevice.Name}...";
                var result = await _deviceSyncService.SyncUsersFromDeviceAsync(SelectedDevice);
                
                if (result.Success)
                {
                    await LoadEmployeesAsync();
                    SyncStatus = result.Message;
                    await ShowMessageAsync("Sync Complete", 
                        $"Successfully synced users from device.\n\n" +
                        $"New employees: {result.NewRecords}\n" +
                        $"Updated employees: {result.UpdatedRecords}\n" +
                        $"Skipped: {result.SkippedRecords}");
                }
                else
                {
                    SyncStatus = "Sync failed";
                    await ShowMessageAsync("Sync Failed", result.ErrorMessage);
                }
            }
            catch (Exception ex)
            {
                SyncStatus = "Error during sync";
                await ShowMessageAsync("Sync Error", $"An error occurred during synchronization: {ex.Message}");
            }
            finally
            {
                IsSyncing = false;
            }
        }
        
        private async Task ShowDeviceSelectionDialogAsync()
        {
            try
            {
                var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                    ? desktop.MainWindow : null;
                
                // Create list of devices
                if (Devices.Count == 0)
                {
                    await ShowMessageAsync("No Devices", "No devices are configured. Please add a device first.");
                    return;
                }
                
                var dialog = new Window
                {
                    Title = "Select Device",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                    CanResize = false
                };
                
                var panel = new StackPanel
                {
                    Margin = new Thickness(20),
                    Spacing = 20
                };
                
                panel.Children.Add(new TextBlock
                {
                    Text = "Select a device to sync from:",
                    TextWrapping = TextWrapping.Wrap
                });
                
                var comboBox = new ComboBox
                {
                    Width = 300,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    ItemsSource = Devices,
                    DisplayMemberBinding = new Avalonia.Data.Binding("Name")
                };
                
                panel.Children.Add(comboBox);
                
                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 10
                };
                
                var okButton = new Button
                {
                    Content = "Sync",
                    Width = 80
                };
                
                var cancelButton = new Button
                {
                    Content = "Cancel",
                    Width = 80
                };
                
                okButton.Click += (s, e) =>
                {
                    if (comboBox.SelectedItem is Device selectedDevice)
                    {
                        SelectedDevice = selectedDevice;
                    }
                    dialog.Close();
                };
                
                cancelButton.Click += (s, e) =>
                {
                    dialog.Close();
                };
                
                buttonPanel.Children.Add(cancelButton);
                buttonPanel.Children.Add(okButton);
                panel.Children.Add(buttonPanel);
                dialog.Content = panel;
                
                await dialog.ShowDialog(mainWindow);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error showing device selection dialog: {ex.Message}");
            }
        }
        
        private async Task ShowMessageAsync(string title, string message)
        {
            var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow : null;
                
            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };
            
            var panel = new StackPanel
            {
                Margin = new Thickness(20)
            };
            
            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            });
            
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Center
            };
            
            var okButton = new Button
            {
                Content = "OK",
                Width = 80
            };
            
            okButton.Click += (s, e) => dialog.Close();
            buttonPanel.Children.Add(okButton);
            panel.Children.Add(buttonPanel);
            dialog.Content = panel;
            
            await dialog.ShowDialog(mainWindow);
        }

        private async Task<bool> ShowConfirmationDialogAsync(string title, string message)
        {
            var mainWindow = App.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
                ? desktop.MainWindow : null;

            var dialog = new Window
            {
                Title = title,
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                CanResize = false
            };

            var panel = new StackPanel
            {
                Margin = new Thickness(20)
            };

            panel.Children.Add(new TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 20)
            });

            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Spacing = 10
            };

            var yesButton = new Button
            {
                Content = "Yes",
                Width = 80
            };

            var noButton = new Button
            {
                Content = "No",
                Width = 80
            };

            var result = false;
            
            yesButton.Click += (s, e) =>
            {
                result = true;
                dialog.Close();
            };

            noButton.Click += (s, e) =>
            {
                result = false;
                dialog.Close();
            };

            buttonPanel.Children.Add(noButton);
            buttonPanel.Children.Add(yesButton);
            panel.Children.Add(buttonPanel);
            dialog.Content = panel;

            await dialog.ShowDialog(mainWindow);
            return result;
        }
    }
} 