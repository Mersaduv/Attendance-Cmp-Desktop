using AttandenceDesktop.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AttandenceDesktop.Services
{
    /// <summary>
    /// Service for extracting data from ZKTeco devices, including users, attendance records, and all device data.
    /// This service implements functionality similar to the /api/users, /api/all-data, and /api/attendance endpoints.
    /// </summary>
    public class ZkDataExtractionService
    {
        private readonly ZkTecoConnectionService _zkService;
        private readonly ZkemkeeperConnectionService _connectionService;

        public ZkDataExtractionService()
        {
            try
            {
                _zkService = new ZkTecoConnectionService();
                _connectionService = new ZkemkeeperConnectionService();
                Program.LogMessage("ZkDataExtractionService initialized successfully");
            }
            catch (Exception ex)
            {
                Program.LogMessage($"Error initializing ZkDataExtractionService: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Extracts users and their fingerprint data from a device (similar to /api/users endpoint)
        /// </summary>
        public async Task<Dictionary<string, object>> GetUsersWithFingerprintsAsync(Device device)
        {
            Program.LogMessage($"Retrieving users from device: {device.Name} (IP: {device.IPAddress})");
            var users = new Dictionary<string, object>();

            try
            {
                dynamic zkemDevice = ZkemkeeperFactory.CreateZKEM();
                bool connected = false;

                try
                {
                    // Set fixed port to 4370 as per the original code
                    int port = 4370;
                    connected = zkemDevice.Connect_Net(device.IPAddress, port);

                    if (!connected)
                    {
                        int errorCode = 0;
                        try { errorCode = zkemDevice.GetLastError(); } catch { }
                        Program.LogMessage($"Failed to connect to device: {device.Name}, error code: {errorCode}");
                        return users;
                    }

                    Program.LogMessage("Connected to device");

                    // Setup and prepare device for data extraction
                    zkemDevice.RegEvent(device.MachineNumber, 65535);
                    zkemDevice.EnableDevice(device.MachineNumber, false);
                    zkemDevice.RefreshData(device.MachineNumber);

                    // Read user IDs and templates
                    bool readUserSuccess = zkemDevice.ReadAllUserID(device.MachineNumber);
                    bool readTemplateSuccess = zkemDevice.ReadAllTemplate(device.MachineNumber);

                    try
                    {
                        // Set fingerprint parameters for better extraction
                        zkemDevice.SetUserInfoEx(device.MachineNumber, 1, 2);
                        zkemDevice.SetDeviceInfo(device.MachineNumber, 77, 1);
                        zkemDevice.SetDeviceInfo(device.MachineNumber, 78, 1);
                        zkemDevice.RefreshData(device.MachineNumber);
                        zkemDevice.ReadAllTemplate(device.MachineNumber);
                    }
                    catch (Exception ex)
                    {
                        Program.LogMessage($"Warning: Could not set fingerprint parameters: {ex.Message}");
                    }

                    string enrollNo, name, password;
                    int privilege;
                    bool enabled;
                    int count = 0;

                    // Extract user information
                    while (zkemDevice.SSR_GetAllUserInfo(device.MachineNumber, out enrollNo, out name, out password, out privilege, out enabled))
                    {
                        if (string.IsNullOrWhiteSpace(enrollNo) || users.ContainsKey(enrollNo))
                            continue;

                        count++;
                        Program.LogMessage($"User #{count}: {enrollNo} - {name}");

                        // Get comprehensive user information
                        var allUserInfo = GetAllUserInfo(zkemDevice, device.MachineNumber, enrollNo);
                        var templates = GetUserTemplates(zkemDevice, device.MachineNumber, enrollNo);

                        users[enrollNo] = new
                        {
                            id = enrollNo,
                            name,
                            privilege,
                            privilegeDescription = GetPrivilegeDescriptionForDisplay(privilege),
                            enabled,
                            department = TryGetUserDepartment(zkemDevice, device.MachineNumber, enrollNo),
                            cardNumber = TryGetUserCardNumber(zkemDevice, device.MachineNumber, enrollNo),
                            hireDate = TryGetUserHireDate(zkemDevice, device.MachineNumber, enrollNo),
                            templates = templates,
                            allInfo = allUserInfo,
                            employeeDetails = new
                            {
                                birthday = GetSpecificField(allUserInfo, "Birthday (15)"),
                                gender = GetSpecificField(allUserInfo, "Gender (26)"),
                                address = GetSpecificField(allUserInfo, "Address (16)"),
                                phone = GetSpecificField(allUserInfo, "Phone (17)"),
                                email = GetSpecificField(allUserInfo, "Email (18)"),
                                position = GetSpecificField(allUserInfo, "Position (19)"),
                                employeeId = GetSpecificField(allUserInfo, "EmployeeID (27)"),
                                creationDate = GetSpecificField(allUserInfo, "CreationDate (30)")
                            }
                        };
                    }
                }
                catch (Exception ex)
                {
                    Program.LogMessage($"Error accessing device: {ex.Message}");
                }
                finally
                {
                    if (connected)
                    {
                        try { zkemDevice.EnableDevice(device.MachineNumber, true); } catch { }
                        try { zkemDevice.Disconnect(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogMessage($"COM error: {ex.Message}");
            }

            Program.LogMessage($"Retrieved {users.Count} users from device");
            return users;
        }

        /// <summary>
        /// Gets all data from a device including users, templates and logs (similar to /api/all-data endpoint)
        /// </summary>
        public async Task<Dictionary<string, object>> GetAllDeviceDataAsync(Device device)
        {
            Program.LogMessage($"Retrieving all data from device: {device.Name} (IP: {device.IPAddress})");
            var result = new Dictionary<string, object>();
            
            try
            {
                dynamic zkemDevice = ZkemkeeperFactory.CreateZKEM();
                bool connected = false;

                try
                {
                    int port = 4370;
                    connected = zkemDevice.Connect_Net(device.IPAddress, port);

                    if (!connected)
                    {
                        int errorCode = 0;
                        try { errorCode = zkemDevice.GetLastError(); } catch { }
                        Program.LogMessage($"Failed to connect to device: {device.Name}, error code: {errorCode}");
                        return result;
                    }

                    Program.LogMessage("Connected to device");
                    
                    // Disable device for consistent data access
                    zkemDevice.EnableDevice(device.MachineNumber, false);
                    zkemDevice.RegEvent(device.MachineNumber, 65535);
                    zkemDevice.RefreshData(device.MachineNumber);
                    
                    // --- PART 1: USER DATA ---
                    bool readUserSuccess = zkemDevice.ReadAllUserID(device.MachineNumber);
                    bool readTemplateSuccess = zkemDevice.ReadAllTemplate(device.MachineNumber);
                    
                    var users = new Dictionary<string, object>();
                    string enrollNo, name, password;
                    int privilege;
                    bool enabled;
                    int userCount = 0;

                    while (zkemDevice.SSR_GetAllUserInfo(device.MachineNumber, out enrollNo, out name, out password, out privilege, out enabled))
                    {
                        if (string.IsNullOrWhiteSpace(enrollNo) || users.ContainsKey(enrollNo))
                            continue;

                        userCount++;
                        
                        // Get all user information
                        var allUserInfo = GetAllUserInfo(zkemDevice, device.MachineNumber, enrollNo);
                        var templates = GetUserTemplates(zkemDevice, device.MachineNumber, enrollNo);
                        
                        users[enrollNo] = new
                        {
                            id = enrollNo,
                            name,
                            privilege,
                            privilegeDescription = GetPrivilegeDescriptionForDisplay(privilege),
                            enabled,
                            department = TryGetUserDepartment(zkemDevice, device.MachineNumber, enrollNo),
                            cardNumber = TryGetUserCardNumber(zkemDevice, device.MachineNumber, enrollNo),
                            hireDate = TryGetUserHireDate(zkemDevice, device.MachineNumber, enrollNo),
                            allInfo = allUserInfo,
                            templates = templates,
                            employeeDetails = new
                            {
                                birthday = GetSpecificField(allUserInfo, "Birthday (15)"),
                                gender = GetSpecificField(allUserInfo, "Gender (26)"),
                                address = GetSpecificField(allUserInfo, "Address (16)"),
                                phone = GetSpecificField(allUserInfo, "Phone (17)"),
                                email = GetSpecificField(allUserInfo, "Email (18)"),
                                position = GetSpecificField(allUserInfo, "Position (19)"),
                                employeeId = GetSpecificField(allUserInfo, "EmployeeID (27)"),
                                creationDate = GetSpecificField(allUserInfo, "CreationDate (30)")
                            }
                        };
                    }

                    // --- PART 2: ATTENDANCE LOGS ---
                    zkemDevice.ReadGeneralLogData(device.MachineNumber);
                    
                    var logs = new List<object>();
                    
                    string sdwEnrollNumber = "";
                    int idwVerifyMode = 0, idwInOutMode = 0;
                    int idwYear = 0, idwMonth = 0, idwDay = 0, idwHour = 0, idwMinute = 0, idwSecond = 0;
                    int idwWorkcode = 0;

                    while (zkemDevice.SSR_GetGeneralLogData(device.MachineNumber, out sdwEnrollNumber, out idwVerifyMode,
                           out idwInOutMode, out idwYear, out idwMonth, out idwDay, out idwHour,
                           out idwMinute, out idwSecond, ref idwWorkcode))
                    {
                        try
                        {
                            DateTime logTime = new DateTime(idwYear, idwMonth, idwDay, idwHour, idwMinute, idwSecond);
                            
                            logs.Add(new
                            {
                                userId = sdwEnrollNumber,
                                dateTime = logTime,
                                dateTimeString = logTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                date = logTime.ToString("yyyy-MM-dd"),
                                time = logTime.ToString("HH:mm:ss"),
                                verifyMode = idwVerifyMode,
                                verifyModeDescription = GetVerifyModeDescriptionForDisplay(idwVerifyMode),
                                inOutMode = idwInOutMode,
                                inOutModeDescription = GetLogTypeDescriptionForDisplay(idwInOutMode),
                                workCode = idwWorkcode
                            });
                        }
                        catch (Exception logEx)
                        {
                            Program.LogMessage($"Error processing log: {logEx.Message}");
                        }
                    }

                    // --- PART 3: DEVICE INFO ---
                    var deviceInfo = GetDeviceInfo(zkemDevice, device.MachineNumber);

                    // Compile results
                    result["device"] = new 
                    { 
                        ipAddress = device.IPAddress, 
                        port = 4370, 
                        machineNumber = device.MachineNumber,
                        info = deviceInfo
                    };
                    
                    result["statistics"] = new
                    {
                        totalUsers = users.Count,
                        totalLogs = logs.Count
                    };
                    
                    result["users"] = users.Values;
                    result["logs"] = logs;
                    result["success"] = true;
                }
                catch (Exception ex)
                {
                    Program.LogMessage($"Error accessing device: {ex.Message}");
                    result["success"] = false;
                    result["error"] = ex.Message;
                }
                finally
                {
                    if (connected)
                    {
                        try { zkemDevice.EnableDevice(device.MachineNumber, true); } catch { }
                        try { zkemDevice.Disconnect(); } catch { }
                        Program.LogMessage("Device re-enabled and disconnected");
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogMessage($"COM error: {ex.Message}");
                result["success"] = false;
                result["error"] = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Gets attendance logs for the last three days (similar to /api/attendance endpoint)
        /// </summary>
        public async Task<Dictionary<string, object>> GetAttendanceLogsAsync(Device device)
        {
            Program.LogMessage($"Retrieving attendance logs from device: {device.Name} (IP: {device.IPAddress})");
            var result = new Dictionary<string, object>();
            
            // Fixed date range: today and the previous two days
            DateTime endDate = DateTime.Today;
            DateTime startDate = endDate.AddDays(-2); // Covers the last three days inclusive

            try
            {
                dynamic zkemDevice = ZkemkeeperFactory.CreateZKEM();
                bool connected = false;

                try
                {
                    connected = zkemDevice.Connect_Net(device.IPAddress, 4370);
                    if (!connected)
                    {
                        int errorCode = 0;
                        try { errorCode = zkemDevice.GetLastError(); } catch { }
                        
                        Program.LogMessage($"Failed to connect to device at {device.IPAddress}, error code: {errorCode}");
                        result["success"] = false;
                        result["error"] = $"Failed to connect, error code: {errorCode}";
                        return result;
                    }

                    // Disable the device to get consistent data
                    zkemDevice.EnableDevice(device.MachineNumber, false);

                    // Prepare to read all general log data
                    zkemDevice.ReadGeneralLogData(device.MachineNumber);

                    var logs = new List<object>();

                    string sdwEnrollNumber = "";
                    int idwVerifyMode = 0, idwInOutMode = 0;
                    int idwYear = 0, idwMonth = 0, idwDay = 0, idwHour = 0, idwMinute = 0, idwSecond = 0;
                    int idwWorkcode = 0;

                    while (zkemDevice.SSR_GetGeneralLogData(device.MachineNumber, out sdwEnrollNumber, out idwVerifyMode,
                           out idwInOutMode, out idwYear, out idwMonth, out idwDay, out idwHour,
                           out idwMinute, out idwSecond, ref idwWorkcode))
                    {
                        try
                        {
                            DateTime logTime = new DateTime(idwYear, idwMonth, idwDay, idwHour, idwMinute, idwSecond);
                            if (logTime >= startDate && logTime <= endDate.AddDays(1).AddSeconds(-1))
                            {
                                logs.Add(new
                                {
                                    userId = sdwEnrollNumber,
                                    dateTime = logTime,
                                    dateTimeString = logTime.ToString("yyyy-MM-dd HH:mm:ss"),
                                    date = logTime.ToString("yyyy-MM-dd"),
                                    time = logTime.ToString("HH:mm:ss"),
                                    verifyMode = idwVerifyMode,
                                    verifyModeDescription = GetVerifyModeDescriptionForDisplay(idwVerifyMode),
                                    inOutMode = idwInOutMode,
                                    inOutModeDescription = GetLogTypeDescriptionForDisplay(idwInOutMode),
                                    workCode = idwWorkcode
                                });
                            }
                        }
                        catch (Exception logEx)
                        {
                            Program.LogMessage($"Error processing log: {logEx.Message}");
                        }
                    }

                    // Compile results
                    result["success"] = true;
                    result["dateRange"] = new { start = startDate.ToString("yyyy-MM-dd"), end = endDate.ToString("yyyy-MM-dd") };
                    result["totalLogs"] = logs.Count;
                    result["logs"] = logs;
                }
                catch (Exception ex)
                {
                    Program.LogMessage($"Error accessing device: {ex.Message}");
                    result["success"] = false;
                    result["error"] = ex.Message;
                }
                finally
                {
                    if (connected)
                    {
                        try { zkemDevice.EnableDevice(device.MachineNumber, true); } catch { }
                        try { zkemDevice.Disconnect(); } catch { }
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogMessage($"COM error: {ex.Message}");
                result["success"] = false;
                result["error"] = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// Extracts users with their department information
        /// </summary>
        public async Task<List<dynamic>> ExtractUsers(Device device)
        {
            Program.LogMessage($"Extracting users from device: {device.Name} (IP: {device.IPAddress})");
            var result = new List<dynamic>();

            try
            {
                // Get all users with their data
                var usersData = await GetUsersWithFingerprintsAsync(device);
                if (usersData == null || !usersData.Any())
                {
                    Program.LogMessage($"No users found on device {device.Name}");
                    return result;
                }

                Program.LogMessage($"Found {usersData.Count} users on device {device.Name}");
                
                // Extract user information with departments
                foreach (var userEntry in usersData)
                {
                    try
                    {
                        dynamic userData = userEntry.Value;
                        string userId = userData?.id?.ToString() ?? "";
                        string name = userData?.name?.ToString() ?? "";
                        string department = userData?.department?.ToString() ?? "";
                        int privilege = Convert.ToInt32(userData?.privilege ?? 0);
                        string privilegeDescription = userData?.privilegeDescription?.ToString() ?? "user";
                        
                        // Create a dynamic object with user and department info
                        result.Add(new
                        {
                            UserId = userId,
                            Name = name,
                            Department = department,
                            Privilege = privilege,
                            PrivilegeDescription = privilegeDescription,
                            Templates = userData?.templates
                        });
                        
                        Program.LogMessage($"Extracted user: {userId}, Name: {name}, Department: {department}, Privilege: {privilege} ({privilegeDescription})");
                    }
                    catch (Exception ex)
                    {
                        Program.LogMessage($"Error extracting user data: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Program.LogMessage($"Error extracting users from device {device.Name}: {ex.Message}");
            }

            return result;
        }

        #region Helper Methods

        // Helper method for getting user information from device
        private static Dictionary<string, string> GetAllUserInfo(dynamic zkemDevice, int machineNumber, string userId)
        {
            var userInfo = new Dictionary<string, string>();
            
            try
            {
                // Extract user information from all possible fields (1-40)
                for (int field = 1; field <= 40; field++)
                {
                    try
                    {
                        int value = 0;
                        bool result = zkemDevice.GetUserInfoEx(machineNumber, userId, field, ref value);
                        if (result)
                        {
                            string fieldName = GetFieldName(field);
                            userInfo.Add($"{fieldName} ({field})", value.ToString());
                        }
                    }
                    catch { }
                }
                
                // Read additional user information with alternative methods
                try 
                {
                    string password = "", cardNumber = "";
                    int privilege = 0;
                    bool enabled = false;
                    
                    bool result = zkemDevice.SSR_GetUserInfo(machineNumber, userId, out cardNumber, out password, out privilege, out enabled);
                    
                    if (result)
                    {
                        if (!string.IsNullOrEmpty(password))
                            userInfo.Add("Password", password);
                            
                        if (!string.IsNullOrEmpty(cardNumber) && cardNumber != userId)
                            userInfo.Add("Card", cardNumber);
                    }
                }
                catch { }
            }
            catch { }
            
            return userInfo;
        }

        // Helper method to get user templates (fingerprints)
        private static Dictionary<string, object> GetUserTemplates(dynamic zkemDevice, int machineNumber, string userId)
        {
            var templates = new Dictionary<string, object>();
            
            try
            {
                // Try to get fingerprint templates
                for (int fingerIndex = 0; fingerIndex < 10; fingerIndex++)
                {
                    int flag = 1; // Flag for template format
                    string templateData = "";
                    int templateLength = 0;
                    
                    try
                    {
                        bool result = zkemDevice.GetUserTmpExStr(machineNumber, userId, fingerIndex, out flag, out templateData, out templateLength);
                        
                        if (result && !string.IsNullOrEmpty(templateData) && templateLength > 0)
                        {
                            string fingerName = GetFingerName(fingerIndex);
                            templates[fingerName] = new
                            {
                                index = fingerIndex,
                                name = fingerName,
                                type = GetFingerType(fingerIndex),
                                hand = fingerIndex < 5 ? "right" : "left",
                                flag = flag,
                                length = templateLength,
                                // Truncate template data if too large
                                templateDataSummary = templateData.Length > 50 ? 
                                    $"{templateData.Substring(0, 20)}...{templateData.Substring(templateData.Length - 20)}" : 
                                    templateData
                            };
                        }
                    }
                    catch { }
                }
                
                // Try to get face templates if available
                try
                {
                    int faceFlag = 0;
                    string faceTemplate = "";
                    int faceLength = 0;
                    
                    bool hasFace = false;
                    
                    try
                    {
                        hasFace = zkemDevice.GetUserFaceStr(machineNumber, userId, 50, out faceFlag, out faceTemplate, out faceLength);
                    }
                    catch { }
                    
                    if (hasFace && !string.IsNullOrEmpty(faceTemplate) && faceLength > 0)
                    {
                        templates["face"] = new
                        {
                            flag = faceFlag,
                            length = faceLength,
                            hasTemplate = true
                        };
                    }
                }
                catch { }
            }
            catch { }
            
            return templates;
        }

        // Helper method to get device information
        private static Dictionary<string, object> GetDeviceInfo(dynamic zkemDevice, int machineNumber)
        {
            var info = new Dictionary<string, object>();
            
            try
            {
                // Get device version
                string version = "";
                try
                {
                    if (zkemDevice.GetFirmwareVersion(machineNumber, ref version))
                    {
                        info["firmwareVersion"] = version;
                    }
                }
                catch { }
                
                // Get device name
                string deviceName = "";
                try
                {
                    if (zkemDevice.GetDeviceName(machineNumber, ref deviceName))
                    {
                        info["deviceName"] = deviceName;
                    }
                }
                catch { }
                
                // Get serial number
                string serialNumber = "";
                try
                {
                    if (zkemDevice.GetSerialNumber(machineNumber, out serialNumber))
                    {
                        info["serialNumber"] = serialNumber;
                    }
                }
                catch { }
                
                // Get product code
                string productCode = "";
                try
                {
                    if (zkemDevice.GetProductCode(machineNumber, out productCode))
                    {
                        info["productCode"] = productCode;
                    }
                }
                catch { }
                
                // Get capacity info
                int userCount = 0, fpCount = 0, recordCount = 0;
                try
                {
                    if (zkemDevice.GetDeviceStatus(machineNumber, 2, ref userCount))
                    {
                        info["userCapacity"] = userCount;
                    }
                    
                    if (zkemDevice.GetDeviceStatus(machineNumber, 1, ref fpCount))
                    {
                        info["fingerprintCapacity"] = fpCount;
                    }
                    
                    if (zkemDevice.GetDeviceStatus(machineNumber, 6, ref recordCount))
                    {
                        info["recordCapacity"] = recordCount;
                    }
                }
                catch { }
            }
            catch { }
            
            return info;
        }

        // Helper method for finger name
        private static string GetFingerName(int index)
        {
            return index switch
            {
                0 => "Left Little Finger",
                1 => "Left Ring Finger",
                2 => "Left Middle Finger",
                3 => "Left Index Finger",
                4 => "Left Thumb",
                5 => "Right Thumb",
                6 => "Right Index Finger",
                7 => "Right Middle Finger",
                8 => "Right Ring Finger",
                9 => "Right Little Finger",
                _ => $"Unknown Finger ({index})"
            };
        }

        // Helper method for finger type
        private static string GetFingerType(int fingerIndex)
        {
            return fingerIndex switch
            {
                0 => "Little",
                1 => "Ring",
                2 => "Middle",
                3 => "Index",
                4 => "Thumb",
                5 => "Thumb",
                6 => "Index",
                7 => "Middle",
                8 => "Ring",
                9 => "Little",
                _ => $"Unknown"
            };
        }

        // Helper method for log type description
        private static string GetLogTypeDescriptionForDisplay(int logType)
        {
            return logType switch
            {
                0 => "Check In",
                1 => "Check Out",
                2 => "Break Out",
                3 => "Break In",
                4 => "Overtime In",
                5 => "Overtime Out",
                _ => $"Unknown ({logType})"
            };
        }

        // Helper method for verify mode description
        private static string GetVerifyModeDescriptionForDisplay(int verifyMode)
        {
            return verifyMode switch
            {
                0 => "Password",
                1 => "Fingerprint",
                2 => "Card",
                3 => "Face",
                4 => "Palm",
                _ => $"Other ({verifyMode})"
            };
        }

        // Helper method for privilege description
        private static string GetPrivilegeDescriptionForDisplay(int privilege)
        {
            return privilege switch
            {
                0 => "user",
                1 => "admin",
                2 => "manager",
                3 => "superadmin",
                _ => $"unknown ({privilege})"
            };
        }

        // Helper method for field names
        private static string GetFieldName(int field)
        {
            return field switch
            {
                1 => "UserRole",
                2 => "VerificationType",
                3 => "EnableUser",
                4 => "PasswordLevel",
                5 => "FPCount",
                6 => "CardNumber",
                7 => "Group",
                8 => "TimeGroup",
                9 => "PIN",
                10 => "FaceCount",
                11 => "PalmCount",
                12 => "VeinCount",
                13 => "Department",
                14 => "HireDate",
                15 => "Birthday",
                16 => "Address", 
                17 => "Phone",
                18 => "Email",
                19 => "Position",
                20 => "PhotoCount",
                21 => "CustomField1", 
                22 => "CustomField2",
                23 => "CustomField3",
                24 => "CustomField4",
                25 => "CustomField5",
                26 => "Gender",
                27 => "EmployeeID",
                28 => "LastVerified",
                29 => "LastModified",
                30 => "CreationDate",
                _ => $"Field{field}"
            };
        }

        // Helper method for getting a specific field from user info
        private static string GetSpecificField(Dictionary<string, string> userInfo, string fieldKey)
        {
            if (userInfo != null && userInfo.ContainsKey(fieldKey))
            {
                return userInfo[fieldKey];
            }
            return "";
        }

        // Helper method for department extraction
        private static string TryGetUserDepartment(dynamic zkemDevice, int machineNumber, string userId)
        {
            try
            {
                string departmentName = "";
                
                try
                {
                    int departmentId = 0;
                    bool result = zkemDevice.GetUserInfoEx(machineNumber, userId, 13, ref departmentId);
                    if (result && departmentId > 0)
                    {
                        departmentName = departmentId switch
                        {
                            1 => "admin",
                            2 => "finance",
                            3 => "hr",
                            4 => "technical",
                            5 => "marketing",
                            6 => "sales",
                            7 => "support",
                            8 => "production",
                            9 => "research and development",
                            10 => "management",
                            _ => $"department {departmentId}"
                        };
                    }
                }
                catch
                {
                    try
                    {
                        int departmentId = 0;
                        string outString1 = "", outString2 = "";
                        int outInt1 = 0;
                        bool outBool1 = false;
                        bool result = zkemDevice.SSR_GetUserInfo(machineNumber, userId, out outString1, out outString2, out outInt1, out outBool1, out departmentId);
                        if (result && departmentId > 0)
                        {
                            departmentName = $"department {departmentId}";
                        }
                    }
                    catch { }
                }
                
                return departmentName;
            }
            catch
            {
                return "";
            }
        }

        // Helper method for card number extraction
        private static string TryGetUserCardNumber(dynamic zkemDevice, int machineNumber, string userId)
        {
            try
            {
                string cardNumber = "";
                
                try
                {
                    int cardValue = 0;
                    bool result = zkemDevice.GetStrCardNumber(out cardValue);
                    if (result && cardValue > 0)
                    {
                        cardNumber = cardValue.ToString();
                    }
                }
                catch
                {
                    try
                    {
                        string password = "";
                        int privilege = 0;
                        bool enabled = false;
                        
                        bool result = zkemDevice.SSR_GetUserInfo(machineNumber, userId, out cardNumber, out password, out privilege, out enabled);
                        
                        if (cardNumber == userId)
                        {
                            cardNumber = "";
                        }
                    }
                    catch { }
                }
                
                return !string.IsNullOrEmpty(cardNumber) ? cardNumber : "";
            }
            catch
            {
                return "";
            }
        }

        // Helper method for hire date extraction
        private static string TryGetUserHireDate(dynamic zkemDevice, int machineNumber, string userId)
        {
            try
            {
                try 
                {
                    int hireDate = 0;
                    bool result = zkemDevice.GetUserInfoEx(machineNumber, userId, 14, ref hireDate);
                    if (result && hireDate > 0)
                    {
                        try
                        {
                            int year = hireDate / 10000;
                            int month = (hireDate % 10000) / 100;
                            int day = hireDate % 100;
                            
                            if (year > 0 && month > 0 && day > 0 && month <= 12 && day <= 31)
                            {
                                return $"{year:0000}/{month:00}/{day:00}";
                            }
                        }
                        catch { }
                    }
                }
                catch { }
                
                try 
                {
                    int hireDate = 0;
                    bool result = zkemDevice.GetUserInfoEx(machineNumber, userId, 29, ref hireDate);
                    if (result && hireDate > 0)
                    {
                        return $"{hireDate}"; // Raw code for verification
                    }
                }
                catch { }
                
                return "";
            }
            catch
            {
                return "";
            }
        }
        #endregion
    }
} 