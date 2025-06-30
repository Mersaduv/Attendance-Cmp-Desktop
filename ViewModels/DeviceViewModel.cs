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

namespace AttandenceDesktop.ViewModels;

public partial class DeviceViewModel : ViewModelBase
{
    private readonly DeviceService _service;
    private readonly DataRefreshService _refresh;

    public ObservableCollection<Device> Devices { get; } = new();

    [ObservableProperty] private Device? _selectedDevice;

    public DeviceViewModel(DeviceService service, DataRefreshService refresh)
    {
        _service = service; _refresh = refresh;
        _refresh.DevicesChanged += async (_, __) => await LoadAsync();
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
            using var zk = new ZkTecoConnectionService();
            bool connected = zk.Connect(device);
            var logs = connected ? zk.FetchLogs(device) : new List<PunchLog>();

            await ShowMessageAsync("Device Test", connected ? $"Connection OK. Logs retrieved: {logs.Count}." : "Failed to connect to device.");
        }
        catch(Exception ex)
        {
            await ShowMessageAsync("Error", ex.Message);
        }
    }
} 