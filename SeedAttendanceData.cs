using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using AttandenceDesktop.Data;
using AttandenceDesktop.Models;

namespace AttandenceDesktop
{
    public static class SeedAttendanceData
    {
        public static void ClearAndCreateSampleData()
        {
            try
            {
                Console.WriteLine("Starting ClearAndCreateSampleData method...");
                using (var context = ApplicationDbContext.Create())
                {
                    // Delete all existing data
                    Console.WriteLine("Clearing existing data...");
                    context.Attendances.RemoveRange(context.Attendances.ToList());
                    context.WorkCalendars.RemoveRange(context.WorkCalendars.ToList());
                    context.Employees.RemoveRange(context.Employees.ToList());
                    context.WorkSchedules.RemoveRange(context.WorkSchedules.ToList());
                    context.Departments.RemoveRange(context.Departments.ToList());
                    context.SaveChanges();
                    Console.WriteLine("Data cleared successfully.");
                    
                    // Create new data
                    Console.WriteLine("Creating new sample data...");
                    var today = DateTime.Today;
                    Console.WriteLine("Creating departments...");
                    var departments = CreateDepartments(context);
                    Console.WriteLine("Creating work schedules...");
                    var workSchedules = CreateWorkSchedules(context, departments);
                    Console.WriteLine("Creating special test case employees...");
                    var specialEmployees = CreateTestCaseEmployees(context, departments, workSchedules);
                    Console.WriteLine("Creating other employees...");
                    var regularEmployees = CreateEmployees(context, departments, workSchedules);
                    var allEmployees = regularEmployees.Concat(specialEmployees).ToList();
                    Console.WriteLine("Creating holidays...");
                    var holidays = CreateHolidays(context, today);
                    Console.WriteLine("Creating attendance records...");
                    CreateAttendanceRecords(context, regularEmployees, workSchedules, holidays, today);
                    
                    Console.WriteLine("Sample data created successfully!");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR in ClearAndCreateSampleData: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                throw; // Rethrow to let the caller handle it if needed
            }
        }
        
        // New method to create the specific test case with two employees
        private static List<Employee> CreateTestCaseEmployees(ApplicationDbContext context, List<Department> departments, List<WorkSchedule> workSchedules)
        {
            Console.WriteLine("Creating test case with two employees in the same department (one fixed, one flexible)...");
            
            // Get the IT department
            var itDepartment = departments.FirstOrDefault(d => d.Name == "IT");
            if (itDepartment == null)
            {
                Console.WriteLine("IT department not found, creating it...");
                itDepartment = new Department { Name = "IT" };
                context.Departments.Add(itDepartment);
                context.SaveChanges();
            }
            
            // Create or get schedules for the IT department
            var fixedSchedule = workSchedules.FirstOrDefault(ws => ws.DepartmentId == itDepartment.Id && !ws.IsFlexibleSchedule);
            if (fixedSchedule == null)
            {
                fixedSchedule = new WorkSchedule
                {
                    Name = "IT Standard Schedule",
                    IsFlexibleSchedule = false,
                    StartTime = new TimeSpan(8, 0, 0), // 8:00 AM
                    EndTime = new TimeSpan(17, 0, 0),   // 5:00 PM
                    TotalWorkHours = 9.0,
                    IsWorkingDayMonday = true,
                    IsWorkingDayTuesday = true,
                    IsWorkingDayWednesday = true,
                    IsWorkingDayThursday = true,
                    IsWorkingDayFriday = true,
                    IsWorkingDaySaturday = false,
                    IsWorkingDaySunday = false,
                    FlexTimeAllowanceMinutes = 15,
                    Description = "Standard IT department working hours",
                    DepartmentId = itDepartment.Id
                };
                context.WorkSchedules.Add(fixedSchedule);
                context.SaveChanges();
            }
            
            var flexibleSchedule = workSchedules.FirstOrDefault(ws => ws.DepartmentId == itDepartment.Id && ws.IsFlexibleSchedule);
            if (flexibleSchedule == null)
            {
                flexibleSchedule = new WorkSchedule
                {
                    Name = "IT Flexible Schedule",
                    IsFlexibleSchedule = true,
                    TotalWorkHours = 8.0,
                    IsWorkingDayMonday = true,
                    IsWorkingDayTuesday = true,
                    IsWorkingDayWednesday = true,
                    IsWorkingDayThursday = true,
                    IsWorkingDayFriday = true,
                    IsWorkingDaySaturday = false,
                    IsWorkingDaySunday = false,
                    FlexTimeAllowanceMinutes = 60,
                    Description = "Flexible working hours for IT department",
                    DepartmentId = itDepartment.Id
                };
                context.WorkSchedules.Add(flexibleSchedule);
                context.SaveChanges();
            }
            
            var employees = new List<Employee>
            {
                // Employee with fixed schedule
                new Employee
                {
                    FirstName = "علی",
                    LastName = "محمدی",
                    Email = "ali.mohammadi@company.com",
                    PhoneNumber = "09123456789",
                    DepartmentId = itDepartment.Id,
                    Position = "برنامه‌نویس",
                    EmployeeCode = "IT-001",
                    HireDate = DateTime.Now.AddDays(-90),
                    IsFlexibleHours = false,
                    RequiredWorkHoursPerDay = 8.0,
                    WorkScheduleId = fixedSchedule.Id
                },
                
                // Employee with flexible hours
                new Employee
                {
                    FirstName = "محمد",
                    LastName = "حسینی",
                    Email = "mohammad.hosseini@company.com",
                    PhoneNumber = "09198765432",
                    DepartmentId = itDepartment.Id,
                    Position = "برنامه‌نویس",
                    EmployeeCode = "IT-002",
                    HireDate = DateTime.Now.AddDays(-60),
                    IsFlexibleHours = true, // This employee has flexible hours
                    RequiredWorkHoursPerDay = 8.0, // Must work exactly 8 hours total
                    WorkScheduleId = flexibleSchedule.Id
                }
            };
            
            // Add employees to database
            context.Employees.AddRange(employees);
            context.SaveChanges();
            
            // Create some attendance records for these employees
            var today = DateTime.Today;
            var attendances = new List<Attendance>();
            var random = new Random();
            
            // Create attendance records for the past 30 days
            for (int day = -30; day <= 0; day++)
            {
                var date = today.AddDays(day);
                
                // Skip weekends
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Friday)
                    continue;
                
                // Fixed schedule employee - arrives on time, leaves on time
                if (day > -20) // Only the last 20 days for standard employee
                {
                    var fixedCheckIn = date.Date.Add(fixedSchedule.StartTime);
                    var fixedCheckOut = date.Date.Add(fixedSchedule.EndTime);
                    var fixedDuration = fixedCheckOut - fixedCheckIn;
                    
                    attendances.Add(new Attendance
                    {
                        EmployeeId = employees[0].Id,
                        Date = date,
                        CheckInTime = fixedCheckIn,
                        CheckOutTime = fixedCheckOut,
                        WorkDuration = fixedDuration,
                        IsComplete = true,
                        Notes = "",
                        IsLateArrival = false,
                        IsEarlyDeparture = false,
                        IsOvertime = false,
                        IsEarlyArrival = false,
                        IsFlexibleSchedule = false,
                        ExpectedWorkHours = 8.0,
                        AttendanceCode = "P"
                    });
                }
                
                // Flexible schedule employee - works 8 hours but at different times each day
                var flexibleStartHours = new[] { 9, 11, 13, 7, 10, 12, 8, 14, 6 };
                var idx = (day + 30) % flexibleStartHours.Length;
                var flexCheckIn = date.Date.AddHours(flexibleStartHours[idx]);
                
                // Vary the work duration around 8 hours (7.5 to 8.5)
                double workHours = 8.0;
                if (random.Next(100) < 30) 
                {
                    // Sometimes work a bit more or less
                    workHours = 7.5 + (random.NextDouble() * 1.0);
                }
                
                var flexCheckOut = flexCheckIn.AddHours(workHours);
                var flexDuration = TimeSpan.FromHours(workHours);
                
                // Set overtime flag if worked more than 8 hours
                bool isOvertime = workHours > 8.0;
                TimeSpan? overtimeMinutes = isOvertime ? 
                    TimeSpan.FromHours(workHours - 8.0) : null;
                
                attendances.Add(new Attendance
                {
                    EmployeeId = employees[1].Id,
                    Date = date,
                    CheckInTime = flexCheckIn,
                    CheckOutTime = flexCheckOut,
                    WorkDuration = flexDuration,
                    IsComplete = true,
                    Notes = "",
                    IsLateArrival = false, // No late arrival concept for flexible hours
                    IsEarlyDeparture = false, // No early departure concept for flexible hours
                    IsOvertime = isOvertime,
                    OvertimeMinutes = overtimeMinutes,
                    IsEarlyArrival = false,
                    IsFlexibleSchedule = true, // Very important to mark as flexible schedule
                    ExpectedWorkHours = 8.0,
                    AttendanceCode = isOvertime ? "O" : "P"
                });
            }
            
            // Add today's record for flexible employee with incomplete status (still working)
            if (DateTime.Now.DayOfWeek != DayOfWeek.Friday && DateTime.Now.DayOfWeek != DayOfWeek.Saturday)
            {
                var todayCheckIn = DateTime.Today.AddHours(10); // Started at 10 AM today
                
                attendances.Add(new Attendance
                {
                    EmployeeId = employees[1].Id,
                    Date = DateTime.Today,
                    CheckInTime = todayCheckIn,
                    CheckOutTime = null, // Still working
                    WorkDuration = null,
                    IsComplete = false,
                    Notes = "Currently working",
                    IsLateArrival = false,
                    IsEarlyDeparture = false,
                    IsOvertime = false,
                    IsEarlyArrival = false,
                    IsFlexibleSchedule = true,
                    ExpectedWorkHours = 8.0,
                    AttendanceCode = "W" // Working (not checked out)
                });
            }
            
            context.Attendances.AddRange(attendances);
            context.SaveChanges();
            Console.WriteLine($"Created {attendances.Count} attendance records for test case employees.");
            
            return employees;
        }
        
        private static List<Department> CreateDepartments(ApplicationDbContext context)
        {
            Console.WriteLine("Creating departments...");
            var departments = new List<Department>
            {
                new Department { Name = "HR" },
                new Department { Name = "IT" },
                new Department { Name = "Finance" },
                new Department { Name = "Operations" },
                new Department { Name = "Marketing" },
                new Department { Name = "Research & Development" }
            };
            
            context.Departments.AddRange(departments);
            context.SaveChanges();
            return departments;
        }
        
        private static List<WorkSchedule> CreateWorkSchedules(ApplicationDbContext context, List<Department> departments)
        {
            Console.WriteLine("Creating work schedules...");
            var workSchedules = new List<WorkSchedule>();
            
            // Standard fixed schedules for each department
            foreach (var dept in departments)
            {
                // Fixed schedule for each department
                workSchedules.Add(new WorkSchedule
                {
                    Name = $"{dept.Name} Standard Schedule",
                    IsFlexibleSchedule = false,
                    StartTime = new TimeSpan(8, 30, 0), // 8:30 AM
                    EndTime = new TimeSpan(17, 0, 0),   // 5:00 PM
                    TotalWorkHours = 8.5,
                    IsWorkingDayMonday = true,
                    IsWorkingDayTuesday = true,
                    IsWorkingDayWednesday = true,
                    IsWorkingDayThursday = true,
                    IsWorkingDayFriday = true,
                    IsWorkingDaySaturday = false,
                    IsWorkingDaySunday = false,
                    FlexTimeAllowanceMinutes = 15,
                    Description = $"Standard {dept.Name} department working hours",
                    DepartmentId = dept.Id
                });
                
                // Additionally, create a flexible schedule for IT and R&D departments
                if (dept.Name == "IT" || dept.Name == "Research & Development")
                {
                    workSchedules.Add(new WorkSchedule
                    {
                        Name = $"{dept.Name} Flexible Schedule",
                        IsFlexibleSchedule = true,
                        TotalWorkHours = 8.0,
                        IsWorkingDayMonday = true,
                        IsWorkingDayTuesday = true,
                        IsWorkingDayWednesday = true,
                        IsWorkingDayThursday = true,
                        IsWorkingDayFriday = true,
                        IsWorkingDaySaturday = false,
                        IsWorkingDaySunday = false,
                        FlexTimeAllowanceMinutes = 60, // More flexibility for flexible schedules
                        Description = $"Flexible working hours for {dept.Name} department",
                        DepartmentId = dept.Id
                    });
                }
            }
            
            // Also create some global schedules not tied to specific departments
            workSchedules.Add(new WorkSchedule
            {
                Name = "Early Shift",
                IsFlexibleSchedule = false,
                StartTime = new TimeSpan(6, 0, 0),  // 6:00 AM
                EndTime = new TimeSpan(14, 30, 0),  // 2:30 PM
                TotalWorkHours = 8.5,
                IsWorkingDayMonday = true,
                IsWorkingDayTuesday = true,
                IsWorkingDayWednesday = true,
                IsWorkingDayThursday = true,
                IsWorkingDayFriday = true,
                FlexTimeAllowanceMinutes = 10,
                Description = "Early morning shift"
            });
            
            workSchedules.Add(new WorkSchedule
            {
                Name = "Night Shift",
                IsFlexibleSchedule = false,
                StartTime = new TimeSpan(22, 0, 0), // 10:00 PM
                EndTime = new TimeSpan(6, 30, 0),   // 6:30 AM (next day)
                TotalWorkHours = 8.5,
                IsWorkingDayMonday = true,
                IsWorkingDayTuesday = true,
                IsWorkingDayWednesday = true,
                IsWorkingDayThursday = true,
                IsWorkingDaySunday = true,
                FlexTimeAllowanceMinutes = 10,
                Description = "Night shift schedule"
            });
            
            workSchedules.Add(new WorkSchedule
            {
                Name = "Weekend Shift",
                IsFlexibleSchedule = false,
                StartTime = new TimeSpan(9, 0, 0),  // 9:00 AM
                EndTime = new TimeSpan(17, 30, 0),  // 5:30 PM
                TotalWorkHours = 8.5,
                IsWorkingDayMonday = false,
                IsWorkingDayTuesday = false,
                IsWorkingDayWednesday = false,
                IsWorkingDayThursday = false,
                IsWorkingDayFriday = false,
                IsWorkingDaySaturday = true,
                IsWorkingDaySunday = true,
                FlexTimeAllowanceMinutes = 15,
                Description = "Weekend only schedule"
            });
            
            context.WorkSchedules.AddRange(workSchedules);
            context.SaveChanges();
            return workSchedules;
        }
        
        private static List<Employee> CreateEmployees(ApplicationDbContext context, List<Department> departments, List<WorkSchedule> workSchedules)
        {
            Console.WriteLine("Creating employees...");
            var rand = new Random();
            var employees = new List<Employee>();
            var positions = new Dictionary<string, List<string>>
            {
                { "HR", new List<string> { "HR Manager", "Recruiter", "HR Specialist", "Training Coordinator" } },
                { "IT", new List<string> { "Software Engineer", "System Administrator", "DevOps Engineer", "QA Engineer", "IT Manager" } },
                { "Finance", new List<string> { "Accountant", "Financial Analyst", "Payroll Specialist", "Finance Manager" } },
                { "Operations", new List<string> { "Operations Manager", "Logistics Coordinator", "Warehouse Supervisor", "Supply Chain Analyst" } },
                { "Marketing", new List<string> { "Marketing Manager", "Social Media Specialist", "Content Writer", "SEO Specialist" } },
                { "Research & Development", new List<string> { "Research Scientist", "Product Developer", "R&D Manager", "Innovation Specialist" } }
            };
            
            var firstNames = new[] { "Sara", "Mohammad", "Ali", "Fatima", "Hassan", "Maryam", "Reza", "Zahra", "Ahmad", "Leila", "Javad", "Narges", "Amir", "Fatemeh", "Hossein" };
            var lastNames = new[] { "Ahmadi", "Mohammadi", "Hosseini", "Rezaei", "Karimi", "Jafari", "Moradi", "Kazemi", "Hashemi", "Tehrani", "Akbari", "Nasiri", "Salehi", "Ebrahimi" };
            
            // Create employees for each department
            foreach (var dept in departments)
            {
                var deptPositions = positions[dept.Name];
                var deptSchedules = workSchedules.Where(ws => ws.DepartmentId == dept.Id || ws.DepartmentId == null).ToList();
                
                // Create 3-5 employees per department
                int empCount = rand.Next(3, 6);
                for (int i = 0; i < empCount; i++)
                {
                    string firstName = firstNames[rand.Next(firstNames.Length)];
                    string lastName = lastNames[rand.Next(lastNames.Length)];
                    string position = deptPositions[rand.Next(deptPositions.Count)];
                    
                    // Decide if this employee has flexible hours
                    bool isFlexible = (dept.Name == "IT" || dept.Name == "Research & Development") ? rand.Next(100) > 50 : rand.Next(100) > 80;
                    
                    // Pick an appropriate schedule
                    WorkSchedule schedule = null;
                    if (isFlexible)
                    {
                        // Try to get a flexible schedule for this department
                        schedule = deptSchedules.FirstOrDefault(ws => ws.IsFlexibleSchedule);
                        // If none exists, just use regular department schedule
                        if (schedule == null)
                            schedule = deptSchedules.FirstOrDefault(ws => ws.DepartmentId == dept.Id);
                    }
                    else
                    {
                        // Get a fixed schedule (prefer department's own schedule)
                        schedule = deptSchedules.FirstOrDefault(ws => ws.DepartmentId == dept.Id && !ws.IsFlexibleSchedule);
                        if (schedule == null)
                            schedule = deptSchedules.FirstOrDefault(ws => !ws.IsFlexibleSchedule);
                    }
                    
                    // Create the employee
                    var employee = new Employee
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        Email = $"{firstName.ToLower()}.{lastName.ToLower()}@company.com",
                        PhoneNumber = $"09{rand.Next(10000000, 99999999)}",
                        DepartmentId = dept.Id,
                        Position = position,
                        EmployeeCode = $"{dept.Name.Substring(0, 1)}{(100 + i):000}",
                        HireDate = DateTime.Now.AddDays(-rand.Next(30, 1000)),
                        IsFlexibleHours = isFlexible,
                        RequiredWorkHoursPerDay = isFlexible ? (rand.Next(4, 9) + rand.Next(0, 2) * 0.5) : 8.0
                    };
                    
                    // Assign work schedule if one was selected
                    if (schedule != null)
                    {
                        employee.WorkScheduleId = schedule.Id;
                    }
                    
                    employees.Add(employee);
                }
            }
            
            context.Employees.AddRange(employees);
            context.SaveChanges();
            return employees;
        }
        
        private static List<WorkCalendar> CreateHolidays(ApplicationDbContext context, DateTime today)
        {
            Console.WriteLine("Creating holidays and special work days...");
            var holidays = new List<WorkCalendar>();
            
            // Add some holidays in the past 30 days and upcoming month
            
            // Example: National holiday
            holidays.Add(new WorkCalendar
            {
                Date = today.AddDays(-15),
                Name = "National Holiday",
                Description = "National celebration day",
                EntryType = CalendarEntryType.Holiday,
                IsRecurringAnnually = true
            });
            
            // Example: Company event
            holidays.Add(new WorkCalendar
            {
                Date = today.AddDays(-7),
                Name = "Company Event",
                Description = "Annual company event day",
                EntryType = CalendarEntryType.NonWorkingDay,
                IsRecurringAnnually = false
            });
            
            // Example: Short working day
            holidays.Add(new WorkCalendar
            {
                Date = today.AddDays(7),
                Name = "Short Day",
                Description = "Leave early for Eid preparations",
                EntryType = CalendarEntryType.ShortDay,
                IsRecurringAnnually = false
            });
            
            // Example: Religious holiday
            holidays.Add(new WorkCalendar
            {
                Date = today.AddDays(14),
                Name = "Religious Holiday",
                Description = "Religious celebration",
                EntryType = CalendarEntryType.Holiday,
                IsRecurringAnnually = true
            });
            
            context.WorkCalendars.AddRange(holidays);
            context.SaveChanges();
            return holidays;
        }
        
        private static void CreateAttendanceRecords(ApplicationDbContext context, List<Employee> employees, List<WorkSchedule> workSchedules, List<WorkCalendar> holidays, DateTime today)
        {
            Console.WriteLine("Creating attendance records for the past 30 days...");
            var attendances = new List<Attendance>();
            var rand = new Random();
            
            // Create attendance records for all employees for the past 30 days
            foreach (var employee in employees)
            {
                var workSchedule = employee.WorkScheduleId.HasValue 
                    ? workSchedules.First(ws => ws.Id == employee.WorkScheduleId.Value) 
                    : workSchedules.First(ws => ws.DepartmentId == employee.DepartmentId);
                
                // For each day in the past 30 days
                for (int day = -30; day <= 0; day++)
                {
                    var date = today.AddDays(day);
                    
                    // Skip weekends based on work schedule
                    if (!workSchedule.IsWorkingDay(date.DayOfWeek))
                    {
                        continue;
                    }
                    
                    // Skip holidays
                    if (holidays.Any(h => h.Date.Date == date.Date && h.EntryType == CalendarEntryType.Holiday))
                    {
                        continue;
                    }
                    
                    // Check if it's a short day
                    bool isShortDay = holidays.Any(h => h.Date.Date == date.Date && h.EntryType == CalendarEntryType.ShortDay);
                    
                    // Determine if this is a flexible schedule day
                    bool isFlexibleSchedule = workSchedule.IsFlexibleSchedule || employee.IsFlexibleHours;
                    
                    // Determine start and end times
                    DateTime? checkInTime = null;
                    DateTime? checkOutTime = null;
                    TimeSpan? workDuration = null;
                    bool isComplete = true;
                    bool isLateArrival = false;
                    bool isEarlyDeparture = false;
                    bool isOvertime = false;
                    bool isEarlyArrival = false;
                    TimeSpan? lateMinutes = null;
                    TimeSpan? earlyDepartureMinutes = null;
                    TimeSpan? overtimeMinutes = null;
                    TimeSpan? earlyArrivalMinutes = null;
                    
                    double expectedWorkHours = isFlexibleSchedule 
                        ? employee.RequiredWorkHoursPerDay 
                        : (isShortDay ? (workSchedule.CalculateExpectedWorkHours(date) * 0.6) : workSchedule.CalculateExpectedWorkHours(date));
                    
                    // Occasionally skip some attendance records (absence)
                    if (rand.Next(100) < 3) // 3% chance of absence
                    {
                        // Create an absent record
                        attendances.Add(new Attendance
                        {
                            EmployeeId = employee.Id,
                            Date = date,
                            CheckInTime = null,
                            CheckOutTime = null,
                            IsComplete = false,
                            Notes = "Absent",
                            IsFlexibleSchedule = isFlexibleSchedule,
                            ExpectedWorkHours = expectedWorkHours,
                            AttendanceCode = "A" // Absent
                        });
                        continue;
                    }
                    
                    // For flexible schedules
                    if (isFlexibleSchedule)
                    {
                        // Random check-in between 7 AM and 11 AM
                        int checkInHour = rand.Next(7, 11);
                        int checkInMinute = rand.Next(0, 60);
                        checkInTime = date.Date.AddHours(checkInHour).AddMinutes(checkInMinute);
                        
                        // Calculate work hours (between 80% and 120% of required hours)
                        double actualWorkHours = employee.RequiredWorkHoursPerDay * (0.8 + (rand.NextDouble() * 0.4));
                        
                        // Occasionally create incomplete records for today
                        if (date.Date == today.Date && rand.Next(100) < 30)
                        {
                            // Only check-in time, still at work
                            checkOutTime = null;
                            isComplete = false;
                            workDuration = null;
                        }
                        else
                        {
                            // Complete day
                            checkOutTime = checkInTime.Value.AddHours(actualWorkHours);
                            workDuration = checkOutTime.Value - checkInTime.Value;
                            
                            // Determine if overtime or undertime
                            if (actualWorkHours > employee.RequiredWorkHoursPerDay * 1.05)
                            {
                                isOvertime = true;
                                overtimeMinutes = TimeSpan.FromHours(actualWorkHours - employee.RequiredWorkHoursPerDay);
                            }
                            else if (actualWorkHours < employee.RequiredWorkHoursPerDay * 0.95)
                            {
                                isEarlyDeparture = true;
                                earlyDepartureMinutes = TimeSpan.FromHours(employee.RequiredWorkHoursPerDay - actualWorkHours);
                            }
                        }
                    }
                    else // For fixed schedules
                    {
                        // Get schedule start/end times
                        var scheduleStartTime = workSchedule.StartTime;
                        var scheduleEndTime = workSchedule.EndTime;
                        
                        // If it's a short day, adjust end time
                        if (isShortDay)
                        {
                            scheduleEndTime = scheduleStartTime.Add(TimeSpan.FromHours(
                                (scheduleEndTime - scheduleStartTime).TotalHours * 0.6)); // 60% of normal day
                        }
                        
                        // Random variance in arrival time (-20 to +20 minutes from start time)
                        int arrivalVariance = rand.Next(-20, 21);
                        checkInTime = date.Date.Add(scheduleStartTime).AddMinutes(arrivalVariance);
                        
                        // Handle late arrival
                        if (arrivalVariance > workSchedule.FlexTimeAllowanceMinutes)
                        {
                            isLateArrival = true;
                            lateMinutes = TimeSpan.FromMinutes(arrivalVariance - workSchedule.FlexTimeAllowanceMinutes);
                        }
                        
                        // Handle early arrival
                        if (arrivalVariance < -workSchedule.FlexTimeAllowanceMinutes)
                        {
                            isEarlyArrival = true;
                            earlyArrivalMinutes = TimeSpan.FromMinutes(-arrivalVariance - workSchedule.FlexTimeAllowanceMinutes);
                        }
                        
                        // Occasionally create incomplete records for today
                        if (date.Date == today.Date && rand.Next(100) < 30)
                        {
                            // Only check-in time, still at work
                            checkOutTime = null;
                            isComplete = false;
                            workDuration = null;
                        }
                        else
                        {
                            // Random variance in departure time (-20 to +40 minutes from end time)
                            int departureVariance = rand.Next(-20, 41);
                            checkOutTime = date.Date.Add(scheduleEndTime).AddMinutes(departureVariance);
                            
                            // Handle early departure
                            if (departureVariance < -workSchedule.FlexTimeAllowanceMinutes)
                            {
                                isEarlyDeparture = true;
                                earlyDepartureMinutes = TimeSpan.FromMinutes(-departureVariance - workSchedule.FlexTimeAllowanceMinutes);
                            }
                            
                            // Handle overtime
                            if (departureVariance > workSchedule.FlexTimeAllowanceMinutes)
                            {
                                isOvertime = true;
                                overtimeMinutes = TimeSpan.FromMinutes(departureVariance - workSchedule.FlexTimeAllowanceMinutes);
                            }
                            
                            workDuration = checkOutTime.Value - checkInTime.Value;
                        }
                    }
                    
                    // Determine attendance code
                    string attendanceCode = DetermineAttendanceCode(isLateArrival, isEarlyDeparture, isOvertime, isEarlyArrival, isComplete);
                    
                    // Create the attendance record
                    attendances.Add(new Attendance
                    {
                        EmployeeId = employee.Id,
                        Date = date,
                        CheckInTime = checkInTime,
                        CheckOutTime = checkOutTime,
                        WorkDuration = workDuration,
                        IsComplete = isComplete,
                        Notes = GenerateRandomNote(attendanceCode),
                        IsLateArrival = isLateArrival,
                        IsEarlyDeparture = isEarlyDeparture,
                        IsOvertime = isOvertime,
                        IsEarlyArrival = isEarlyArrival,
                        LateMinutes = lateMinutes,
                        EarlyDepartureMinutes = earlyDepartureMinutes,
                        OvertimeMinutes = overtimeMinutes,
                        EarlyArrivalMinutes = earlyArrivalMinutes,
                        IsFlexibleSchedule = isFlexibleSchedule,
                        ExpectedWorkHours = expectedWorkHours,
                        AttendanceCode = attendanceCode
                    });
                }
            }
            
            context.Attendances.AddRange(attendances);
            context.SaveChanges();
        }
        
        // Helper method to determine attendance code
        private static string DetermineAttendanceCode(bool isLateArrival, bool isEarlyDeparture, bool isOvertime, bool isEarlyArrival, bool isComplete)
        {
            if (!isComplete)
                return "W"; // Working (checked in, not checked out)
            if (isLateArrival && isEarlyDeparture)
                return "LE"; // Late and Early Departure
            if (isLateArrival)
                return "L"; // Late
            if (isEarlyDeparture)
                return "E"; // Early Departure
            if (isOvertime)
                return "O"; // Overtime
            if (isEarlyArrival)
                return "EA"; // Early Arrival
            return "P"; // Present (normal)
        }
        
        // Helper method to generate random note
        private static string GenerateRandomNote(string attendanceCode)
        {
            var rand = new Random();
            
            switch (attendanceCode)
            {
                case "L":
                    var lateReasons = new[] { "Traffic delay", "Public transport issues", "Personal emergency", "Family matter", "" };
                    return lateReasons[rand.Next(lateReasons.Length)];
                    
                case "E":
                    var earlyReasons = new[] { "Doctor appointment", "Family pickup", "Personal errand", "Feeling unwell", "" };
                    return earlyReasons[rand.Next(earlyReasons.Length)];
                    
                case "O":
                    var overtimeReasons = new[] { "Project deadline", "Extra workload", "Covering for colleague", "Meeting overrun", "" };
                    return overtimeReasons[rand.Next(overtimeReasons.Length)];
                
                case "A":
                    var absentReasons = new[] { "Sick leave", "Family emergency", "Personal leave", "Approved absence" };
                    return absentReasons[rand.Next(absentReasons.Length)];
                
                default:
                    return "";
            }
        }
        
        // Legacy method for backward compatibility
        public static void CreateSampleAttendanceData()
        {
            ClearAndCreateSampleData();
        }
    }
}