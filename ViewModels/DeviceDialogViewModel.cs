using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.ComponentModel.DataAnnotations;
using AttandenceDesktop.Models;

namespace AttandenceDesktop.ViewModels;

public partial class DeviceDialogViewModel : ObservableValidator
{
    [ObservableProperty] private int _id;

    [Required(ErrorMessage="Name required")]
    [ObservableProperty] [NotifyDataErrorInfo] private string _name = string.Empty;

    [Required] [ObservableProperty] private string _ipAddress = string.Empty;
    [ObservableProperty] private int _port = 4370;
    [ObservableProperty] private int _machineNumber = 1;
    [ObservableProperty] private string? _serialNumber;
    [ObservableProperty] private string? _description;

    public string WindowTitle => _id == 0 ? "Add Device" : "Edit Device";

    public Device ToDevice() => new()
    {
        Id = Id, Name = Name, IPAddress = IpAddress, Port = Port,
        MachineNumber = MachineNumber, SerialNumber = SerialNumber,
        Description = Description
    };

    public void Load(Device d)
    {
        Id = d.Id; Name = d.Name; IpAddress = d.IPAddress; Port = d.Port;
        MachineNumber = d.MachineNumber; SerialNumber = d.SerialNumber;
        Description = d.Description;
    }
} 