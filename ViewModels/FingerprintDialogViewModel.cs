using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel.DataAnnotations;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;
using System.Linq;
using System.Collections.Generic;

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

    public byte[]? FingerprintTemplate1 { get; private set; }

    public List<Device> AvailableDevices { get; }

    [ObservableProperty]
    private Device? _selectedDevice;

    public List<int> Fingers { get; } = Enumerable.Range(0, 10).ToList();

    [ObservableProperty]
    private int _selectedFinger = 0;

    public RelayCommand<int> SelectFingerCommand { get; }

    partial void OnSelectedFingerChanged(int value)
    {
        // nothing, hook for future
    }

    private void SelectFinger(int finger) => SelectedFinger = finger;

    public FingerprintDialogViewModel(Employee emp, List<Device> devices)
    {
        Init(emp);
        AvailableDevices = devices;
        SelectedDevice = devices.FirstOrDefault();
        SelectFingerCommand = new RelayCommand<int>(SelectFinger);
    }

    public FingerprintDialogViewModel(Employee emp)
    {
        Init(emp);
        AvailableDevices = new List<Device>();
        SelectFingerCommand = new RelayCommand<int>(SelectFinger);
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
    private void Capture()
    {
        if (SelectedDevice == null) return;
        // Simulate capture logic using SelectedDevice and SelectedFinger
        var random = new Random();
        byte[] template = new byte[512];
        random.NextBytes(template);
        FingerprintTemplate1 = template;
        IsFingerprintRegistered = true;
    }
} 