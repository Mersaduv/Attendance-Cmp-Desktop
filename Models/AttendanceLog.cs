using System;

namespace AttandenceDesktop.Models
{
    /// <summary>
    /// Simple DTO to display a single attendance log record fetched from the REST endpoint.
    /// </summary>
    public class AttendanceLog
    {
        public string UserId { get; set; } = string.Empty;
        public string EmployeeName { get; set; } = string.Empty;
        public string DepartmentName { get; set; } = string.Empty;
        public DateTime DateTime { get; set; }
        public string DateTimeString => DateTime.ToString("yyyy-MM-dd HH:mm:ss");
        public string Date => DateTime.ToString("yyyy-MM-dd");
        public string Time => DateTime.ToString("HH:mm:ss");
        public int VerifyMode { get; set; }
        public string VerifyModeDescription { get; set; } = string.Empty;
        public int InOutMode { get; set; }
        public string InOutModeDescription { get; set; } = string.Empty;
        public int WorkCode { get; set; }
    }
} 