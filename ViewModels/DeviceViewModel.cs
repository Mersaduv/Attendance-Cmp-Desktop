using AttandenceDesktop.Models;
using AttandenceDesktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia;
using System.Collections.Generic;
using System;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using System.Threading.Tasks;
using System.IO;
using System.Text.Json;

namespace AttandenceDesktop.ViewModels;

public partial class DeviceViewModel : ViewModelBase
{
    private readonly DeviceService _service;
    private readonly ZkDataExtractionService _zkDataService;

    public ObservableCollection<Device> Devices { get; } = new();

    [ObservableProperty] private Device? _selectedDevice;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public DeviceViewModel(DeviceService service)
    {
        _service = service;
        _zkDataService = new ZkDataExtractionService();
        _ = LoadAsync();
    }

    public async Task LoadAsync()
    {
        Devices.Clear();
        foreach(var d in await _service.GetAllAsync()) Devices.Add(d);
    }

    [RelayCommand]
    private async Task Add()
    {
        var dlgVm = new DeviceDialogViewModel();
        var dlg = new Views.DeviceDialog{ DataContext = dlgVm };
        var res = await dlg.ShowDialog<bool>(App.Current!.ApplicationLifetime! is IClassicDesktopStyleApplicationLifetime desk ? desk.MainWindow : null);
        if(res) await _service.AddAsync(dlgVm.ToDevice());
    }

    [RelayCommand]
    private async Task Edit(Device? device)
    {
        device ??= SelectedDevice;
        if(device == null) return;
        var dlgVm = new DeviceDialogViewModel();
        dlgVm.Load(device);
        var dlg = new Views.DeviceDialog{ DataContext = dlgVm };
        var res = await dlg.ShowDialog<bool>(App.Current!.ApplicationLifetime! is IClassicDesktopStyleApplicationLifetime desk ? desk.MainWindow : null);
        if(res) await _service.UpdateAsync(dlgVm.ToDevice());
    }

    [RelayCommand]
    private async Task Delete(Device? device)
    {
        device ??= SelectedDevice;
        if(device==null) return;
        await _service.DeleteAsync(device.Id);
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var text = new TextBlock
        {
            Text = message,
            Margin = new Thickness(20),
            TextWrapping = TextWrapping.Wrap
        };

        var okButton = new Button
        {
            Content = "OK",
            Width = 80,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 10, 0, 0)
        };

        var stack = new StackPanel();
        stack.Children.Add(text);
        stack.Children.Add(okButton);

        var dialog = new Window
        {
            Title = title,
            Width = 400,
            Height = 200,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Content = stack
        };

        okButton.Click += (_, __) => dialog.Close();

        var main = (App.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?.MainWindow;
        if (main != null)
        {
            await dialog.ShowDialog(main);
        }
        else
        {
            dialog.Show();
        }
    }

    [RelayCommand]
    private async Task FetchLogs(Device? device)
    {
        device ??= SelectedDevice;
        if (device == null) return;

        try
        {
            Program.LogMessage("=========== BEGINNING FETCH LOGS OPERATION ===========");
            Program.LogMessage($"Device: {device.Name}, IP: {device.IPAddress}, Port: {device.Port}, Machine: {device.MachineNumber}");
            Program.LogMessage($"Current process is {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}");
            
            using var sdk = new ZkemkeeperConnectionService();
            Program.LogMessage("ZkemkeeperConnectionService created successfully");
            
            bool connected = sdk.Connect(device);
            Program.LogMessage($"Connection result: {(connected ? "SUCCESS" : "FAILED")}");
            
            var logs = connected ? sdk.FetchLogs(device) : new List<PunchLog>();

            // Log device details and fetched logs
            Program.LogMessage($"Retrieved {logs.Count} logs from device");
            
            // Log each individual record
            if (logs.Count > 0)
            {
                Program.LogMessage("Device log records:");
                foreach (var log in logs)
                {
                    Program.LogMessage($"  - Employee ID: {log.EmployeeId}, Time: {log.PunchTime}, Type: {log.PunchType}, Device Row ID: {log.DeviceRowId}");
                }
            }
            
            Program.LogMessage("=========== COMPLETED FETCH LOGS OPERATION ===========");

            await ShowMessageAsync("Device Test", 
                connected 
                    ? $"Connection OK. {logs.Count} logs retrieved and saved to app_log.txt." 
                    : "Failed to connect to device.");
        }
        catch(Exception ex)
        {
            Program.LogMessage("=========== ERROR IN FETCH LOGS OPERATION ===========");
            Program.LogMessage($"Error details: {ex.Message}");
            Program.LogMessage($"Exception type: {ex.GetType().FullName}");
            Program.LogMessage($"Stack trace: {ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                Program.LogMessage($"Inner exception: {ex.InnerException.Message}");
                Program.LogMessage($"Inner exception type: {ex.InnerException.GetType().FullName}");
                Program.LogMessage($"Inner exception stack trace: {ex.InnerException.StackTrace}");
            }
            
            Program.LogMessage("==================================================");
            
            await ShowMessageAsync("Error", $"{ex.Message}\n\nDetails have been logged to app_log.txt");
        }
    }

    [RelayCommand]
    private async Task ExtractUsers(Device? device)
    {
        device ??= SelectedDevice;
        if (device == null) return;
        
        IsBusy = true;
        StatusMessage = "Extracting users data...";
        
        try
        {
            Program.LogMessage("=========== BEGINNING USER DATA EXTRACTION ===========");
            Program.LogMessage($"Device: {device.Name}, IP: {device.IPAddress}, Machine: {device.MachineNumber}");
            
            var users = await _zkDataService.GetUsersWithFingerprintsAsync(device);
            
            // Save the result to a JSON file
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"device_{device.Id}_users.json");
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(users, new JsonSerializerOptions { WriteIndented = true }));
            
            Program.LogMessage($"Users data extracted successfully and saved to {filePath}");
            Program.LogMessage("=========== COMPLETED USER DATA EXTRACTION ===========");
            
            await ShowMessageAsync("User Data Extraction", 
                $"Successfully extracted data for {users.Count} users.\nData saved to {filePath}");
        }
        catch (Exception ex)
        {
            Program.LogMessage("=========== ERROR IN USER DATA EXTRACTION ===========");
            Program.LogMessage($"Error details: {ex.Message}");
            Program.LogMessage($"Stack trace: {ex.StackTrace}");
            
            await ShowMessageAsync("Error", $"Failed to extract user data: {ex.Message}\n\nDetails have been logged to app_log.txt");
        }
        finally
        {
            IsBusy = false;
            StatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task ExtractAllData(Device? device)
    {
        device ??= SelectedDevice;
        if (device == null) return;
        
        IsBusy = true;
        StatusMessage = "Extracting all device data...";
        
        try
        {
            Program.LogMessage("=========== BEGINNING ALL DATA EXTRACTION ===========");
            Program.LogMessage($"Device: {device.Name}, IP: {device.IPAddress}, Machine: {device.MachineNumber}");
            
            var allData = await _zkDataService.GetAllDeviceDataAsync(device);
            
            // Save the result to a JSON file
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"device_{device.Id}_all_data.json");
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(allData, new JsonSerializerOptions { WriteIndented = true }));
            
            Program.LogMessage($"All device data extracted successfully and saved to {filePath}");
            Program.LogMessage("=========== COMPLETED ALL DATA EXTRACTION ===========");
            
            await ShowMessageAsync("All Data Extraction", 
                $"Successfully extracted all device data.\nData saved to {filePath}");
        }
        catch (Exception ex)
        {
            Program.LogMessage("=========== ERROR IN ALL DATA EXTRACTION ===========");
            Program.LogMessage($"Error details: {ex.Message}");
            Program.LogMessage($"Stack trace: {ex.StackTrace}");
            
            await ShowMessageAsync("Error", $"Failed to extract all data: {ex.Message}\n\nDetails have been logged to app_log.txt");
        }
        finally
        {
            IsBusy = false;
            StatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    private async Task ExtractAttendanceLogs(Device? device)
    {
        device ??= SelectedDevice;
        if (device == null) return;
        
        IsBusy = true;
        StatusMessage = "Extracting attendance logs...";
        
        try
        {
            Program.LogMessage("=========== BEGINNING ATTENDANCE LOGS EXTRACTION ===========");
            Program.LogMessage($"Device: {device.Name}, IP: {device.IPAddress}, Machine: {device.MachineNumber}");
            
            var attendanceLogs = await _zkDataService.GetAttendanceLogsAsync(device);
            
            // Save the result to a JSON file
            var filePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, $"device_{device.Id}_attendance.json");
            await File.WriteAllTextAsync(filePath, JsonSerializer.Serialize(attendanceLogs, new JsonSerializerOptions { WriteIndented = true }));
            
            Program.LogMessage($"Attendance logs extracted successfully and saved to {filePath}");
            Program.LogMessage("=========== COMPLETED ATTENDANCE LOGS EXTRACTION ===========");
            
            int logCount = 0;
            if (attendanceLogs.TryGetValue("totalLogs", out var total) && total is int totalLogs)
            {
                logCount = totalLogs;
            }
            
            await ShowMessageAsync("Attendance Logs Extraction", 
                $"Successfully extracted {logCount} attendance logs.\nData saved to {filePath}");
        }
        catch (Exception ex)
        {
            Program.LogMessage("=========== ERROR IN ATTENDANCE LOGS EXTRACTION ===========");
            Program.LogMessage($"Error details: {ex.Message}");
            Program.LogMessage($"Stack trace: {ex.StackTrace}");
            
            await ShowMessageAsync("Error", $"Failed to extract attendance logs: {ex.Message}\n\nDetails have been logged to app_log.txt");
        }
        finally
        {
            IsBusy = false;
            StatusMessage = string.Empty;
        }
    }
} 