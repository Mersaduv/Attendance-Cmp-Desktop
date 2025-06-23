using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AttandenceDesktop.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AttandenceDesktop.ViewModels
{
    public class WorkScheduleDialogViewModel : ObservableValidator
    {
        private readonly List<Department> _departments;

        public WorkScheduleDialogViewModel(List<Department> departments)
        {
            _departments = departments;
            Name = string.Empty;
            StartTime = new TimeSpan(9,0,0);
            EndTime = new TimeSpan(17,0,0);
            FlexTimeAllowanceMinutes = 15; // Default grace period: 15 minutes
            
            // Set default working days (Monday to Friday)
            IsWorkingDaySunday = false;
            IsWorkingDayMonday = true;
            IsWorkingDayTuesday = true;
            IsWorkingDayWednesday = true;
            IsWorkingDayThursday = true;
            IsWorkingDayFriday = true;
            IsWorkingDaySaturday = false;
        }

        public bool IsNew => Id == 0;
        public string WindowTitle => IsNew ? "Add Schedule" : "Edit Schedule";

        public List<Department> Departments => _departments;

        private int _id;
        public int Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        [Required]
        public string Name { get; set; }

        [Required]
        public TimeSpan StartTime { get; set; }

        [Required]
        public TimeSpan EndTime { get; set; }
        
        [Range(0, 60, ErrorMessage = "Grace period must be between 0 and 60 minutes")]
        public int FlexTimeAllowanceMinutes { get; set; }
        
        // Working days properties
        public bool IsWorkingDaySunday { get; set; }
        public bool IsWorkingDayMonday { get; set; }
        public bool IsWorkingDayTuesday { get; set; }
        public bool IsWorkingDayWednesday { get; set; }
        public bool IsWorkingDayThursday { get; set; }
        public bool IsWorkingDayFriday { get; set; }
        public bool IsWorkingDaySaturday { get; set; }

        public int? DepartmentId { get; set; }

        private Department? _selectedDepartment;
        public Department? SelectedDepartment
        {
            get => _selectedDepartment;
            set
            {
                SetProperty(ref _selectedDepartment, value);
                if (value != null) DepartmentId = value.Id;
            }
        }

        public WorkSchedule ToEntity()
        {
            return new WorkSchedule
            {
                Id = Id,
                Name = Name,
                StartTime = StartTime,
                EndTime = EndTime,
                DepartmentId = DepartmentId,
                FlexTimeAllowanceMinutes = FlexTimeAllowanceMinutes,
                // Include working day settings
                IsWorkingDaySunday = IsWorkingDaySunday,
                IsWorkingDayMonday = IsWorkingDayMonday,
                IsWorkingDayTuesday = IsWorkingDayTuesday,
                IsWorkingDayWednesday = IsWorkingDayWednesday,
                IsWorkingDayThursday = IsWorkingDayThursday,
                IsWorkingDayFriday = IsWorkingDayFriday,
                IsWorkingDaySaturday = IsWorkingDaySaturday
            };
        }

        public void LoadFromEntity(WorkSchedule schedule)
        {
            if (schedule == null) return;
            Id = schedule.Id;
            Name = schedule.Name;
            StartTime = schedule.StartTime;
            EndTime = schedule.EndTime;
            DepartmentId = schedule.DepartmentId;
            FlexTimeAllowanceMinutes = schedule.FlexTimeAllowanceMinutes;
            // Load working day settings
            IsWorkingDaySunday = schedule.IsWorkingDaySunday;
            IsWorkingDayMonday = schedule.IsWorkingDayMonday;
            IsWorkingDayTuesday = schedule.IsWorkingDayTuesday;
            IsWorkingDayWednesday = schedule.IsWorkingDayWednesday;
            IsWorkingDayThursday = schedule.IsWorkingDayThursday;
            IsWorkingDayFriday = schedule.IsWorkingDayFriday;
            IsWorkingDaySaturday = schedule.IsWorkingDaySaturday;
            SelectedDepartment = _departments.Find(d => d.Id == DepartmentId);
        }
    }
} 