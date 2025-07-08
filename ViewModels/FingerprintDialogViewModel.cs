using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel.DataAnnotations;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

namespace AttandenceDesktop.ViewModels;

public partial class FingerprintDialogViewModel : ObservableValidator
{
    public int EmployeeId { get; private set; }
    public string EmployeeName { get; private set; }

    [ObservableProperty]
    [Required]
    [StringLength(50)]
    private string? _zkUserId;

    [ObservableProperty]
    private bool _isFingerprintRegistered;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isCapturing;

    public byte[]? FingerprintTemplate1 { get; private set; }

    public List<Device> AvailableDevices { get; }

    [ObservableProperty]
    private Device? _selectedDevice;

    public List<int> Fingers { get; } = Enumerable.Range(0, 10).ToList();

    [ObservableProperty]
    private int _selectedFinger = 0;

    public RelayCommand<string> SelectFingerCommand { get; }

    partial void OnSelectedFingerChanged(int value)
    {
        // nothing, hook for future
    }

    private void SelectFinger(string fingerStr)
    {
        if (int.TryParse(fingerStr, out int finger))
        {
            SelectedFinger = finger;
        }
    }

    public FingerprintDialogViewModel(Employee emp, List<Device> devices)
    {
        Init(emp);
        AvailableDevices = devices;
        SelectedDevice = devices.FirstOrDefault();
        SelectFingerCommand = new RelayCommand<string>(SelectFinger);
    }

    public FingerprintDialogViewModel(Employee emp)
    {
        Init(emp);
        AvailableDevices = new List<Device>();
        SelectFingerCommand = new RelayCommand<string>(SelectFinger);
    }

    private void Init(Employee emp)
    {
        EmployeeId = emp.Id;
        EmployeeName = emp.FullName;
        ZkUserId = string.IsNullOrWhiteSpace(emp.ZkUserId) ? emp.EmployeeNumber : emp.ZkUserId;
        IsFingerprintRegistered = emp.FingerprintTemplate1 != null;
        FingerprintTemplate1 = emp.FingerprintTemplate1;
    }

    [RelayCommand]
    private async Task Capture()
    {
        if (SelectedDevice == null)
        {
            StatusMessage = "Please select a device";
            Program.LogMessage("Fingerprint capture failed: No device selected");
            return;
        }

        if (string.IsNullOrWhiteSpace(ZkUserId))
        {
            StatusMessage = "ZK User ID cannot be empty";
            Program.LogMessage("Fingerprint capture failed: ZK User ID is empty");
            return;
        }

        try
        {
            StatusMessage = "Connecting to device...";
            IsCapturing = true;
            Program.LogMessage($"Starting fingerprint capture process for employee {EmployeeId} ({EmployeeName}), finger {SelectedFinger}");
            
            // Create a ZK connection service and attempt to register fingerprint
            using var zkService = new ZkemkeeperConnectionService();
            
            // Execute the operation on a background thread to avoid UI blocking
            await Task.Run(() => 
            {
                Program.LogMessage($"Connecting to device {SelectedDevice.Name} ({SelectedDevice.IPAddress}:{SelectedDevice.Port})");
                
                // First attempt to connect
                if (!zkService.Connect(SelectedDevice))
                {
                    StatusMessage = "Error connecting to device";
                    Program.LogMessage($"Failed to connect to device {SelectedDevice.Name}");
                    return;
                }
                
                Program.LogMessage("Connection successful, proceeding with fingerprint registration");
                StatusMessage = "Place your finger on the sensor...";
                
                // Register fingerprint
                var (success, message, template) = zkService.RegisterFingerprint(SelectedDevice, ZkUserId, SelectedFinger);
                
                if (success && template != null)
                {
                    FingerprintTemplate1 = template;
                    IsFingerprintRegistered = true;
                    StatusMessage = "Fingerprint registered successfully";
                    Program.LogMessage($"Successfully registered fingerprint for employee {EmployeeId} ({EmployeeName}), finger {SelectedFinger}");
                }
                else
                {
                    StatusMessage = $"Error registering fingerprint: {message}";
                    Program.LogMessage($"Fingerprint registration failed: {message}");
                }
            });
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Program.LogMessage($"Exception during fingerprint capture: {ex.Message}");
            if (ex.InnerException != null)
            {
                Program.LogMessage($"Inner exception: {ex.InnerException.Message}");
            }
        }
        finally
        {
            IsCapturing = false;
        }
    }
} 