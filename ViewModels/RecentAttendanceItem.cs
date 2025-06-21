namespace AttandenceDesktop.ViewModels;

public record RecentAttendanceItem(
    string EmployeeName,
    string DepartmentName,
    string Date,
    string CheckIn,
    string CheckOut,
    string Status); 