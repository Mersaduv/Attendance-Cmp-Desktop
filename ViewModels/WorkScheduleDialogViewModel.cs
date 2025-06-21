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
                DepartmentId = DepartmentId
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
            SelectedDepartment = _departments.Find(d => d.Id == DepartmentId);
        }
    }
} 