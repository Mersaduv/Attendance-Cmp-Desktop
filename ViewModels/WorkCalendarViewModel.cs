using System.Collections.ObjectModel;
using System.Threading.Tasks;
using AttandenceDesktop.Models;
using AttandenceDesktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace AttandenceDesktop.ViewModels
{
    public partial class WorkCalendarViewModel : ViewModelBase
    {
        private readonly WorkCalendarService _workCalendarService;

        public WorkCalendarViewModel(WorkCalendarService workCalendarService)
        {
            _workCalendarService = workCalendarService;
            WorkCalendars = new ObservableCollection<WorkCalendar>();
            LoadCommand = new AsyncRelayCommand(LoadAsync);
            AddCommand = new AsyncRelayCommand(AddAsync);
            EditCommand = new AsyncRelayCommand<WorkCalendar>(EditAsync);
            DeleteCommand = new AsyncRelayCommand<WorkCalendar>(DeleteAsync);
            _ = LoadAsync();
        }

        [ObservableProperty]
        private ObservableCollection<WorkCalendar> _workCalendars;

        [ObservableProperty]
        private WorkCalendar? _selectedCalendar;

        public IAsyncRelayCommand LoadCommand { get; }
        public IAsyncRelayCommand AddCommand { get; }
        public IAsyncRelayCommand<WorkCalendar> EditCommand { get; }
        public IAsyncRelayCommand<WorkCalendar> DeleteCommand { get; }

        private async Task LoadAsync()
        {
            var list = await _workCalendarService.GetAllAsync();
            WorkCalendars = new ObservableCollection<WorkCalendar>(list);
        }

        private async Task AddAsync()
        {
            var dialogVm = new WorkCalendarDialogViewModel();
            var dialog = new AttandenceDesktop.Views.WorkCalendarDialog { DataContext = dialogVm };
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var result = await dialog.ShowDialog<WorkCalendar?>(desktop.MainWindow);
                if (result != null)
                {
                    await _workCalendarService.CreateAsync(result);
                    await LoadAsync();
                }
            }
        }

        private async Task EditAsync(WorkCalendar? entry)
        {
            if (entry == null) return;
            var dialogVm = new WorkCalendarDialogViewModel();
            dialogVm.LoadFromEntity(entry);
            var dialog = new AttandenceDesktop.Views.WorkCalendarDialog { DataContext = dialogVm };
            if (App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop && desktop.MainWindow != null)
            {
                var result = await dialog.ShowDialog<WorkCalendar?>(desktop.MainWindow);
                if (result != null)
                {
                    await _workCalendarService.UpdateAsync(result);
                    await LoadAsync();
                }
            }
        }

        private async Task DeleteAsync(WorkCalendar? entry)
        {
            if (entry == null) return;
            await _workCalendarService.DeleteAsync(entry.Id);
            await LoadAsync();
        }
    }
} 