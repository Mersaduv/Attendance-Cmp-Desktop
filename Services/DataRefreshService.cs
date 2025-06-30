using System;

namespace AttandenceDesktop.Services
{
    public class DataRefreshService
    {
        // Events for different data types
        public event EventHandler EmployeesChanged;
        public event EventHandler DepartmentsChanged;
        public event EventHandler AttendanceChanged;
        public event EventHandler WorkSchedulesChanged;
        public event EventHandler WorkCalendarsChanged;
        public event EventHandler DevicesChanged;
        
        // Methods to notify subscribers about data changes
        public void NotifyEmployeesChanged()
        {
            EmployeesChanged?.Invoke(this, EventArgs.Empty);
        }
        
        public void NotifyDepartmentsChanged()
        {
            DepartmentsChanged?.Invoke(this, EventArgs.Empty);
        }
        
        public void NotifyAttendanceChanged()
        {
            AttendanceChanged?.Invoke(this, EventArgs.Empty);
        }
        
        public void NotifyWorkSchedulesChanged()
        {
            WorkSchedulesChanged?.Invoke(this, EventArgs.Empty);
        }
        
        public void NotifyWorkCalendarsChanged()
        {
            WorkCalendarsChanged?.Invoke(this, EventArgs.Empty);
        }
        
        public void NotifyDevicesChanged()
        {
            DevicesChanged?.Invoke(this, EventArgs.Empty);
        }
    }
} 