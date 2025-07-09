using AttandenceDesktop.Data;
using AttandenceDesktop.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AttandenceDesktop.Services
{
    /// <summary>
    /// Service for synchronizing data between ZKTeco devices and the application database.
    /// Handles employee data synchronization, fingerprint template syncing, and more.
    /// </summary>
    public class DeviceSyncService
    {
        private readonly ZkDataExtractionService _zkDataService;
        private readonly EmployeeService? _employeeService;
        private readonly Func<ApplicationDbContext> _contextFactory;
        private readonly DataRefreshService? _dataRefreshService;

        public DeviceSyncService(
            Func<ApplicationDbContext> contextFactory,
            DataRefreshService dataRefreshService)
        {
            _zkDataService = new ZkDataExtractionService();
            _contextFactory = contextFactory;
            _dataRefreshService = dataRefreshService;
            _employeeService = null; // This will be initialized if needed
        }

        public DeviceSyncService(
            Func<ApplicationDbContext> contextFactory,
            EmployeeService employeeService)
        {
            _zkDataService = new ZkDataExtractionService();
            _employeeService = employeeService;
            _contextFactory = contextFactory;
            _dataRefreshService = null; // This will be initialized if needed
        }

        public DeviceSyncService(
            Func<ApplicationDbContext> contextFactory,
            EmployeeService employeeService,
            DataRefreshService dataRefreshService)
        {
            _zkDataService = new ZkDataExtractionService();
            _employeeService = employeeService;
            _contextFactory = contextFactory;
            _dataRefreshService = dataRefreshService;
        }

        /// <summary>
        /// Synchronizes users from a device to the Employee database
        /// </summary>
        /// <param name="device">The device to extract users from</param>
        /// <returns>A result object with success status and sync statistics</returns>
        public async Task<SyncResult> SyncUsersFromDeviceAsync(Device device)
        {
            var result = new SyncResult();

            try
            {
                Program.LogMessage($"Starting user synchronization from device: {device.Name}");

                // Extract users from device
                var usersData = await _zkDataService.GetUsersWithFingerprintsAsync(device);

                if (usersData == null || usersData.Count == 0)
                {
                    Program.LogMessage("No users found on device or extraction failed");
                    result.ErrorMessage = "No users found on device or extraction failed";
                    return result;
                }

                // Get all existing employees for comparison
                var allEmployees = await _employeeService.GetAllAsync();
                Program.LogMessage($"Found {allEmployees.Count} employees in database");

                int newCount = 0;
                int updatedCount = 0;
                int skippedCount = 0;
                int errorCount = 0;

                // Process each user from the device
                foreach (var userEntry in usersData)
                {
                    try
                    {
                        string userId = userEntry.Key;
                        dynamic userData = userEntry.Value;

                        // Extract user details from the dynamic object
                        string userName = userData.name?.ToString() ?? "";
                        string employeeId = userData.id?.ToString() ?? "";

                        Program.LogMessage($"Processing user: {employeeId} - {userName}");

                        // Try to find existing employee by ZkUserId
                        Employee existingEmployee = allEmployees.FirstOrDefault(e => e.ZkUserId == employeeId);

                        // If not found by ZkUserId, try by EmployeeNumber or EmployeeCode
                        if (existingEmployee == null)
                        {
                            existingEmployee = allEmployees.FirstOrDefault(e => 
                                e.EmployeeNumber == employeeId || e.EmployeeCode == employeeId);
                        }

                        if (existingEmployee != null)
                        {
                            // Update existing employee with device data
                            Program.LogMessage($"Updating existing employee: {existingEmployee.Id} - {existingEmployee.FullName}");

                            // Update ZkUserId if not already set
                            if (string.IsNullOrEmpty(existingEmployee.ZkUserId))
                            {
                                existingEmployee.ZkUserId = employeeId;
                            }
                            
                            // Update EmployeeNumber if not already set
                            if (string.IsNullOrEmpty(existingEmployee.EmployeeNumber))
                            {
                                existingEmployee.EmployeeNumber = employeeId;
                            }
                            
                            // Update name if needed - parse the name from device
                            string fullName = userName;
                            if (!string.IsNullOrWhiteSpace(fullName))
                            {
                                string firstName = "";
                                string lastName = "";
                                
                                // First check for period separator (e.g., "Farhad.Zaka")
                                if (fullName.Contains('.'))
                                {
                                    var nameParts = fullName.Split(new[] { '.' }, 2);
                                    firstName = nameParts[0].Trim();
                                    lastName = nameParts.Length > 1 ? nameParts[1].Trim() : "";
                                    Program.LogMessage($"Updated name from period-separated format: '{fullName}' as First: '{firstName}', Last: '{lastName}'");
                                }
                                // Then check for space separator (e.g., "Nazir Ahmad Ahmadi")
                                else
                                {
                                    var nameParts = fullName.Split(new[] { ' ' }, 2);
                                    firstName = nameParts[0].Trim();
                                    lastName = nameParts.Length > 1 ? nameParts[1].Trim() : "";
                                    Program.LogMessage($"Updated name from space-separated format: '{fullName}' as First: '{firstName}', Last: '{lastName}'");
                                }
                                
                                // Update the employee's name if we have valid values
                                if (!string.IsNullOrWhiteSpace(firstName))
                                    existingEmployee.FirstName = firstName;
                                if (!string.IsNullOrWhiteSpace(lastName))
                                    existingEmployee.LastName = lastName;
                            }

                            // Update fingerprint template if available
                            if (userData.templates != null)
                            {
                                try
                                {
                                    // Try to get templates for fingers 0 (right thumb) and 5 (left thumb)
                                    UpdateFingerprintTemplates(existingEmployee, userData.templates);
                                }
                                catch (Exception ex)
                                {
                                    Program.LogMessage($"Error extracting fingerprint templates: {ex.Message}");
                                }
                            }

                            // Update privilege information
                            try
                            {
                                int privilege = 0;
                                string privilegeDesc = "";
                                
                                // First try to get privilege directly
                                try
                                {
                                    privilege = Convert.ToInt32(userData.privilege ?? 0);
                                    privilegeDesc = userData.privilegeDescription?.ToString() ?? "";
                                    Program.LogMessage($"Extracted privilege for {existingEmployee.FullName}: {privilege}, description: {privilegeDesc}");
                                }
                                catch (Exception ex)
                                {
                                    Program.LogMessage($"Error extracting privilege directly: {ex.Message}");
                                    
                                    // Try alternative method - check if privilege is in allInfo
                                    if (userData.allInfo != null)
                                    {
                                        try
                                        {
                                            // Instead of using lambda expression with dynamic object
                                            var properties = userData.allInfo.GetType().GetProperties();
                                            var userRoleField = null as System.Reflection.PropertyInfo;
                                            
                                            foreach (var prop in properties)
                                            {
                                                if (prop.Name.Contains("UserRole"))
                                                {
                                                    userRoleField = prop;
                                                    break;
                                                }
                                            }
                                                
                                            if (userRoleField != null)
                                            {
                                                string? userRoleValue = userRoleField.GetValue(userData.allInfo)?.ToString();
                                                if (!string.IsNullOrEmpty(userRoleValue) && int.TryParse(userRoleValue, out int parsedPrivilege))
                                                {
                                                    privilege = parsedPrivilege;
                                                    privilegeDesc = privilege switch
                                                    {
                                                        0 => "user",
                                                        1 => "admin",
                                                        2 => "manager",
                                                        3 => "superadmin",
                                                        _ => $"role-{privilege}"
                                                    };
                                                    Program.LogMessage($"Extracted privilege from allInfo for {existingEmployee.FullName}: {privilege}, description: {privilegeDesc}");
                                                }
                                            }
                                        }
                                        catch (Exception innerEx)
                                        {
                                            Program.LogMessage($"Error extracting privilege from allInfo: {innerEx.Message}");
                                        }
                                    }
                                }
                                
                                // Only update if values are different
                                bool privilegeChanged = existingEmployee.Privilege != privilege || existingEmployee.PrivilegeDescription != privilegeDesc;
                                if (privilegeChanged)
                                {
                                    existingEmployee.Privilege = privilege;
                                    existingEmployee.PrivilegeDescription = privilegeDesc;
                                    Program.LogMessage($"Updated privilege for {existingEmployee.FullName}: {privilege} ({privilegeDesc})");
                                    
                                    // Update department based on privilege if privilege changed and department name is empty
                                    string departmentName = userData.department?.ToString() ?? "";
                                    if (string.IsNullOrWhiteSpace(departmentName) && !string.IsNullOrWhiteSpace(privilegeDesc))
                                    {
                                        // Format the privilege description for department name (capitalize first letter)
                                        string privilegeDeptName = char.ToUpper(privilegeDesc[0]) + privilegeDesc.Substring(1);
                                        
                                        using var dbContext = _contextFactory();
                                        // Try to find existing department by privilege name
                                        var existingDepartment = await dbContext.Departments
                                            .FirstOrDefaultAsync(d => d.Name.ToLower() == privilegeDeptName.ToLower());
                                        
                                        if (existingDepartment != null)
                                        {
                                            existingEmployee.DepartmentId = existingDepartment.Id;
                                            Program.LogMessage($"Updated department for {existingEmployee.FullName} to {privilegeDeptName} based on privilege");
                                        }
                                        else
                                        {
                                            // Create new department based on privilege
                                            var newDepartment = new Department
                                            {
                                                Name = privilegeDeptName
                                            };
                                            
                                            dbContext.Departments.Add(newDepartment);
                                            await dbContext.SaveChangesAsync();
                                            
                                            existingEmployee.DepartmentId = newDepartment.Id;
                                            Program.LogMessage($"Created new department {privilegeDeptName} and updated {existingEmployee.FullName}'s department");
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Program.LogMessage($"Error updating privilege information: {ex.Message}");
                            }

                            // Save changes
                            await _employeeService.UpdateAsync(existingEmployee);
                            updatedCount++;
                        }
                        else
                        {
                            // Consider creating a new employee record
                            // We need a minimum of name and department
                            if (!string.IsNullOrWhiteSpace(userName))
                            {
                                // Create a new employee with basic info from device
                                var newEmployee = CreateEmployeeFromDeviceData(userData, allEmployees);
                                
                                if (newEmployee != null)
                                {
                                    await _employeeService.CreateAsync(newEmployee);
                                    newCount++;
                                    Program.LogMessage($"Created new employee: {newEmployee.FullName}");
                                }
                                else
                                {
                                    // Skip due to insufficient data
                                    Program.LogMessage($"Skipped creating employee for user {employeeId} - insufficient data");
                                    skippedCount++;
                                }
                            }
                            else
                            {
                                // Skip due to missing name
                                Program.LogMessage($"Skipped creating employee for user {employeeId} - missing name");
                                skippedCount++;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Program.LogMessage($"Error processing user {userEntry.Key}: {ex.Message}");
                        errorCount++;
                    }
                }

                result.Success = true;
                result.NewRecords = newCount;
                result.UpdatedRecords = updatedCount;
                result.SkippedRecords = skippedCount;
                result.ErrorRecords = errorCount;
                result.Message = $"Sync completed: {newCount} new employees, {updatedCount} updated, {skippedCount} skipped, {errorCount} errors";
                
                Program.LogMessage(result.Message);
            }
            catch (Exception ex)
            {
                result.Success = false;
                result.ErrorMessage = $"Error during synchronization: {ex.Message}";
                Program.LogMessage(result.ErrorMessage);
            }

            return result;
        }

        /// <summary>
        /// Extracts departments from the device and updates the database
        /// </summary>
        public async Task<List<Department>> ExtractDepartmentsFromDevice(Device device)
        {
            var extractedDepartments = new List<Department>();
            
            try
            {
                // Get all users from the device with their department information
                var users = await _zkDataService.ExtractUsers(device);
                if (users == null || !users.Any())
                {
                    Program.LogMessage($"No users found on device {device.Name}");
                    return extractedDepartments;
                }
                
                Program.LogMessage($"Found {users.Count} users on device {device.Name}");
                
                // Extract unique departments from user data
                var uniqueDepartments = new HashSet<string>();
                
                foreach (var user in users)
                {
                    // First try to get department directly from user data
                    string department = user?.Department?.ToString() ?? "";
                    
                    // Check if department is a numeric code and map it to a name if needed
                    if (!string.IsNullOrWhiteSpace(department) && int.TryParse(department, out int deptCode))
                    {
                        department = MapDepartmentCodeToName(deptCode);
                        Program.LogMessage($"Mapped department code {deptCode} to name: {department} for user {user?.Name}");
                    }
                    
                    // If department is still empty, try to determine from privilege level
                    if (string.IsNullOrWhiteSpace(department))
                    {
                        int privilege = Convert.ToInt32(user?.Privilege ?? 0);
                        string privilegeDesc = user?.PrivilegeDescription?.ToString() ?? "";
                        
                        // Map privilege to department name
                        department = MapPrivilegeToDepartment(privilege, privilegeDesc);
                        Program.LogMessage($"Mapped privilege {privilege} ({privilegeDesc}) to department: {department} for user {user?.Name}");
                    }
                    
                    if (!string.IsNullOrWhiteSpace(department))
                    {
                        string deptName = department.Trim();
                        uniqueDepartments.Add(deptName);
                        Program.LogMessage($"Found department: {deptName} for user {user?.Name}");
                    }
                }
                
                Program.LogMessage($"Found {uniqueDepartments.Count} unique departments on device {device.Name}");
                
                // Get existing departments from database
                using var context = _contextFactory();
                var existingDepartments = await context.Departments.ToListAsync();
                var existingDepartmentNames = existingDepartments.Select(d => d.Name.ToLower()).ToHashSet();
                
                // Create new departments for those that don't exist
                foreach (var deptName in uniqueDepartments)
                {
                    if (!existingDepartmentNames.Contains(deptName.ToLower()))
                    {
                        var newDepartment = new Department { Name = deptName };
                        context.Departments.Add(newDepartment);
                        extractedDepartments.Add(newDepartment);
                        Program.LogMessage($"Adding new department: {deptName}");
                    }
                    else
                    {
                        // Add existing department to the result list
                        var existingDept = existingDepartments.First(d => d.Name.ToLower() == deptName.ToLower());
                        extractedDepartments.Add(existingDept);
                        Program.LogMessage($"Department already exists: {deptName}");
                    }
                }
                
                // If no departments were found in the device, create a default one
                if (uniqueDepartments.Count == 0)
                {
                    string defaultDeptName = "General";
                    if (!existingDepartmentNames.Contains(defaultDeptName.ToLower()))
                    {
                        var defaultDept = new Department { Name = defaultDeptName };
                        context.Departments.Add(defaultDept);
                        extractedDepartments.Add(defaultDept);
                        Program.LogMessage($"No departments found on device. Creating default department: {defaultDeptName}");
                    }
                    else
                    {
                        var existingDefaultDept = existingDepartments.First(d => d.Name.ToLower() == defaultDeptName.ToLower());
                        extractedDepartments.Add(existingDefaultDept);
                    }
                }
                
                // Save changes to database
                await context.SaveChangesAsync();
                
                // Notify that departments have changed
                _dataRefreshService?.NotifyDepartmentsChanged();
                Program.LogMessage($"Department synchronization completed. Added {extractedDepartments.Count} departments.");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error extracting departments: {ex.Message}");
                // Log the error
                Program.LogMessage($"Error extracting departments: {ex.Message}");
                Program.LogMessage($"Stack trace: {ex.StackTrace}");
            }
            
            return extractedDepartments;
        }
        
        /// <summary>
        /// Maps user privilege level to a department name
        /// </summary>
        private string MapPrivilegeToDepartment(int privilege, string privilegeDescription)
        {
            // First try to use the actual privilegeDescription from the device
            if (!string.IsNullOrWhiteSpace(privilegeDescription))
            {
                // Convert first letter to uppercase for consistency
                return char.ToUpper(privilegeDescription[0]) + privilegeDescription.Substring(1);
            }
            
            // If privilegeDescription is empty, fall back to numeric privilege level
            return privilege switch
            {
                0 => "User",     // Regular user
                1 => "Admin",    // Admin
                2 => "Manager",  // Manager
                3 => "SuperAdmin", // Super admin
                _ => "General"   // Default
            };
        }

        /// <summary>
        /// Maps numeric department codes to department names
        /// </summary>
        private string MapDepartmentCodeToName(int departmentCode)
        {
            // Common department code mappings in ZKTeco devices
            return departmentCode switch
            {
                1 => "Management",
                2 => "Finance",
                3 => "HR",
                4 => "IT",
                5 => "Marketing",
                6 => "Sales",
                7 => "Support",
                8 => "Production",
                9 => "Research",
                10 => "Development",
                _ => $"Department-{departmentCode}" // For unknown codes, use a prefix with the code
            };
        }

        /// <summary>
        /// Creates a new Employee record from device data
        /// </summary>
        private Employee CreateEmployeeFromDeviceData(dynamic userData, List<Employee> existingEmployees)
        {
            try
            {
                // Extract name from the device
                string fullName = userData.name?.ToString() ?? "";
                
                // Parse name - handle both period-separated and space-separated formats
                string firstName = "";
                string lastName = "";
                
                // First check for period separator (e.g., "Farhad.Zaka")
                if (fullName.Contains('.'))
                {
                    var nameParts = fullName.Split(new[] { '.' }, 2);
                    firstName = nameParts[0].Trim();
                    lastName = nameParts.Length > 1 ? nameParts[1].Trim() : "";
                    Program.LogMessage($"Parsed period-separated name: '{fullName}' as First: '{firstName}', Last: '{lastName}'");
                }
                // Then check for space separator (e.g., "Nazir Ahmad Ahmadi")
                else
                {
                    var nameParts = fullName.Split(new[] { ' ' }, 2);
                    firstName = nameParts[0].Trim();
                    lastName = nameParts.Length > 1 ? nameParts[1].Trim() : "";
                    Program.LogMessage($"Parsed space-separated name: '{fullName}' as First: '{firstName}', Last: '{lastName}'");
                }
                
                // If name is empty, use default values
                if (string.IsNullOrWhiteSpace(firstName))
                    firstName = "Unknown";
                if (string.IsNullOrWhiteSpace(lastName))
                    lastName = "User";

                // Get department information
                using var ctx = _contextFactory();
                var departments = ctx.Departments.ToList();
                
                if (departments.Count == 0)
                {
                    Program.LogMessage("Cannot create employee: No departments found in database");
                    return null;
                }
                
                // Extract privilege information
                int privilege = 0;
                string privilegeDescription = "";
                try
                {
                    privilege = Convert.ToInt32(userData.privilege ?? 0);
                    privilegeDescription = userData.privilegeDescription?.ToString() ?? "";
                    Program.LogMessage($"Extracted privilege: {privilege}, description: {privilegeDescription}");
                }
                catch (Exception ex)
                {
                    Program.LogMessage($"Error extracting privilege directly: {ex.Message}");
                    
                    // Try alternative method - check if privilege is in allInfo
                    if (userData.allInfo != null)
                    {
                        try
                        {
                            // Instead of using lambda expression with dynamic object
                            var properties = userData.allInfo.GetType().GetProperties();
                            var userRoleField = null as System.Reflection.PropertyInfo;
                            
                            foreach (var prop in properties)
                            {
                                if (prop.Name.Contains("UserRole"))
                                {
                                    userRoleField = prop;
                                    break;
                                }
                            }
                                
                            if (userRoleField != null)
                            {
                                string? userRoleValue = userRoleField.GetValue(userData.allInfo)?.ToString();
                                if (!string.IsNullOrEmpty(userRoleValue) && int.TryParse(userRoleValue, out int parsedPrivilege))
                                {
                                    privilege = parsedPrivilege;
                                    privilegeDescription = privilege switch
                                    {
                                        0 => "user",
                                        1 => "admin",
                                        2 => "manager",
                                        3 => "superadmin",
                                        _ => $"role-{privilege}"
                                    };
                                    Program.LogMessage($"Extracted privilege from allInfo: {privilege}, description: {privilegeDescription}");
                                }
                            }
                        }
                        catch (Exception innerEx)
                        {
                            Program.LogMessage($"Error extracting privilege from allInfo: {innerEx.Message}");
                        }
                    }
                }
                
                // Try to extract department information from device data
                string departmentName = "";
                int departmentId = 0;
                
                try
                {
                    departmentName = userData.department?.ToString() ?? "";
                    
                    // If department name is empty, use privilege description as department
                    if (string.IsNullOrWhiteSpace(departmentName) && !string.IsNullOrWhiteSpace(privilegeDescription))
                    {
                        // Format the privilege description for department name (capitalize first letter)
                        departmentName = char.ToUpper(privilegeDescription[0]) + privilegeDescription.Substring(1);
                        Program.LogMessage($"Using privilege description as department: {departmentName}");
                    }
                    
                    // If we have a department name, try to match it with an existing department
                    if (!string.IsNullOrWhiteSpace(departmentName))
                    {
                        var matchedDept = departments.FirstOrDefault(d => 
                            d.Name.Equals(departmentName, StringComparison.OrdinalIgnoreCase));
                            
                        if (matchedDept != null)
                        {
                            departmentId = matchedDept.Id;
                            Program.LogMessage($"Matched department '{departmentName}' to ID: {departmentId}");
                        }
                        else
                        {
                            // Create new department
                            using var dbContext = _contextFactory();
                            var newDepartment = new Department
                            {
                                Name = departmentName
                            };
                            
                            dbContext.Departments.Add(newDepartment);
                            dbContext.SaveChanges();
                            
                            departmentId = newDepartment.Id;
                            Program.LogMessage($"Created new department: {departmentName} (ID: {departmentId})");
                        }
                    }
                    else
                    {
                        // Use default department (General)
                        var defaultDept = departments.FirstOrDefault(d => d.Name == "General");
                        if (defaultDept != null)
                        {
                            departmentId = defaultDept.Id;
                            Program.LogMessage($"Using default department: General (ID: {departmentId})");
                        }
                        else
                        {
                            departmentId = departments.First().Id; // Default to first department
                            Program.LogMessage($"Using first available department (ID: {departmentId})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Program.LogMessage($"Error extracting department: {ex.Message}");
                    departmentId = departments.First().Id; // Default to first department
                }

                // Extract other potential fields
                string employeeId = userData.id?.ToString() ?? "";
                string position = "";
                string email = "";
                string phone = "";
                
                // Try to extract detailed fields if available
                if (userData.employeeDetails != null)
                {
                    try
                    {
                        position = userData.employeeDetails.position?.ToString() ?? "";
                        email = userData.employeeDetails.email?.ToString() ?? "";
                        phone = userData.employeeDetails.phone?.ToString() ?? "";
                    }
                    catch { }
                }

                // Generate a unique employee code
                string employeeCode = GenerateUniqueEmployeeCode(existingEmployees);

                // Create the new employee
                var employee = new Employee
                {
                    FirstName = firstName,
                    LastName = lastName,
                    Email = !string.IsNullOrEmpty(email) ? email : $"{firstName.ToLower()}.{lastName.ToLower()}@example.com",
                    PhoneNumber = !string.IsNullOrEmpty(phone) ? phone : "0000000000",
                    Position = !string.IsNullOrEmpty(position) ? position : "Employee",
                    EmployeeCode = employeeCode,
                    EmployeeNumber = employeeId,
                    ZkUserId = employeeId,
                    DepartmentId = departmentId,
                    HireDate = DateTime.Today,
                    Privilege = privilege,
                    PrivilegeDescription = privilegeDescription
                };

                // Add fingerprint templates if available
                if (userData.templates != null)
                {
                    UpdateFingerprintTemplates(employee, userData.templates);
                }

                return employee;
            }
            catch (Exception ex)
            {
                Program.LogMessage($"Error creating employee from device data: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Updates employee's fingerprint templates from device data
        /// </summary>
        private void UpdateFingerprintTemplates(Employee employee, dynamic templates)
        {
            try
            {
                // Check if templates is null or not a valid object
                if (templates == null)
                {
                    Program.LogMessage("No templates data provided");
                    return;
                }
                
                // Check if templates has properties
                var templateProperties = templates.GetType().GetProperties();
                if (templateProperties == null || templateProperties.Length == 0)
                {
                    Program.LogMessage("Templates object has no properties");
                    return;
                }
                
                // Map of ZKTeco finger names to finger indices
                var fingerNameToIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
                {
                    { "Left Little Finger", 0 },
                    { "Left Ring Finger", 1 },
                    { "Left Middle Finger", 2 },
                    { "Left Index Finger", 3 },
                    { "Left Thumb", 4 },
                    { "Right Thumb", 5 },
                    { "Right Index Finger", 6 },
                    { "Right Middle Finger", 7 },
                    { "Right Ring Finger", 8 },
                    { "Right Little Finger", 9 }
                };
                
                // Map of finger indices to ZKTeco finger names (for logging)
                var fingerIndexToName = fingerNameToIndex.ToDictionary(x => x.Value, x => x.Key);
                
                // Track which templates we've found
                Dictionary<int, byte[]> foundTemplates = new Dictionary<int, byte[]>();
                
                // Process all templates from the device
                foreach (var prop in templateProperties)
                {
                    try
                    {
                        string fingerName = prop.Name;
                        dynamic? templateData = null;
                        
                        try
                        {
                            templateData = prop.GetValue(templates);
                        }
                        catch (Exception ex)
                        {
                            Program.LogMessage($"Error getting template data for {fingerName}: {ex.Message}");
                            continue;
                        }
                        
                        if (templateData == null) 
                        {
                            Program.LogMessage($"Template data for {fingerName} is null");
                            continue;
                        }
                        
                        string? templateString = null;
                        try
                        {
                            // Instead of using lambda with dynamic object, use direct property access
                            if (templateData.GetType().GetProperty("templateDataSummary") != null)
                            {
                                var summaryProp = templateData.GetType().GetProperty("templateDataSummary");
                                if (summaryProp != null)
                                {
                                    var summaryValue = summaryProp.GetValue(templateData);
                                    templateString = summaryValue?.ToString();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Program.LogMessage($"Error getting template data summary for {fingerName}: {ex.Message}");
                            continue;
                        }
                        
                        if (string.IsNullOrEmpty(templateString))
                        {
                            Program.LogMessage($"Template string for {fingerName} is null or empty");
                            continue;
                        }
                        
                        // Try to convert the template string to a byte array
                        try
                        {
                            byte[] template = Convert.FromBase64String(templateString);
                            
                            // Map the finger name to the correct index
                            int fingerIndex = -1;
                            
                            // Check if the property name is one of our standard finger names
                            if (fingerNameToIndex.TryGetValue(fingerName, out int standardIndex))
                            {
                                fingerIndex = standardIndex;
                                Program.LogMessage($"Mapped standard finger name '{fingerName}' to index {fingerIndex}");
                            }
                            // Handle legacy naming from ZKTeco API (e.g., "left thumb", "right index")
                            else
                            {
                                // Try to parse the finger name
                                string normalizedName = fingerName.ToLowerInvariant();
                                
                                if (normalizedName.Contains("left") && normalizedName.Contains("little"))
                                    fingerIndex = 0;
                                else if (normalizedName.Contains("left") && normalizedName.Contains("ring"))
                                    fingerIndex = 1;
                                else if (normalizedName.Contains("left") && normalizedName.Contains("middle"))
                                    fingerIndex = 2;
                                else if (normalizedName.Contains("left") && normalizedName.Contains("index"))
                                    fingerIndex = 3;
                                else if (normalizedName.Contains("left") && normalizedName.Contains("thumb"))
                                    fingerIndex = 4;
                                else if (normalizedName.Contains("right") && normalizedName.Contains("thumb"))
                                    fingerIndex = 5;
                                else if (normalizedName.Contains("right") && normalizedName.Contains("index"))
                                    fingerIndex = 6;
                                else if (normalizedName.Contains("right") && normalizedName.Contains("middle"))
                                    fingerIndex = 7;
                                else if (normalizedName.Contains("right") && normalizedName.Contains("ring"))
                                    fingerIndex = 8;
                                else if (normalizedName.Contains("right") && normalizedName.Contains("little"))
                                    fingerIndex = 9;
                                
                                if (fingerIndex >= 0)
                                {
                                    Program.LogMessage($"Mapped legacy finger name '{fingerName}' to index {fingerIndex}");
                                }
                            }
                            
                            // If we successfully mapped the finger name to an index, store the template
                            if (fingerIndex >= 0)
                            {
                                foundTemplates[fingerIndex] = template;
                                Program.LogMessage($"Found template for finger index {fingerIndex} ({fingerIndexToName.GetValueOrDefault(fingerIndex, "Unknown")})");
                            }
                            else
                            {
                                Program.LogMessage($"Could not map finger name '{fingerName}' to a known finger index");
                            }
                        }
                        catch (Exception ex)
                        {
                            Program.LogMessage($"Error processing template for {fingerName}: {ex.Message}");
                        }
                    }
                    catch (Exception ex)
                    {
                        Program.LogMessage($"Error processing property: {ex.Message}");
                    }
                }
                
                // Now assign the templates to the employee record
                // First priority: Store thumb templates if available
                if (foundTemplates.TryGetValue(4, out byte[] leftThumb))
                {
                    employee.FingerprintTemplate1 = leftThumb;
                    Program.LogMessage("Stored Left Thumb (4) template in FingerprintTemplate1");
                }
                
                if (foundTemplates.TryGetValue(5, out byte[] rightThumb))
                {
                    employee.FingerprintTemplate2 = rightThumb;
                    Program.LogMessage("Stored Right Thumb (5) template in FingerprintTemplate2");
                }
                
                // Second priority: If no thumbs, use any other available templates
                if (employee.FingerprintTemplate1 == null)
                {
                    // Find the first available template that isn't a thumb
                    foreach (var kvp in foundTemplates.OrderBy(t => t.Key))
                    {
                        if (kvp.Key != 4 && kvp.Key != 5) // Skip thumbs as we already processed them
                        {
                            employee.FingerprintTemplate1 = kvp.Value;
                            Program.LogMessage($"Stored {fingerIndexToName[kvp.Key]} (index {kvp.Key}) template in FingerprintTemplate1 (no thumb templates available)");
                            break;
                        }
                    }
                }
                
                if (employee.FingerprintTemplate2 == null)
                {
                    // Find the next available template that isn't already stored in Template1
                    foreach (var kvp in foundTemplates.OrderBy(t => t.Key))
                    {
                        if (kvp.Key != 4 && kvp.Key != 5 && // Skip thumbs
                            (employee.FingerprintTemplate1 == null || !employee.FingerprintTemplate1.SequenceEqual(kvp.Value))) // Skip if already in Template1
                        {
                            employee.FingerprintTemplate2 = kvp.Value;
                            Program.LogMessage($"Stored {fingerIndexToName[kvp.Key]} (index {kvp.Key}) template in FingerprintTemplate2 (no thumb templates available)");
                            break;
                        }
                    }
                }
                
                // Log a summary of what we stored
                Program.LogMessage($"Template storage summary - Template1: {(employee.FingerprintTemplate1 != null ? "Filled" : "Empty")}, Template2: {(employee.FingerprintTemplate2 != null ? "Filled" : "Empty")}");
            }
            catch (Exception ex)
            {
                Program.LogMessage($"Error extracting fingerprint templates: {ex.Message}");
            }
        }

        /// <summary>
        /// Generates a unique employee code for new employees
        /// </summary>
        private string GenerateUniqueEmployeeCode(List<Employee> existingEmployees)
        {
            // Generate a code based on current date and time
            string baseCode = $"EMP{DateTime.Now:yyMMdd}";
            
            // Find highest number with this prefix
            int highestNumber = 1;
            foreach (var emp in existingEmployees)
            {
                if (emp.EmployeeCode.StartsWith(baseCode))
                {
                    string numberPart = emp.EmployeeCode.Substring(baseCode.Length);
                    if (int.TryParse(numberPart, out int number) && number >= highestNumber)
                    {
                        highestNumber = number + 1;
                    }
                }
            }

            return $"{baseCode}{highestNumber:D2}";
        }
    }

    /// <summary>
    /// Class to hold the result of a sync operation
    /// </summary>
    public class SyncResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public int NewRecords { get; set; }
        public int UpdatedRecords { get; set; }
        public int SkippedRecords { get; set; }
        public int ErrorRecords { get; set; }
    }
} 