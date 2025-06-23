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