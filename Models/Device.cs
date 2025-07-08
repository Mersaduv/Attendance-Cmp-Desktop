using System.ComponentModel.DataAnnotations;

namespace AttandenceDesktop.Models;

/// <summary>
/// Represents a physical attendance device (e.g., ZKTeco fingerprint reader).
/// Keeping it simple for first migration – can be extended later.
/// </summary>
public class Device
{
    [Key]
    public int Id { get; set; }

    [Required, StringLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// IP or hostname used to connect over TCP/IP.
    /// </summary>
    [Required, StringLength(100)]
    public string IPAddress { get; set; } = "";

    /// <summary>
    /// Network port (ZKTeco پیش‌فرض 4370).
    /// </summary>
    public int Port { get; set; } = 4370;

    /// <summary>
    /// DeviceNumber or MachineNumber used by SDK (معمولاً 1).
    /// </summary>
    public int MachineNumber { get; set; } = 1;

    [StringLength(50)]
    public string? SerialNumber { get; set; }
    
    /// <summary>
    /// رمز ارتباطی برای اتصال به دستگاه (Communication Password)
    /// معمولا 0 است، اما گاهی به صورت دیگری تنظیم می‌شود
    /// </summary>
    [StringLength(50)]
    public string? CommunicationPassword { get; set; } = "0";

    /// <summary>
    /// آخرین باری که لاگ‌ها از این دستگاه خوانده شده است.
    /// </summary>
    public DateTime? LastSyncTime { get; set; }

    [StringLength(250)]
    public string? Description { get; set; }

    // Navigation
    public ICollection<PunchLog> PunchLogs { get; set; } = new List<PunchLog>();
} 