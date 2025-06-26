# GhalibHR Attendance Desktop Application

## SQLite Database Configuration

This application has been configured to use SQLite as its database engine, making it portable and easy to install on any Windows system without requiring SQL Server.

### Key Features

- **Portable Database**: The SQLite database file is stored locally in the application directory.
- **No External Dependencies**: No need to install or configure SQL Server.
- **Easy Deployment**: Just copy the application folder to any Windows system and run it.
- **Data Migration**: Includes tools to migrate data from SQL Server to SQLite if needed.

### Database Location

The SQLite database file is stored at:
```
[Application Directory]/TimeAttendance.db
```

### Data Migration

If you need to migrate data from an existing SQL Server database:

1. Export your data from SQL Server to CSV files:
   - departments.csv
   - employees.csv
   - workschedules.csv
   - attendances.csv
   - workcalendars.csv

2. Place these files in the `[Application Directory]/Import` folder.

3. Use the built-in data migration tool to import the data into SQLite.

### Technical Details

- The application uses Entity Framework Core with SQLite provider.
- Database schema is automatically created on first run.
- Initial seed data for departments and work schedules is included.

### Requirements

- Windows operating system
- .NET 9.0 Runtime

### Troubleshooting

If you encounter any database-related issues:

1. Check that the application has write permissions to its directory.
2. Verify that the TimeAttendance.db file exists and is not corrupted.
3. Check the application logs at `[Application Directory]/app.log` for detailed error information.

## Flexible Hours Feature Guide

### Setting Up Departments and Schedules

1. **Create a Department**:
   - Navigate to the Departments tab and click "Add Department"
   - Enter the department name and save

2. **Create a Work Schedule**:
   - Navigate to the Work Schedules tab and click "Add Work Schedule"
   - Enter a name (e.g., "Standard Office Hours")
   - Set the start time (e.g., 8:00) and end time (e.g., 17:00)
   - Select working days (typically Monday-Friday)
   - Save the schedule

3. **Assign Schedule to Department**:
   - When creating/editing the work schedule, select the department from the dropdown
   - This makes the schedule the default for all employees in that department

### Employee Types

#### Regular Employees (Fixed Schedule)

1. **Creating a Regular Employee**:
   - Navigate to Employees tab and click "Add Employee"
   - Fill in employee details and select the department
   - Keep "Flexible Hours (Total Hours Only)" **UNCHECKED**
   - Save the employee

2. **How Regular Employees are Tracked**:
   - Must follow the assigned department's schedule
   - Will be marked as late if they check in after the schedule start time (plus grace period)
   - Will be marked as leaving early if they check out before the schedule end time
   - Reports will show attendance status including Late Arrival, Early Departure, etc.

#### Flexible Hours Employees (Total Hours Only)

1. **Creating a Flexible Hours Employee**:
   - Navigate to Employees tab and click "Add Employee"
   - Fill in employee details and select the department
   - **CHECK** the "Flexible Hours (Total Hours Only)" checkbox
   - Set the "Required Work Hours Per Day" (defaults to 8.0)
   - Save the employee

2. **How Flexible Hours Employees are Tracked**:
   - Not bound by specific start/end times
   - Can work at any time of day (even at night)
   - Only evaluated on total hours worked per day
   - Reports will show attendance status as Present, Half Day, etc. based on total hours
   - Will NOT be marked as Late or Early Departure

### Reports

The reporting system automatically handles both types of employees:

- **Regular Employees**: Reports show attendance status including Late Arrival, Early Departure
- **Flexible Hours Employees**: Reports show only if they worked their required hours, not when

## راهنمای ویژگی ساعات کاری انعطاف‌پذیر

### تنظیم دپارتمان‌ها و برنامه‌های کاری

1. **ایجاد دپارتمان**:
   - به تب دپارتمان‌ها بروید و روی "افزودن دپارتمان" کلیک کنید
   - نام دپارتمان را وارد کرده و ذخیره کنید

2. **ایجاد برنامه کاری**:
   - به تب برنامه‌های کاری بروید و روی "افزودن برنامه کاری" کلیک کنید
   - یک نام وارد کنید (مثلاً "ساعات کاری استاندارد")
   - زمان شروع (مثلاً 8:00) و زمان پایان (مثلاً 17:00) را تنظیم کنید
   - روزهای کاری را انتخاب کنید (معمولاً دوشنبه تا جمعه)
   - برنامه را ذخیره کنید

3. **اختصاص برنامه کاری به دپارتمان**:
   - هنگام ایجاد یا ویرایش برنامه کاری، دپارتمان را از منوی کشویی انتخاب کنید
   - این کار برنامه را به عنوان پیش‌فرض برای همه کارکنان آن دپارتمان تنظیم می‌کند

### انواع کارمندان

#### کارمندان معمولی (برنامه کاری ثابت)

1. **ایجاد کارمند معمولی**:
   - به تب کارمندان بروید و روی "افزودن کارمند" کلیک کنید
   - اطلاعات کارمند را وارد کرده و دپارتمان را انتخاب کنید
   - گزینه "ساعات کاری انعطاف‌پذیر (فقط مجموع ساعات)" را **تیک نزنید**
   - کارمند را ذخیره کنید

2. **نحوه پیگیری کارمندان معمولی**:
   - باید از برنامه کاری دپارتمان پیروی کنند
   - اگر بعد از زمان شروع برنامه (به علاوه زمان مجاز تاخیر) ورود زنند، به عنوان تأخیر علامت‌گذاری می‌شوند
   - اگر قبل از زمان پایان برنامه خروج بزنند، به عنوان خروج زودهنگام علامت‌گذاری می‌شوند
   - گزارش‌ها وضعیت حضور شامل ورود با تأخیر، خروج زودهنگام و غیره را نشان می‌دهند

#### کارمندان با ساعات کاری انعطاف‌پذیر (فقط مجموع ساعات)

1. **ایجاد کارمند با ساعات کاری انعطاف‌پذیر**:
   - به تب کارمندان بروید و روی "افزودن کارمند" کلیک کنید
   - اطلاعات کارمند را وارد کرده و دپارتمان را انتخاب کنید
   - گزینه "ساعات کاری انعطاف‌پذیر (فقط مجموع ساعات)" را **تیک بزنید**
   - "ساعات کاری مورد نیاز در روز" را تنظیم کنید (پیش‌فرض 8.0)
   - کارمند را ذخیره کنید

2. **نحوه پیگیری کارمندان با ساعات کاری انعطاف‌پذیر**:
   - محدود به زمان‌های شروع/پایان خاصی نیستند
   - می‌توانند در هر زمانی از شبانه‌روز کار کنند (حتی نیمه شب)
   - فقط بر اساس کل ساعات کارکرد روزانه ارزیابی می‌شوند
   - گزارش‌ها وضعیت حضور را به صورت حاضر، نیم روز و غیره بر اساس کل ساعات کاری نشان می‌دهند
   - به عنوان تأخیر یا خروج زودهنگام علامت‌گذاری نخواهند شد

### گزارش‌ها

سیستم گزارش‌دهی به طور خودکار هر دو نوع کارمند را مدیریت می‌کند:

- **کارمندان معمولی**: گزارش‌ها وضعیت حضور شامل ورود با تأخیر، خروج زودهنگام را نشان می‌دهند
- **کارمندان با ساعات کاری انعطاف‌پذیر**: گزارش‌ها فقط نشان می‌دهند که آیا ساعات کاری مورد نیازشان را انجام داده‌اند، نه اینکه چه زمانی کار کرده‌اند 