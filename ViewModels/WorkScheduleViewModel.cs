using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace AttandenceDesktop.ViewModels
{
    public partial class WorkScheduleViewModel : ViewModelBase, IDisposable
    {
        private readonly WorkScheduleService _workScheduleService;
        private readonly DepartmentService _departmentService;
        private readonly DataRefreshService _dataRefreshService;

        public WorkScheduleViewModel(WorkScheduleService workScheduleService,
                                      DepartmentService departmentService,
                                      DataRefreshService dataRefreshService)
        {
            _workScheduleService = workScheduleService;
            _departmentService = departmentService;
            _dataRefreshService = dataRefreshService;
            
            WorkSchedules = new ObservableCollection<WorkSchedule>();
            LoadCommand = new AsyncRelayCommand(LoadAsync);
            AddCommand = new AsyncRelayCommand(AddAsync);
            EditCommand = new AsyncRelayCommand<WorkSchedule>(EditAsync);
            DeleteCommand = new AsyncRelayCommand<WorkSchedule>(DeleteAsync);
            
            // Subscribe to data change events
            _dataRefreshService.WorkSchedulesChanged += OnWorkSchedulesChanged;
            _dataRefreshService.DepartmentsChanged += OnDepartmentsChanged;
            
            // initial load
            _ = LoadAsync();
        }
        
        // Parameterless constructor for design-time support
        public WorkScheduleViewModel()
        {
            _workScheduleService = null!;
            _departmentService = null!;
            _dataRefreshService = null!;
            
            WorkSchedules = new ObservableCollection<WorkSchedule>();
            LoadCommand = new AsyncRelayCommand(async () => {});
            AddCommand = new AsyncRelayCommand(async () => {});
            EditCommand = new AsyncRelayCommand<WorkSchedule>(async _ => {});
            DeleteCommand = new AsyncRelayCommand<WorkSchedule>(async _ => {});
            
            // Add design-time data
            WorkSchedules.Add(new WorkSchedule { 
                Id = 1, 
                Name = "Standard 9-5", 
                StartTime = new TimeSpan(9, 0, 0),
                EndTime = new TimeSpan(17, 0, 0),
                IsWorkingDayMonday = true,
                IsWorkingDayTuesday = true,
                IsWorkingDayWednesday = true,
                IsWorkingDayThursday = true,
                IsWorkingDayFriday = true,
                FlexTimeAllowanceMinutes = 15
            });
            WorkSchedules.Add(new WorkSchedule { 
                Id = 2, 
                Name = "Night Shift", 
                StartTime = new TimeSpan(22, 0, 0),
                EndTime = new TimeSpan(6, 0, 0),
                IsWorkingDayMonday = true,
                IsWorkingDayTuesday = true,
                IsWorkingDayWednesday = true,
                IsWorkingDayThursday = true,
                IsWorkingDayFriday = false,
                FlexTimeAllowanceMinutes = 10
            });
        }

        public void Dispose()
        {
            // Unsubscribe from events
            _dataRefreshService.WorkSchedulesChanged -= OnWorkSchedulesChanged;
            _dataRefreshService.DepartmentsChanged -= OnDepartmentsChanged;
        }

        private void OnWorkSchedulesChanged(object? sender, EventArgs e)
        {
            _ = LoadAsync();
        }

        private void OnDepartmentsChanged(object? sender, EventArgs e)
        {
            // Reload if departments change, as schedules may be linked to departments
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