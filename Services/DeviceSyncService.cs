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
        private readonly EmployeeService _employeeService;
        private readonly Func<ApplicationDbContext> _contextFactory;

        public DeviceSyncService(
            Func<ApplicationDbContext> contextFactory,
            EmployeeService employeeService)
        {
            _zkDataService = new ZkDataExtractionService();
            _employeeService = employeeService;
            _contextFactory = contextFactory;
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
                
                // Try to extract department information from device data
                string departmentName = "";
                int departmentId = departments.First().Id; // Default to first department
                
                try
                {
                    departmentName = userData.department?.ToString() ?? "";
                    
                    // If we have a department name from the device, try to match it with an existing department
                    if (!string.IsNullOrWhiteSpace(departmentName))
                    {
                        var matchedDept = departments.FirstOrDefault(d => 
                            d.Name.Equals(departmentName, StringComparison.OrdinalIgnoreCase));
                            
                        if (matchedDept != null)
                        {
                            departmentId = matchedDept.Id;
                            Program.LogMessage($"Matched department '{departmentName}' to ID: {departmentId}");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Program.LogMessage($"Error extracting department: {ex.Message}");
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
                    HireDate = DateTime.Today
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
            // Try to extract templates for right thumb (index 0) and left thumb (index 5)
            try
            {
                // Right thumb (finger 0)
                if (templates["Right Thumb"] != null)
                {
                    string templateData = templates["Right Thumb"].templateDataSummary?.ToString();
                    if (!string.IsNullOrEmpty(templateData))
                    {
                        // Convert template string to byte array (simplified)
                        employee.FingerprintTemplate1 = Convert.FromBase64String(templateData);
                    }
                }

                // Left thumb (finger 5)
                if (templates["Left Thumb"] != null)
                {
                    string templateData = templates["Left Thumb"].templateDataSummary?.ToString();
                    if (!string.IsNullOrEmpty(templateData))
                    {
                        // Convert template string to byte array (simplified)
                        employee.FingerprintTemplate2 = Convert.FromBase64String(templateData);
                    }
                }
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
        public string Message { get; set; }
        public string ErrorMessage { get; set; }
        public int NewRecords { get; set; }
        public int UpdatedRecords { get; set; }
        public int SkippedRecords { get; set; }
        public int ErrorRecords { get; set; }
    }
} 