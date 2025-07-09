using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel.DataAnnotations;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;
using System.Linq;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

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

    // Track which fingerprints are registered
    [ObservableProperty]
    private ObservableCollection<int> _registeredFingerprints = new ObservableCollection<int>();

    // Store all fingerprint templates
    private Dictionary<int, byte[]> _fingerprintTemplates = new Dictionary<int, byte[]>();

    public byte[]? FingerprintTemplate1 { get; private set; }
    public byte[]? FingerprintTemplate2 { get; private set; }

    public List<Device> AvailableDevices { get; }

    [ObservableProperty]
    private Device? _selectedDevice;

    public List<int> Fingers { get; } = Enumerable.Range(0, 10).ToList();

    [ObservableProperty]
    private int _selectedFinger = 0;

    public RelayCommand<string> SelectFingerCommand { get; }

    partial void OnSelectedFingerChanged(int value)
    {
        // Update status message when finger selection changes
        if (RegisteredFingerprints.Contains(value))
        {
            StatusMessage = $"Fingerprint already registered for {GetFingerName(value)}";
        }
        else
        {
            StatusMessage = $"Ready to capture {GetFingerName(value)}";
        }
    }

    private string GetFingerName(int fingerNumber)
    {
        var fingerNames = new Dictionary<int, string>
        {
            { 0, "Left Little Finger" },
            { 1, "Left Ring Finger" },
            { 2, "Left Middle Finger" },
            { 3, "Left Index Finger" },
            { 4, "Left Thumb" },
            { 5, "Right Thumb" },
            { 6, "Right Index Finger" },
            { 7, "Right Middle Finger" },
            { 8, "Right Ring Finger" },
            { 9, "Right Little Finger" }
        };

        return fingerNames.TryGetValue(fingerNumber, out string? name) ? name : "Unknown Finger";
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
        
        // Initialize fingerprint templates
        _fingerprintTemplates = new Dictionary<int, byte[]>();
        RegisteredFingerprints.Clear();
        
        // Check for existing templates in the employee record
        // In the database, we store:
        // - FingerprintTemplate1 = Left Thumb (index 4)
        // - FingerprintTemplate2 = Right Thumb (index 5)
        
        if (emp.FingerprintTemplate1 != null)
        {
            // Left Thumb (4)
            _fingerprintTemplates[4] = emp.FingerprintTemplate1;
            RegisteredFingerprints.Add(4);
            Program.LogMessage($"Found existing template for Left Thumb (4) for employee {emp.FullName}");
        }
        
        if (emp.FingerprintTemplate2 != null)
        {
            // Right Thumb (5)
            _fingerprintTemplates[5] = emp.FingerprintTemplate2;
            RegisteredFingerprints.Add(5);
            Program.LogMessage($"Found existing template for Right Thumb (5) for employee {emp.FullName}");
        }
        
        IsFingerprintRegistered = RegisteredFingerprints.Count > 0;
        
        // Set initial status message
        if (IsFingerprintRegistered)
        {
            StatusMessage = $"Employee has {RegisteredFingerprints.Count} registered fingerprint(s)";
            
            // Set the selected finger to one of the registered fingers if available
            if (RegisteredFingerprints.Count > 0)
            {
                SelectedFinger = RegisteredFingerprints.First();
            }
        }
        else
        {
            StatusMessage = "No fingerprints registered yet";
            
            // Default to thumb fingers as they're most commonly used
            SelectedFinger = 4; // Left Thumb
        }
    }

    [RelayCommand]
    private async Task Capture()
    {
        if (SelectedDevice == null)
        {
            StatusMessage = "Please select a device first";
            return;
        }

        IsCapturing = true;
        StatusMessage = "Capturing fingerprint... Place finger on sensor";

        try
        {
            // Create a connection service for the device
            using var connectionService = new ZkemkeeperConnectionService();

            // Attempt to register the fingerprint
            var result = connectionService.RegisterFingerprint(SelectedDevice, ZkUserId, SelectedFinger);

            if (result.Success && result.TemplateData != null)
            {
                // Store the template in our local dictionary
                _fingerprintTemplates[SelectedFinger] = result.TemplateData;
                
                // Add to registered fingerprints list if not already there
                if (!RegisteredFingerprints.Contains(SelectedFinger))
                {
                    RegisteredFingerprints.Add(SelectedFinger);
                }
                
                IsFingerprintRegistered = true;
                StatusMessage = $"Fingerprint registered successfully for {FingerNumberToName(SelectedFinger)}";
                
                // Assign to the appropriate template property based on finger index
                if (SelectedFinger == 4) // Left Thumb
                {
                    FingerprintTemplate1 = result.TemplateData;
                    Program.LogMessage($"Stored Left Thumb (4) template in FingerprintTemplate1");
                }
                else if (SelectedFinger == 5) // Right Thumb
                {
                    FingerprintTemplate2 = result.TemplateData;
                    Program.LogMessage($"Stored Right Thumb (5) template in FingerprintTemplate2");
                }
                else
                {
                    // For other fingers, use the first available template slot
                    if (FingerprintTemplate1 == null)
                    {
                        FingerprintTemplate1 = result.TemplateData;
                        Program.LogMessage($"Stored {FingerNumberToName(SelectedFinger)} ({SelectedFinger}) template in FingerprintTemplate1");
                    }
                    else if (FingerprintTemplate2 == null)
                    {
                        FingerprintTemplate2 = result.TemplateData;
                        Program.LogMessage($"Stored {FingerNumberToName(SelectedFinger)} ({SelectedFinger}) template in FingerprintTemplate2");
                    }
                    else
                    {
                        // If both slots are full, prioritize thumbs, otherwise replace the first template
                        if (SelectedFinger < 4 || SelectedFinger > 5) // Not a thumb
                        {
                            FingerprintTemplate1 = result.TemplateData;
                            Program.LogMessage($"Replaced FingerprintTemplate1 with {FingerNumberToName(SelectedFinger)} ({SelectedFinger}) template");
                        }
                    }
                }
            }
            else
            {
                StatusMessage = $"Failed to register fingerprint: {result.Message}";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            Program.LogMessage($"Error in fingerprint capture: {ex.Message}");
        }
        finally
        {
            IsCapturing = false;
        }
    }
    
    private string FingerNumberToName(int fingerNumber)
    {
        return fingerNumber switch
        {
            0 => "Left Little Finger",
            1 => "Left Ring Finger",
            2 => "Left Middle Finger",
            3 => "Left Index Finger",
            4 => "Left Thumb",
            5 => "Right Thumb",
            6 => "Right Index Finger",
            7 => "Right Middle Finger",
            8 => "Right Ring Finger",
            9 => "Right Little Finger",
            _ => $"Unknown Finger ({fingerNumber})"
        };
    }
    
    // Get all registered fingerprint templates
    public Dictionary<int, byte[]> GetRegisteredTemplates()
    {
        return _fingerprintTemplates;
    }
} 