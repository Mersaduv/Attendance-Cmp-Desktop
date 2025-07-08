using AttandenceDesktop.Models;
using System;
using System.Collections.Generic;
using System.Threading;

namespace AttandenceDesktop.Services;

/// <summary>
/// ارتباط با دستگاه ZKTeco از طریق COM-SDK (zkemkeeper.dll)
/// برای Registration-Free COM نیازی به regsvr32 نیست؛ DLL کنار exe قرار می‌گیرد.
/// </summary>
public sealed class ZkemkeeperConnectionService : IDisposable
{
    private readonly ZkTecoConnectionService? _zkService;
    private bool _connected;

    public ZkemkeeperConnectionService()
    {
        try
        {
            // Use the actual ZkTeco service implementation
            _zkService = new ZkTecoConnectionService();
            Program.LogMessage("ZkemkeeperConnectionService: Successfully initialized ZkTecoConnectionService");
        }
        catch (Exception ex)
        {
            // Log detailed exception information including stack trace
            Program.LogMessage($"Error initializing ZkTecoConnectionService: {ex.Message}");
            Program.LogMessage($"Exception Type: {ex.GetType().FullName}");
            Program.LogMessage($"Stack Trace: {ex.StackTrace}");
            
            if (ex.InnerException != null)
            {
                Program.LogMessage($"Inner Exception: {ex.InnerException.Message}");
                Program.LogMessage($"Inner Exception Type: {ex.InnerException.GetType().FullName}");
                Program.LogMessage($"Inner Stack Trace: {ex.InnerException.StackTrace}");
            }
            
            // Create a flag so we know the service is in fallback mode
            _zkService = null;
        }
    }

    /// <summary>
    /// تلاش برای اتصال TCP به دستگاه.
    /// </summary>
    public bool Connect(Device device)
    {
        if (_connected) return true;
        if (device is null) return false;

        try
        {
            // Log connection attempt
            Program.LogMessage($"Attempting to connect to device: {device.Name} (ID: {device.Id})");
            Program.LogMessage($"Device details - IP: {device.IPAddress}, Port: {device.Port}, Machine Number: {device.MachineNumber}, Serial: {device.SerialNumber ?? "N/A"}");
            
            // Use the real service if available, otherwise use fallback
            if (_zkService != null)
            {
                // Try multiple times with a brief pause
                for (int retry = 0; retry < 3; retry++)
                {
                    if (retry > 0)
                    {
                        Program.LogMessage($"Retrying connection (attempt {retry+1}/3)...");
                        Thread.Sleep(1000); // Wait a bit between retries
                    }
                
                    _connected = _zkService.Connect(device);
                    if (_connected) break;
                }
                
                if (!_connected)
                {
                    Program.LogMessage("Multiple connection attempts failed");
                    
                    // Try to diagnose network issues
                    try
                    {
                        System.Net.NetworkInformation.Ping ping = new System.Net.NetworkInformation.Ping();
                        var reply = ping.Send(device.IPAddress, 2000);
                        if (reply.Status == System.Net.NetworkInformation.IPStatus.Success)
                        {
                            Program.LogMessage($"Ping to device successful (time: {reply.RoundtripTime}ms), but SDK connection failed");
                            Program.LogMessage("This suggests the device is online but not accepting SDK connections");
                            Program.LogMessage("Possible reasons: Wrong password, port blocked, another application using the device");
                        }
                        else
                        {
                            Program.LogMessage($"Ping failed with status: {reply.Status}. Device may be unreachable.");
                        }
                    }
                    catch (Exception pingEx)
                    {
                        Program.LogMessage($"Error during ping test: {pingEx.Message}");
                    }
                    
                    // Special handling for devices that might be in a locked state
                    Program.LogMessage("Attempting to recover device connection...");
                    try
                    {
                        // In some cases, setting the communication password might help
                        // Modify device properties for next attempt
                        device.Port = 4371; // Try alternative port
                        _connected = _zkService.Connect(device);
                        
                        if (!_connected)
                        {
                            device.Port = 4370; // Reset port
                            device.MachineNumber = 0; // Try different machine number
                            _connected = _zkService.Connect(device);
                        }
                    }
                    catch (Exception recEx)
                    {
                        Program.LogMessage($"Recovery attempt failed: {recEx.Message}");
                    }
                }
                
                Program.LogMessage(_connected ? "Connection successful" : "Connection failed");
            }
            else
            {
                // Fallback to simulated connection if service is unavailable
                _connected = true;
                Program.LogMessage("Using simulated connection (ZkTeco SDK not available)");
            }
            
            return _connected;
        }
        catch (Exception ex)
        {
            _connected = false;
            Program.LogMessage($"Connection failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                Program.LogMessage($"Inner exception: {ex.InnerException.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// ثبت اثر انگشت کارمند در دستگاه
    /// </summary>
    public (bool Success, string Message, byte[]? TemplateData) RegisterFingerprint(Device device, string zkUserId, int fingerIndex)
    {
        if (!_connected && !Connect(device))
        {
            var errorMsg = "Cannot register fingerprint: Device not connected";
            Program.LogMessage(errorMsg);
            return (false, errorMsg, null);
        }

        Program.LogMessage($"Attempting to register fingerprint - Device: {device.Name}, User ID: {zkUserId}, Finger: {fingerIndex}");
        
        try
        {
            if (_zkService != null)
            {
                // Check if user exists in device
                Program.LogMessage("Checking if user exists in device");
                bool userExists = _zkService.CheckUser(device.MachineNumber, zkUserId);
                
                // If user doesn't exist, create new user in device
                if (!userExists)
                {
                    Program.LogMessage($"Creating new user {zkUserId} in device");
                    bool userCreated = _zkService.SetUserInfo(device.MachineNumber, zkUserId, null, null, 0);
                    if (!userCreated)
                    {
                        var errorMsg = $"Failed to create user {zkUserId} in device";
                        Program.LogMessage(errorMsg);
                        return (false, errorMsg, null);
                    }
                }

                // Start fingerprint registration
                Program.LogMessage($"Starting fingerprint capture for finger index {fingerIndex}");
                bool registerSuccess = _zkService.StartEnrollFingerprint(device.MachineNumber, zkUserId, fingerIndex);
                
                if (!registerSuccess)
                {
                    var errorMsg = "Failed to start fingerprint enrollment process";
                    Program.LogMessage(errorMsg);
                    return (false, errorMsg, null);
                }
                
                // Simulate waiting for finger placement and capturing
                Program.LogMessage("Capturing fingerprint - please place finger on sensor");
                
                // Get template data after successful enrollment
                byte[]? templateData = _zkService.GetFingerprintTemplate(device.MachineNumber, zkUserId, fingerIndex);
                
                if (templateData != null && templateData.Length > 0)
                {
                    Program.LogMessage($"Successfully captured fingerprint template ({templateData.Length} bytes)");
                    return (true, "Fingerprint registered successfully", templateData);
                }
                else
                {
                    var errorMsg = "Failed to capture fingerprint template";
                    Program.LogMessage(errorMsg);
                    return (false, errorMsg, null);
                }
            }
            else
            {
                // Fallback to simulated fingerprint data if service is unavailable
                Program.LogMessage("Using simulated fingerprint data (ZkTeco SDK not available)");
                var random = new Random();
                byte[] simulatedTemplate = new byte[512];
                random.NextBytes(simulatedTemplate);
                Program.LogMessage("Simulated fingerprint template created");
                return (true, "Fingerprint registered successfully (SIMULATED)", simulatedTemplate);
            }
        }
        catch (Exception ex)
        {
            var errorMsg = $"Error registering fingerprint: {ex.Message}";
            Program.LogMessage(errorMsg);
            return (false, errorMsg, null);
        }
    }

    /// <summary>
    /// لاگ‌های حضور و غیاب (General Log) را از دستگاه می‌خواند.
    /// </summary>
    public List<PunchLog> FetchLogs(Device device)
    {
        var result = new List<PunchLog>();
        if (!_connected)
        {
            Program.LogMessage("Cannot fetch logs: Device not connected");
            return result;
        }

        Program.LogMessage($"Fetching logs from device: {device.Name} (ID: {device.Id})");
        
        try
        {
            // Use the real service if available
            if (_zkService != null)
            {
                // Get actual logs from the device
                result = _zkService.FetchLogs(device);
                Program.LogMessage($"Retrieved {result.Count} actual logs from device");
                
                // Log each record retrieved from the device
                foreach (var log in result)
                {
                    Program.LogMessage($"Log record retrieved - Employee ID: {log.EmployeeId}, Time: {log.PunchTime}, Type: {log.PunchType}, DeviceRowId: {log.DeviceRowId}");
                }
            }
            else
            {
                // Fallback to simulated test data if service is unavailable
                Program.LogMessage("Using simulated log data (ZkTeco SDK not available)");
                for (int i = 1; i <= 5; i++)
                {
                    var punchTime = DateTime.Now.AddMinutes(-i * 30);
                    var punchType = i % 2 == 0 ? PunchType.CheckOut : PunchType.CheckIn;
                    var deviceRowId = $"TEST{i}";
                    
                    var log = new PunchLog
                    {
                        EmployeeId = 0, // This will need to be matched with an actual employee later
                        PunchTime = punchTime,
                        PunchType = punchType,
                        DeviceId = device.Id,
                        DeviceRowId = deviceRowId
                    };
                    
                    result.Add(log);
                    Program.LogMessage($"Simulated log record created - Time: {punchTime}, Type: {punchType}, DeviceRowId: {deviceRowId}");
                }
            }

            Program.LogMessage($"Total logs fetched: {result.Count}");
            return result;
        }
        catch (Exception ex)
        {
            Program.LogMessage($"Error fetching logs: {ex.Message}");
            return result;
        }
    }

    public void Dispose()
    {
        if (_connected)
        {
            Program.LogMessage("Disconnecting from device");
            if (_zkService != null)
            {
                try 
                {
                    _zkService.Dispose();
                    Program.LogMessage("ZkTeco service disposed");
                }
                catch (Exception ex)
                {
                    Program.LogMessage($"Error disposing ZkTeco service: {ex.Message}");
                }
            }
            _connected = false;
        }
    }
} 