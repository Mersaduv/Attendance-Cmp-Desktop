using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AttandenceDesktop.ViewModels
{
    public partial class WorkScheduleViewModel : ViewModelBase
    {
        private readonly WorkScheduleService _workScheduleService;
        private readonly DepartmentService _departmentService;

        public WorkScheduleViewModel(WorkScheduleService workScheduleService,
                                      DepartmentService departmentService)
        {
            _workScheduleService = workScheduleService;
            _departmentService = departmentService;
            WorkSchedules = new ObservableCollection<WorkSchedule>();
            LoadCommand = new AsyncRelayCommand(LoadAsync);
            AddCommand = new AsyncRelayCommand(AddAsync);
            EditCommand = new AsyncRelayCommand<WorkSchedule>(EditAsync);
            DeleteCommand = new AsyncRelayCommand<WorkSchedule>(DeleteAsync);
            // initial load
            _ = LoadAsync();
        }

        [ObservableProperty]
        private ObservableCollection<WorkSchedule> _workSchedules;

        [ObservableProperty]
        private WorkSchedule? _selectedSchedule;

        public IAsyncRelayCommand LoadCommand { get; }
        public IAsyncRelayCommand AddCommand { get; }
        public IAsyncRelayCommand<WorkSchedule> EditCommand { get; }
        public IAsyncRelayCommand<WorkSchedule> DeleteCommand { get; }

        private async Task LoadAsync()
        {
            var list = await _workScheduleService.GetAllAsync();
            WorkSchedules = new ObservableCollection<WorkSchedule>(list);
        }

        private async Task AddAsync()
        {
            var depts = await _departmentService.GetAllAsync();
            var dialogVm = new WorkScheduleDialogViewModel(depts);
            var dialog = new AttandenceDesktop.Views.WorkScheduleDialog { DataContext = dialogVm };
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var result = await dialog.ShowDialog<WorkSchedule?>(desktop.MainWindow);
                if (result != null)
                {
                    await _workScheduleService.CreateAsync(result);
                    await LoadAsync();
                }
            }
        }

        private async Task EditAsync(WorkSchedule? schedule)
        {
            if (schedule == null) return;
            var depts = await _departmentService.GetAllAsync();
            var dialogVm = new WorkScheduleDialogViewModel(depts);
            dialogVm.LoadFromEntity(schedule);
            var dialog = new AttandenceDesktop.Views.WorkScheduleDialog { DataContext = dialogVm };
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var result = await dialog.ShowDialog<WorkSchedule?>(desktop.MainWindow);
                if (result != null)
                {
                    await _workScheduleService.UpdateAsync(result);
                    await LoadAsync();
                }
            }
        }

        private async Task DeleteAsync(WorkSchedule? schedule)
        {
            if (schedule == null) return;
            await _workScheduleService.DeleteAsync(schedule.Id);
            await LoadAsync();
        }
    }
} 