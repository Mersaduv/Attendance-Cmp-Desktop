using System;
using System.ComponentModel.DataAnnotations;
using AttandenceDesktop.Models;
using CommunityToolkit.Mvvm.ComponentModel;

namespace AttandenceDesktop.ViewModels
{
    public class WorkCalendarDialogViewModel : ObservableValidator
    {
        public bool IsNew => Id == 0;
        public string WindowTitle => IsNew ? "Add Calendar Entry" : "Edit Calendar Entry";

        private int _id;
        public int Id { get => _id; set => SetProperty(ref _id, value); }

        [Required]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        [Required]
        public CalendarEntryType EntryType { get; set; } = CalendarEntryType.Holiday;

        public bool IsRecurringAnnually { get; set; }

        public Array EntryTypes => Enum.GetValues(typeof(CalendarEntryType));

        public WorkCalendar ToEntity()
        {
            return new WorkCalendar
            {
                Id = Id,
                Date = Date.Date,
                Name = Name,
                Description = Description,
                EntryType = EntryType,
                IsRecurringAnnually = IsRecurringAnnually
            };
        }

        public void LoadFromEntity(WorkCalendar entity)
        {
            if (entity == null) return;
            Id = entity.Id;
            Date = entity.Date;
            Name = entity.Name;
            Description = entity.Description;
            EntryType = entity.EntryType;
            IsRecurringAnnually = entity.IsRecurringAnnually;
        }
    }
} 