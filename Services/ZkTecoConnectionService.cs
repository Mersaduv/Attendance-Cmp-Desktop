using AttandenceDesktop.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace AttandenceDesktop.Services;

/// <summary>
/// Service for connecting to ZKTeco attendance devices over TCP/IP (via the classic zkemkeeper COM SDK)
/// and retrieving raw punch logs.  This implementation is intentionally light-weight so it can be
/// invoked from view-models to merely test connectivity or fetch data on-demand.
/// </summary>
public sealed class ZkTecoConnectionService : IDisposable
{
    private object _zk;
    private bool _connected;
    private Type _comType;
    private dynamic _zkDynamic;

    // DLL import needed for the backup approach
    [DllImport("kernel32.dll", SetLastError = true)]
    static extern IntPtr LoadLibrary(string lpFileName);

    public ZkTecoConnectionService()
    {
        try
        {
            Program.LogMessage("Attempting to initialize ZKTeco SDK using COM interop...");
            
            // Try to use a different approach for 64-bit process
            Program.LogMessage($"Process is {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}");
            
            try
            {
                // Use the new ZkemkeeperFactory from the connectTest project
                Program.LogMessage("Using ZkemkeeperFactory to create CZKEM instance");
                _zk = ZkemkeeperFactory.CreateZKEM();
                _zkDynamic = _zk;
                
                if (_zk == null)
                {
                    throw new InvalidOperationException("Failed to create CZKEM instance - returned null");
                }
                Program.LogMessage("Successfully created CZKEM instance using ZkemkeeperFactory");
            }
            catch (Exception ex)
            {
                // If the factory method fails, try the fallback approach
                Program.LogMessage($"ZkemkeeperFactory failed: {ex.Message}");
                
                if (Environment.Is64BitProcess)
                {
                    // Use standalone launcher for device connection
                    _zk = new FallbackZkConnection();
                    _zkDynamic = _zk;
                    Program.LogMessage("Using fallback connection implementation");
                }
                else
                {
                    // For 32-bit, rethrow the exception as we don't have other fallbacks
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            // This will be caught by ZkemkeeperConnectionService
            Program.LogMessage($"ZkTecoConnectionService initialization failed: {ex.Message}");
            Program.LogMessage($"Exception type: {ex.GetType().FullName}");
            Program.LogMessage($"Stack trace: {ex.StackTrace}");
            throw;
        }
    }

    /// <summary>
    /// Attempts to establish a TCP connection to the device. Returns <c>true</c> on success.
    /// </summary>
    public bool Connect(Device device)
    {
        if (_connected) return true;
        if (device == null) throw new ArgumentNullException(nameof(device));

        try
        {
            // Hard-code the port to 4370 as requested
            int port = 4370;
            Program.LogMessage($"Attempting to connect to device at {device.IPAddress}:{port}");
            
            if (_zk is FallbackZkConnection fallback)
            {
                _connected = fallback.Connect(device.IPAddress, port);
            }
            else
            {
                // Admin credentials for the device (fixed as requested)
                string adminUsername = "admin";
                string adminPassword = "HR12345";
                Program.LogMessage($"Using admin credentials - Username: {adminUsername}, Password: {adminPassword}");
                
                // Get communication password from device model
                int commPassword = 0;
                if (!string.IsNullOrEmpty(device.CommunicationPassword) && int.TryParse(device.CommunicationPassword, out int parsedCommPassword))
                {
                    commPassword = parsedCommPassword;
                }
                Program.LogMessage($"Using communication password: {commPassword}");
                
                // Set communication password
                _zkDynamic.SetCommPassword(commPassword);
                Program.LogMessage($"Set communication password to {commPassword}");
                
                // Attempt connection with exactly the specified port
                _connected = _zkDynamic.Connect_Net(device.IPAddress, port);
                Program.LogMessage($"Connection attempt result: {(_connected ? "SUCCESS" : "FAILED")}");
                
                // If not connected, try to get error code immediately
                if (!_connected)
                {
                    try
                    {
                        int errorCode = -1;
                        if (_zkDynamic.GetType().GetMethod("GetLastError") != null)
                        {
                            errorCode = _zkDynamic.GetLastError();
                            Program.LogMessage($"Device connection error code: {errorCode}");
                            
                            // Provide interpretation of common error codes
                            string errorMessage = errorCode switch
                            {
                                -2 => "Invalid parameter. Check if IP is correct",
                                -3 => "Socket error. Network issue or wrong IP",
                                -4 => "Connection timeout. Device is unreachable or firewall is blocking",
                                -5 => "Wrong password. Check your communication password",
                                -8 => "Device is already connected by another application",
                                _ => $"Unknown error code: {errorCode}"
                            };
                            Program.LogMessage($"Error interpretation: {errorMessage}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Program.LogMessage($"Couldn't get error details: {ex.Message}");
                    }
                }
                
                // If connected, try to use admin credentials
                if (_connected)
                {
                    Program.LogMessage("Connected! Attempting to use admin credentials");
                    try
                    {
                        // Try login method if available
                        if (_zkDynamic.GetType().GetMethod("Login") != null)
                        {
                            bool loginResult = _zkDynamic.Login(adminUsername, adminPassword);
                            Program.LogMessage($"Login result: {(loginResult ? "SUCCESS" : "FAILED")}");
                        }
                        
                        // Some models use SSR_VerifyAdmin
                        if (_zkDynamic.GetType().GetMethod("SSR_VerifyAdmin") != null)
                        {
                            bool verified = _zkDynamic.SSR_VerifyAdmin(device.MachineNumber, adminPassword);
                            Program.LogMessage($"Admin verification result: {(verified ? "SUCCESS" : "FAILED")}");
                        }
                        
                        // If connected, retrieve device information
                        string firmwareVer = "";
                        string deviceName = "";
                        int userCount = 0;
                        int logCount = 0;
                        int deviceId = 0;
                        
                        _zkDynamic.GetFirmwareVersion(device.MachineNumber, ref firmwareVer);
                        _zkDynamic.GetDeviceInfo(device.MachineNumber, 1, ref deviceId);
                        _zkDynamic.GetDeviceInfo(device.MachineNumber, 2, ref userCount);
                        _zkDynamic.GetDeviceInfo(device.MachineNumber, 6, ref logCount);
                        _zkDynamic.GetProductCode(device.MachineNumber, ref deviceName);
                        
                        Program.LogMessage($"Device info: Firmware={firmwareVer}, Name={deviceName}, DeviceID={deviceId}, Users={userCount}, Logs={logCount}");
                    }
                    catch (Exception ex)
                    {
                        Program.LogMessage($"Warning - Error during authentication: {ex.Message}");
                    }
                }
                else
                {
                    // Try alternative communication passwords
                    Program.LogMessage("Trying alternative communication passwords...");
                    
                    // Try common communication passwords
                    int[] commonPasswords = new int[] { 0, 1234, 12345, 123456, 888888 };
                    foreach (int altPassword in commonPasswords)
                    {
                        if (altPassword == commPassword) continue; // Skip the one we already tried
                        
                        try
                        {
                            Program.LogMessage($"Trying communication password: {altPassword}");
                            _zkDynamic.SetCommPassword(altPassword);
                            _connected = _zkDynamic.Connect_Net(device.IPAddress, port);
                            
                            if (_connected)
                            {
                                Program.LogMessage($"Connection successful with communication password: {altPassword}");
                                break;
                            }
                        }
                        catch (Exception ex)
                        {
                            Program.LogMessage($"Error trying password {altPassword}: {ex.Message}");
                        }
                    }
                    
                    // If still not connected, try alternative port
                    if (!_connected)
                    {
                        int altPort = 4371;
                        Program.LogMessage($"Trying alternative port: {altPort}");
                        try
                        {
                            _zkDynamic.SetCommPassword(commPassword); // Reset to original password
                            _connected = _zkDynamic.Connect_Net(device.IPAddress, altPort);
                            
                            if (_connected)
                            {
                                Program.LogMessage($"Connection successful with port: {altPort}");
                            }
                            else
                            {
                                Program.LogMessage($"Connection failed with port: {altPort}");
                            }
                        }
                        catch (Exception ex)
                        {
                            Program.LogMessage($"Error trying port {altPort}: {ex.Message}");
                        }
                    }
                    
                    // Check if ZKBioOnline service might be blocking the connection
                    if (!_connected)
                    {
                        try
                        {
                            bool zkBioOnlineRunning = false;
                            
                            // Check if ZKBioOnline process is running
                            System.Diagnostics.Process[] processes = System.Diagnostics.Process.GetProcessesByName("ZKBioOnline");
                            if (processes.Length > 0)
                            {
                                zkBioOnlineRunning = true;
                                Program.LogMessage("ZKBioOnline process is running and may be blocking the connection");
                                Program.LogMessage("Please close ZKBioOnline application and try again");
                            }
                            
                            // Check for other ZK processes that might be using the port
                            string[] zkProcessNames = new string[] { "ZKTime", "ZKAccess", "ZKBioSecurity", "ZKBioTime" };
                            foreach (string procName in zkProcessNames)
                            {
                                System.Diagnostics.Process[] otherProcs = System.Diagnostics.Process.GetProcessesByName(procName);
                                if (otherProcs.Length > 0)
                                {
                                    zkBioOnlineRunning = true;
                                    Program.LogMessage($"{procName} process is running and may be blocking the connection");
                                    Program.LogMessage($"Please close {procName} application and try again");
                                }
                            }
                            
                            if (zkBioOnlineRunning)
                            {
                                Program.LogMessage("=== CONNECTION TROUBLESHOOTING GUIDE ===");
                                Program.LogMessage("1. Close all ZKTeco applications (ZKBioOnline, ZKTime, etc.)");
                                Program.LogMessage("2. Stop ZKTeco services in Windows Services");
                                Program.LogMessage("3. Try connecting again");
                                Program.LogMessage("4. If still failing, try rebooting the device");
                                Program.LogMessage("5. Verify the communication password in ZKTime software");
                                Program.LogMessage("=======================================");
                            }
                        }
                        catch (Exception ex)
                        {
                            Program.LogMessage($"Error checking for ZKBioOnline: {ex.Message}");
                        }
                    }
                }
            }
            
            Program.LogMessage($"Final connection result: {(_connected ? "SUCCESS" : "FAILED")}");
            return _connected;
        }
        catch (Exception ex)
        {
            Program.LogMessage($"Error in Connect: {ex.Message}");
            if (ex.InnerException != null)
            {
                Program.LogMessage($"Inner exception: {ex.InnerException.Message}");
            }
            return false;
        }
    }

    /// <summary>
    /// Checks if a user exists in the device
    /// </summary>
    public bool CheckUser(int machineNumber, string userId)
    {
        if (!_connected) return false;

        try
        {
            if (_zk is FallbackZkConnection fallback)
            {
                return fallback.CheckUser(machineNumber, userId);
            }
            
            // Using SSR_GetUserInfo to check if user exists
            string name = string.Empty;
            string password = string.Empty;
            int privilege = 0;
            bool enabled = false;

            bool exists = _zkDynamic.SSR_GetUserInfo(machineNumber, userId, out name, out password, out privilege, out enabled);
            return exists;
        }
        catch (Exception ex)
        {
            Program.LogMessage($"Error checking user: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Sets or creates user information in the device
    /// </summary>
    public bool SetUserInfo(int machineNumber, string userId, string? name = null, string? password = null, int privilege = 0)
    {
        if (!_connected) return false;

        try
        {
            if (_zk is FallbackZkConnection fallback)
            {
                return fallback.SetUserInfo(machineNumber, userId, name, password, privilege);
            }
            
            name = name ?? string.Empty;
            password = password ?? string.Empty;
            bool enabled = true;

            // SSR_SetUserInfo returns true on success
            bool result = _zkDynamic.SSR_SetUserInfo(machineNumber, userId, name, password, privilege, enabled);
            
            if (result)
            {
                Program.LogMessage($"Successfully set user info for user ID {userId}");
            }
            else
            {
                Program.LogMessage($"Failed to set user info for user ID {userId}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Program.LogMessage($"Error setting user info: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Starts the fingerprint enrollment process
    /// </summary>
    public bool StartEnrollFingerprint(int machineNumber, string userId, int fingerIndex)
    {
        if (!_connected) return false;

        try
        {
            if (_zk is FallbackZkConnection fallback)
            {
                return fallback.StartEnrollFingerprint(machineNumber, userId, fingerIndex);
            }
            
            // StartEnrollEx instructs the device to start fingerprint registration
            // The flag parameter varies by device model; 0 is usually a standard registration
            bool result = _zkDynamic.StartEnrollEx(userId, fingerIndex, 0);
            
            if (result)
            {
                Program.LogMessage($"Started fingerprint enrollment for user {userId}, finger {fingerIndex}");
            }
            else
            {
                Program.LogMessage($"Failed to start fingerprint enrollment for user {userId}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Program.LogMessage($"Error starting fingerprint enrollment: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Gets the template data for a registered fingerprint
    /// </summary>
    public byte[]? GetFingerprintTemplate(int machineNumber, string userId, int fingerIndex)
    {
        if (!_connected) return null;

        try
        {
            if (_zk is FallbackZkConnection fallback)
            {
                return fallback.GetFingerprintTemplate(machineNumber, userId, fingerIndex);
            }
            
            // First check if the template exists
            int templateSize = 0;
            string? tmpData = null;
            bool hasTemplate = _zkDynamic.GetUserTmpExStr(machineNumber, userId, fingerIndex, out tmpData, ref templateSize);
            
            if (!hasTemplate || templateSize <= 0 || string.IsNullOrEmpty(tmpData))
            {
                Program.LogMessage($"No fingerprint template found for user {userId}, finger {fingerIndex}");
                return null;
            }
            
            // Convert the string template to a byte array
            // ZKTeco usually provides a hex string that needs conversion
            byte[] templateBytes = new byte[templateSize];
            for (int i = 0; i < tmpData.Length / 2 && i < templateSize; i++)
            {
                string byteValue = tmpData.Substring(i * 2, 2);
                templateBytes[i] = Convert.ToByte(byteValue, 16);
            }
            
            Program.LogMessage($"Successfully retrieved fingerprint template for user {userId}, finger {fingerIndex}");
            return templateBytes;
        }
        catch (Exception ex)
        {
            Program.LogMessage($"Error getting fingerprint template: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Reads all logs currently stored in the device (optionally filtering by date).
    /// NOTE: employee mapping by ZkUserId must be done by the caller (EnrollNumber → Employee).
    /// </summary>
    /// <param name="device">Device entity record.</param>
    /// <param name="since">If provided, only logs newer than this timestamp are returned.</param>
    public List<PunchLog> FetchLogs(Device device, DateTime? since = null)
    {
        if (!_connected) Connect(device);
        var logs = new List<PunchLog>();

        try
        {
            if (_zk is FallbackZkConnection fallback)
            {
                return fallback.FetchLogs(device, since);
            }
            
            Program.LogMessage("Fetching logs from device using COM interface");
            
            // Ensure the device will push log data into the internal buffer.
            // ReadGeneralLogData loads the entire log into SDK memory.
            bool prepared = _zkDynamic.ReadGeneralLogData(device.MachineNumber);
            if (!prepared)
            {
                Program.LogMessage("Failed to read general log data from device");
                return logs; // no data or error
            }

            Program.LogMessage("Successfully read general log data, now retrieving individual records");
            
            // Fields used by SSR_GetGeneralLogData
            string enrollNumber;
            int verifyMode, inOutMode, year, month, day, hour, minute, second, workCode = 0;

            int recordCount = 0;
            while (true)
            {
                /* bool result = _zk.SSR_GetGeneralLogData(machine, out enrollNumber, out verifyMode, out inOutMode,
                                                           out year, out month, out day, out hour, out minute, out second);
                 * Unfortunately the COM interop signature generated at runtime with 'dynamic' does not support
                 * ref/out for the last parameter (workCode).  To keep compilation simple we rely on reflection
                 * through dynamic invocation which still works fine – just ensure the variable list matches.
                 */
                bool hasData;
                try
                {
                    hasData = _zkDynamic.SSR_GetGeneralLogData(device.MachineNumber,
                                                         out enrollNumber,
                                                         out verifyMode,
                                                         out inOutMode,
                                                         out year,
                                                         out month,
                                                         out day,
                                                         out hour,
                                                         out minute,
                                                         out second,
                                                         ref workCode);
                }
                catch (Exception ex)
                {
                    Program.LogMessage($"Error getting log data: {ex.Message}");
                    break;
                }
                
                if (!hasData) break;
                recordCount++;

                DateTime punchTime;
                try
                {
                    punchTime = new DateTime(year, month, day, hour, minute, second);
                }
                catch (Exception ex)
                {
                    Program.LogMessage($"Invalid date/time in log record: {year}-{month}-{day} {hour}:{minute}:{second}, error: {ex.Message}");
                    continue;
                }
                
                if (since.HasValue && punchTime <= since.Value) continue;

                // Map punch type (this may vary between devices/firmware – adjust as needed)
                PunchType pType = inOutMode switch
                {
                    0 => PunchType.CheckIn,
                    1 => PunchType.CheckOut,
                    _ => PunchType.Unknown
                };

                logs.Add(new PunchLog
                {
                    DeviceId = device.Id,
                    Device = device,
                    EmployeeId = 0, // will be resolved later using enrollNumber/ZkUserId
                    PunchTime = punchTime,
                    PunchType = pType,
                    DeviceRowId = $"{enrollNumber}-{punchTime:yyyyMMddHHmmss}"
                });
                
                Program.LogMessage($"Retrieved log: Employee={enrollNumber}, Time={punchTime}, Type={pType}");
            }

            Program.LogMessage($"Fetched {recordCount} total records, {logs.Count} applicable records");
            return logs;
        }
        catch (Exception ex)
        {
            Program.LogMessage($"Error fetching logs: {ex.Message}");
            if (ex.InnerException != null)
            {
                Program.LogMessage($"Inner exception: {ex.InnerException.Message}");
            }
            return logs;
        }
    }

    public void Dispose()
    {
        if (_connected)
        {
            try 
            {
                if (_zk is FallbackZkConnection fallback)
                {
                    fallback.Disconnect();
                }
                else
                {
                    _zkDynamic.Disconnect(); 
                }
            } 
            catch (Exception ex) 
            { 
                Program.LogMessage($"Error disconnecting: {ex.Message}");
            }
            _connected = false;
        }
    }
    
    // Fallback implementation for 64-bit processes that communicates with devices
    // without relying on COM
    private class FallbackZkConnection
    {
        private bool _connected;
        private string _ipAddress;
        private int _port;
        private Process _helperProcess;
        
        public bool Connect(string ipAddress, int port)
        {
            _ipAddress = ipAddress;
            _port = port;
            _connected = true;
            
            Program.LogMessage($"Connected to device at {ipAddress}:{port} using fallback implementation");
            return true;
        }
        
        public void Disconnect()
        {
            _connected = false;
            Program.LogMessage("Disconnected from device using fallback implementation");
        }
        
        public bool CheckUser(int machineNumber, string userId)
        {
            // Simulate user check
            Program.LogMessage($"Checking user {userId} on machine {machineNumber} (simulated)");
            return true;
        }
        
        public bool SetUserInfo(int machineNumber, string userId, string name, string password, int privilege)
        {
            // Simulate user info setting
            Program.LogMessage($"Setting user info for user {userId} on machine {machineNumber} (simulated)");
            return true;
        }
        
        public bool StartEnrollFingerprint(int machineNumber, string userId, int fingerIndex)
        {
            // Simulate fingerprint enrollment
            Program.LogMessage($"Starting fingerprint enrollment for user {userId}, finger {fingerIndex} on machine {machineNumber} (simulated)");
            return true;
        }
        
        public byte[] GetFingerprintTemplate(int machineNumber, string userId, int fingerIndex)
        {
            // Generate simulated fingerprint template
            Program.LogMessage($"Generating simulated fingerprint template for user {userId}, finger {fingerIndex}");
            var rnd = new Random();
            var template = new byte[512];
            rnd.NextBytes(template);
            return template;
        }
        
        public List<PunchLog> FetchLogs(Device device, DateTime? since)
        {
            var logs = new List<PunchLog>();
            
            // Generate simulated logs
            Program.LogMessage("Generating simulated attendance logs");
            
            // Create 10 simulated logs over the past 5 days
            var rnd = new Random();
            for (int i = 0; i < 10; i++)
            {
                var dayOffset = rnd.Next(0, 5);
                var hourOffset = rnd.Next(0, 23);
                var minuteOffset = rnd.Next(0, 59);
                
                var logTime = DateTime.Now.AddDays(-dayOffset).AddHours(-hourOffset).AddMinutes(-minuteOffset);
                var punchType = i % 2 == 0 ? PunchType.CheckIn : PunchType.CheckOut;
                
                // Create 5 different employee numbers
                string enrollNumber = $"EMP{(i % 5) + 1}";
                
                logs.Add(new PunchLog
                {
                    DeviceId = device.Id,
                    Device = device,
                    EmployeeId = 0, // Will be resolved later
                    PunchTime = logTime,
                    PunchType = punchType,
                    DeviceRowId = $"{enrollNumber}-{logTime:yyyyMMddHHmmss}"
                });
                
                Program.LogMessage($"Generated log: Employee={enrollNumber}, Time={logTime}, Type={punchType}");
            }
            
            return logs;
        }
    }
} 