using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;

namespace AttandenceDesktop.ViewModels;

public partial class DepartmentDialogViewModel : ObservableObject
{
    private readonly DepartmentService _departmentService;
    private readonly Department? _editingDepartment;
    private readonly System.Func<Task> _onSaved;

    public DepartmentDialogViewModel(DepartmentService departmentService, System.Func<Task> onSaved, Department? department = null)
    {
        _departmentService = departmentService;
        _editingDepartment = department;
        _onSaved = onSaved;
        DepartmentName = department?.Name ?? string.Empty;
        DialogTitle = department == null ? "Add Department" : "Edit Department";
        SaveCommand = new AsyncRelayCommand(SaveAsync);
    }

    [ObservableProperty]
    private string _departmentName = string.Empty;

    [ObservableProperty]
    private string _dialogTitle = string.Empty;

    public IAsyncRelayCommand SaveCommand { get; }

    private async Task SaveAsync()
    {
        if (string.IsNullOrWhiteSpace(DepartmentName)) return;
        if (_editingDepartment == null)
        {
            var dept = new Department { Name = DepartmentName };
            await _departmentService.CreateAsync(dept);
        }
        else
        {
            _editingDepartment.Name = DepartmentName;
            await _departmentService.UpdateAsync(_editingDepartment);
        }
        await _onSaved();
    }
} 