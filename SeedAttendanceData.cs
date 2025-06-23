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
            Console.WriteLine("Clearing existing data and creating new sample data...");
            
            // Create a new database context
            var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
            var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TimeAttendance.db");
            var connectionString = $"Data Source={dbPath}";
            optionsBuilder.UseSqlite(connectionString);
            
            // Update the database schema first
            DatabaseUpdater.UpdateDatabase();
            
            using (var context = new ApplicationDbContext(optionsBuilder.Options))
            {
                // Clear ALL existing data
                Console.WriteLine("Clearing all existing data...");
                context.Attendances.RemoveRange(context.Attendances.ToList());
                context.Employees.RemoveRange(context.Employees.ToList());
                context.WorkSchedules.RemoveRange(context.WorkSchedules.ToList());
                context.WorkCalendars.RemoveRange(context.WorkCalendars.ToList());
                context.Departments.RemoveRange(context.Departments.ToList());
                context.SaveChanges();
                
                // 1. Create departments
                Console.WriteLine("Creating departments...");
                var departments = CreateDepartments(context);
                
                // 2. Create different work schedules
                Console.WriteLine("Creating work schedules...");
                var workSchedules = CreateWorkSchedules(context);
                
                // 3. Create employees with different work schedules
                Console.WriteLine("Creating employees...");
                var employees = CreateEmployees(context, departments, workSchedules);
                
                // 4. Create work calendar (holidays)
                var today = DateTime.Today;
                var holidays = CreateHolidays(context, today);
                
                // 5. Create attendance records for each employee
                Console.WriteLine("Creating sample attendance records...");
                var startDate = today.AddDays(-30);
                
                foreach (var employee in employees)
                {
                    var workSchedule = workSchedules.FirstOrDefault(ws => ws.Id == employee.WorkScheduleId);
                    if (workSchedule != null)
                    {
                        CreateAttendanceRecords(
                            context, 
                            employee.Id, 
                            startDate, 
                            today, 
                            workSchedule,
                            holidays,
                            presentFrequency: 0.6,
                            absentFrequency: 0.05,
                            lateArrivalFrequency: 0.1,
                            earlyDepartureFrequency: 0.1,
                            overtimeFrequency: 0.05,
                            earlyArrivalFrequency: 0.1
                        );
                    }
                }
                
                Console.WriteLine("Sample data created successfully.");
            }
        }
        
        private static List<Department> CreateDepartments(ApplicationDbContext context)
        {
            var departments = new List<Department>
            {
                new Department { Name = "مالی" },
                new Department { Name = "فناوری اطلاعات" },
                new Department { Name = "منابع انسانی" },
                new Department { Name = "بازاریابی" },
                new Department { Name = "عملیات" },
                new Department { Name = "پشتیبانی فنی" },
                new Department { Name = "تحقیق و توسعه" }
            };
            
            context.Departments.AddRange(departments);
            context.SaveChanges();
            return departments;
        }
        
        private static List<WorkSchedule> CreateWorkSchedules(ApplicationDbContext context)
        {
            var workSchedules = new List<WorkSchedule>
            {
                new WorkSchedule
                {
                    Name = "برنامه کاری عادی",
                    StartTime = new TimeSpan(9, 0, 0), // 9:00 AM
                    EndTime = new TimeSpan(17, 0, 0),  // 5:00 PM
                    IsWorkingDaySunday = false,
                    IsWorkingDayMonday = true,
                    IsWorkingDayTuesday = true,
                    IsWorkingDayWednesday = true,
                    IsWorkingDayThursday = true,
                    IsWorkingDayFriday = true,
                    IsWorkingDaySaturday = false,
                    FlexTimeAllowanceMinutes = 15,
                    Description = "برنامه کاری استاندارد 9 تا 5"
                },
                new WorkSchedule
                {
                    Name = "برنامه کاری انعطاف‌پذیر",
                    StartTime = new TimeSpan(8, 0, 0), // 8:00 AM
                    EndTime = new TimeSpan(16, 0, 0),  // 4:00 PM
                    IsWorkingDaySunday = false,
                    IsWorkingDayMonday = true,
                    IsWorkingDayTuesday = true,
                    IsWorkingDayWednesday = true,
                    IsWorkingDayThursday = true,
                    IsWorkingDayFriday = true,
                    IsWorkingDaySaturday = false,
                    FlexTimeAllowanceMinutes = 30,
                    Description = "برنامه کاری با زمان شناور بیشتر"
                },
                new WorkSchedule
                {
                    Name = "برنامه کاری آخر هفته",
                    StartTime = new TimeSpan(10, 0, 0), // 10:00 AM
                    EndTime = new TimeSpan(18, 0, 0),   // 6:00 PM
                    IsWorkingDaySunday = true,
                    IsWorkingDayMonday = false,
                    IsWorkingDayTuesday = false,
                    IsWorkingDayWednesday = false,
                    IsWorkingDayThursday = false,
                    IsWorkingDayFriday = false,
                    IsWorkingDaySaturday = true,
                    FlexTimeAllowanceMinutes = 15,
                    Description = "برنامه کاری برای کارکنان آخر هفته"
                },
                new WorkSchedule
                {
                    Name = "برنامه کاری شیفت شب",
                    StartTime = new TimeSpan(22, 0, 0), // 10:00 PM
                    EndTime = new TimeSpan(6, 0, 0),    // 6:00 AM
                    IsWorkingDaySunday = false,
                    IsWorkingDayMonday = true,
                    IsWorkingDayTuesday = true,
                    IsWorkingDayWednesday = true,
                    IsWorkingDayThursday = true,
                    IsWorkingDayFriday = false,
                    IsWorkingDaySaturday = false,
                    FlexTimeAllowanceMinutes = 15,
                    Description = "برنامه کاری برای شیفت شب"
                }
            };
            
            context.WorkSchedules.AddRange(workSchedules);
            context.SaveChanges();
            return workSchedules;
        }
        
        private static List<Employee> CreateEmployees(ApplicationDbContext context, List<Department> departments, List<WorkSchedule> workSchedules)
        {
            var employees = new List<Employee>();
            
            // Employees for Finance Department
            employees.Add(new Employee
            {
                FirstName = "علی",
                LastName = "محمدی",
                Email = "ali@example.com",
                PhoneNumber = "09121234567",
                DepartmentId = departments.First(d => d.Name == "مالی").Id,
                WorkScheduleId = workSchedules.First(w => w.Name == "برنامه کاری عادی").Id,
                HireDate = DateTime.Now.AddYears(-2),
                EmployeeCode = "F001",
                Position = "حسابدار"
            });
            
            employees.Add(new Employee
            {
                FirstName = "مریم",
                LastName = "حسینی",
                Email = "maryam@example.com",
                PhoneNumber = "09127654321",
                DepartmentId = departments.First(d => d.Name == "مالی").Id,
                WorkScheduleId = workSchedules.First(w => w.Name == "برنامه کاری انعطاف‌پذیر").Id,
                HireDate = DateTime.Now.AddYears(-1),
                EmployeeCode = "F002",
                Position = "مدیر مالی"
            });
            
            // Employees for IT Department
            employees.Add(new Employee
            {
                FirstName = "سارا",
                LastName = "احمدی",
                Email = "sara@example.com",
                PhoneNumber = "09131234567",
                DepartmentId = departments.First(d => d.Name == "فناوری اطلاعات").Id,
                WorkScheduleId = workSchedules.First(w => w.Name == "برنامه کاری عادی").Id,
                HireDate = DateTime.Now.AddYears(-1),
                EmployeeCode = "IT001",
                Position = "برنامه‌نویس"
            });
            
            employees.Add(new Employee
            {
                FirstName = "حسین",
                LastName = "رضایی",
                Email = "hossein@example.com",
                PhoneNumber = "09133456789",
                DepartmentId = departments.First(d => d.Name == "فناوری اطلاعات").Id,
                WorkScheduleId = workSchedules.First(w => w.Name == "برنامه کاری شیفت شب").Id,
                HireDate = DateTime.Now.AddMonths(-8),
                EmployeeCode = "IT002",
                Position = "مدیر زیرساخت"
            });
            
            // Employees for HR Department
            employees.Add(new Employee
            {
                FirstName = "محمد",
                LastName = "رضایی",
                Email = "mohammad@example.com",
                PhoneNumber = "09141234567",
                DepartmentId = departments.First(d => d.Name == "منابع انسانی").Id,
                WorkScheduleId = workSchedules.First(w => w.Name == "برنامه کاری عادی").Id,
                HireDate = DateTime.Now.AddMonths(-6),
                EmployeeCode = "HR001",
                Position = "کارشناس منابع انسانی"
            });
            
            // Employees for Marketing Department
            employees.Add(new Employee
            {
                FirstName = "زهرا",
                LastName = "کریمی",
                Email = "zahra@example.com",
                PhoneNumber = "09151234567",
                DepartmentId = departments.First(d => d.Name == "بازاریابی").Id,
                WorkScheduleId = workSchedules.First(w => w.Name == "برنامه کاری انعطاف‌پذیر").Id,
                HireDate = DateTime.Now.AddMonths(-8),
                EmployeeCode = "M001",
                Position = "کارشناس بازاریابی"
            });
            
            // Employees for Operations Department
            employees.Add(new Employee
            {
                FirstName = "رضا",
                LastName = "حسینی",
                Email = "reza@example.com",
                PhoneNumber = "09161234567",
                DepartmentId = departments.First(d => d.Name == "عملیات").Id,
                WorkScheduleId = workSchedules.First(w => w.Name == "برنامه کاری عادی").Id,
                HireDate = DateTime.Now.AddMonths(-12),
                EmployeeCode = "OP001",
                Position = "مدیر عملیات"
            });
            
            // Employees for Technical Support Department
            employees.Add(new Employee
            {
                FirstName = "فاطمه",
                LastName = "محمودی",
                Email = "fateme@example.com",
                PhoneNumber = "09171234567",
                DepartmentId = departments.First(d => d.Name == "پشتیبانی فنی").Id,
                WorkScheduleId = workSchedules.First(w => w.Name == "برنامه کاری آخر هفته").Id,
                HireDate = DateTime.Now.AddMonths(-4),
                EmployeeCode = "TS001",
                Position = "کارشناس پشتیبانی"
            });
            
            // Employees for R&D Department
            employees.Add(new Employee
            {
                FirstName = "امیر",
                LastName = "علوی",
                Email = "amir@example.com",
                PhoneNumber = "09181234567",
                DepartmentId = departments.First(d => d.Name == "تحقیق و توسعه").Id,
                WorkScheduleId = workSchedules.First(w => w.Name == "برنامه کاری انعطاف‌پذیر").Id,
                HireDate = DateTime.Now.AddMonths(-15),
                EmployeeCode = "RD001",
                Position = "مهندس تحقیق و توسعه"
            });
            
            context.Employees.AddRange(employees);
            context.SaveChanges();
            return employees;
        }
        
        private static List<WorkCalendar> CreateHolidays(ApplicationDbContext context, DateTime today)
        {
            var holidays = new List<WorkCalendar>
            {
                new WorkCalendar 
                { 
                    Name = "تعطیلی رسمی 1", 
                    Date = today.AddDays(5), 
                    EntryType = CalendarEntryType.Holiday,
                    Description = "تعطیلی رسمی نمونه 1"
                },
                new WorkCalendar 
                { 
                    Name = "تعطیلی رسمی 2", 
                    Date = today.AddDays(12), 
                    EntryType = CalendarEntryType.Holiday,
                    Description = "تعطیلی رسمی نمونه 2"
                },
                new WorkCalendar 
                { 
                    Name = "تعطیلی رسمی 3", 
                    Date = today.AddDays(19), 
                    EntryType = CalendarEntryType.Holiday,
                    Description = "تعطیلی رسمی نمونه 3"
                },
                new WorkCalendar 
                { 
                    Name = "تعطیلی رسمی 4", 
                    Date = today.AddDays(-7), 
                    EntryType = CalendarEntryType.Holiday,
                    Description = "تعطیلی رسمی نمونه 4 (گذشته)"
                },
                new WorkCalendar 
                { 
                    Name = "تعطیلی رسمی 5", 
                    Date = today.AddDays(-14), 
                    EntryType = CalendarEntryType.Holiday,
                    Description = "تعطیلی رسمی نمونه 5 (گذشته)"
                }
            };
            
            context.WorkCalendars.AddRange(holidays);
            context.SaveChanges();
            return holidays;
        }
        
        private static void CreateAttendanceRecords(
            ApplicationDbContext context, 
            int employeeId, 
            DateTime startDate, 
            DateTime endDate, 
            WorkSchedule workSchedule,
            List<WorkCalendar> holidays,
            double presentFrequency,
            double absentFrequency,
            double lateArrivalFrequency,
            double earlyDepartureFrequency,
            double overtimeFrequency,
            double earlyArrivalFrequency)
        {
            var random = new Random();
            var currentDate = startDate;
            
            while (currentDate <= endDate)
            {
                // Check if it's a holiday
                bool isHoliday = holidays.Any(h => h.Date.Date == currentDate.Date && h.EntryType == CalendarEntryType.Holiday);
                
                // Check if it's a weekend
                bool isWeekend = !workSchedule.IsWorkingDay(currentDate.DayOfWeek);
                
                // If weekend or holiday, create a special record and continue
                if (isWeekend || isHoliday)
                {
                    var attendanceCode = isHoliday ? "H" : "W"; // H for Holiday, W for Weekend
                    
                    var specialAttendance = new Attendance
                    {
                        EmployeeId = employeeId,
                        Date = currentDate,
                        Notes = isHoliday ? "Holiday" : "Weekend",
                        AttendanceCode = attendanceCode,
                        IsComplete = true
                    };
                    
                    context.Attendances.Add(specialAttendance);
                    currentDate = currentDate.AddDays(1);
                    continue;
                }
                
                // For working days, randomize attendance status
                double rand = random.NextDouble();
                
                // Create attendance record
                var attendance = new Attendance
                {
                    EmployeeId = employeeId,
                    Date = currentDate,
                    Notes = "",
                    IsComplete = true
                };
                
                // Set standard start and end times
                var standardStartTime = new DateTime(
                    currentDate.Year, currentDate.Month, currentDate.Day,
                    workSchedule.StartTime.Hours, workSchedule.StartTime.Minutes, 0
                );
                
                var standardEndTime = new DateTime(
                    currentDate.Year, currentDate.Month, currentDate.Day,
                    workSchedule.EndTime.Hours, workSchedule.EndTime.Minutes, 0
                );
                
                // Handle night shift that crosses midnight
                if (workSchedule.StartTime > workSchedule.EndTime)
                {
                    standardEndTime = standardEndTime.AddDays(1);
                }
                
                // Determine attendance status
                if (rand < absentFrequency)
                {
                    // Absent - no check-in/check-out times
                    attendance.Notes = "Absent";
                    attendance.AttendanceCode = "A"; // A for Absent
                }
                else
                {
                    // Determine if early arrival
                    bool isEarlyArrival = random.NextDouble() < earlyArrivalFrequency;
                    
                    // Determine if late arrival
                    bool isLateArrival = !isEarlyArrival && random.NextDouble() < lateArrivalFrequency;
                    
                    // Determine if early departure
                    bool isEarlyDeparture = random.NextDouble() < earlyDepartureFrequency;
                    
                    // Determine if overtime
                    bool isOvertime = !isEarlyDeparture && random.NextDouble() < overtimeFrequency;
                    
                    string attendanceCode = "P"; // Default: P for Present
                    
                    // Handle check-in time
                    if (isEarlyArrival)
                    {
                        int earlyMinutes = random.Next(15, 31);
                        attendance.CheckInTime = standardStartTime.AddMinutes(-earlyMinutes);
                        attendance.IsEarlyArrival = true;
                        attendance.EarlyArrivalMinutes = TimeSpan.FromMinutes(earlyMinutes);
                        attendance.Notes = "Early Arrival";
                        attendanceCode = "EA"; // EA for Early Arrival
                    }
                    else if (isLateArrival)
                    {
                        int lateMinutes = random.Next(5, 31);
                        attendance.CheckInTime = standardStartTime.AddMinutes(lateMinutes);
                        attendance.IsLateArrival = true;
                        attendance.LateMinutes = TimeSpan.FromMinutes(lateMinutes);
                        attendance.Notes = "Late Arrival";
                        attendanceCode = "L"; // L for Late
                    }
                    else
                    {
                        // Regular arrival (on time)
                        int minuteVariation = random.Next(-5, 6);
                        attendance.CheckInTime = standardStartTime.AddMinutes(minuteVariation);
                    }
                    
                    // Handle check-out time
                    if (isEarlyDeparture)
                    {
                        int earlyMinutes = random.Next(10, 61);
                        attendance.CheckOutTime = standardEndTime.AddMinutes(-earlyMinutes);
                        attendance.IsEarlyDeparture = true;
                        attendance.EarlyDepartureMinutes = TimeSpan.FromMinutes(earlyMinutes);
                        attendance.Notes = string.IsNullOrEmpty(attendance.Notes) ? "Early Departure" : attendance.Notes + " & Early Departure";
                        attendanceCode = "E"; // E for Early Departure
                    }
                    else if (isOvertime)
                    {
                        int overtimeMinutes = random.Next(15, 121);
                        attendance.CheckOutTime = standardEndTime.AddMinutes(overtimeMinutes);
                        attendance.IsOvertime = true;
                        attendance.OvertimeMinutes = TimeSpan.FromMinutes(overtimeMinutes);
                        attendance.Notes = string.IsNullOrEmpty(attendance.Notes) ? "Overtime" : attendance.Notes + " & Overtime";
                        attendanceCode = "O"; // O for Overtime
                    }
                    else
                    {
                        // Regular departure
                        int minuteVariation = random.Next(-5, 6);
                        attendance.CheckOutTime = standardEndTime.AddMinutes(minuteVariation);
                        
                        if (string.IsNullOrEmpty(attendance.Notes))
                        {
                            attendance.Notes = "Present";
                        }
                    }
                    
                    // Calculate work duration
                    if (attendance.CheckInTime.HasValue && attendance.CheckOutTime.HasValue)
                    {
                        attendance.WorkDuration = attendance.CheckOutTime.Value - attendance.CheckInTime.Value;
                    }
                    
                    attendance.AttendanceCode = attendanceCode;
                }
                
                // Add to context
                context.Attendances.Add(attendance);
                
                // Move to next day
                currentDate = currentDate.AddDays(1);
            }
            
            // Save all records
            context.SaveChanges();
        }
        
        // Legacy method for backward compatibility
        public static void CreateSampleAttendanceData()
        {
            ClearAndCreateSampleData();
        }
    }
} 