using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AttandenceDesktop.Models;

public enum PunchType
{
    Unknown = 0,
    CheckIn = 1,
    CheckOut = 2,
    // Other = 3 (breaks, lunch, etc.)
}

/// <summary>
/// Raw log from the attendance device. Multiple در یک روز ممکن است.
/// </summary>
public class PunchLog
{
    [Key]
    public int Id { get; set; }

    [Required]
    public int EmployeeId { get; set; }

    [ForeignKey("EmployeeId")]
    public Employee Employee { get; set; }

    [Required]
    public int DeviceId { get; set; }

    [ForeignKey("DeviceId")]
    public Device Device { get; set; }

    /// <summary>
    /// تاریخ و ساعت ثبت‌شده روی دستگاه
    /// </summary>
    public DateTime PunchTime { get; set; }

    public PunchType PunchType { get; set; } = PunchType.Unknown;

    /// <summary>
    /// شناسهٔ رکورد خام داخل دستگاه (در ZKTeco معمولاً ترکیب UserId + timestamp است).
    /// </summary>
    [StringLength(50)]
    public string? DeviceRowId { get; set; }
} 