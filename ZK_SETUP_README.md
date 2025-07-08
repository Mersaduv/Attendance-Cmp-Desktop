# ZKTeco Integration Setup Guide

## Problem
The error `Failed to initialize database: The type initializer for 'AttandenceDesktop.Services.ExportService' threw an exception` occurs when running the application because of 32-bit compatibility issues between the ZKTeco SDK and the rest of the application.

## Solution
This guide provides steps to properly set up the ZKTeco SDK integration in 32-bit mode.

## Automated Fix
For your convenience, we've provided a script that automatically fixes most common issues:

1. Close all instances of the application
2. Right-click on `fix-32bit.ps1` in the AttandenceDesktop folder
3. Select "Run with PowerShell as Administrator"
4. Follow any on-screen instructions

## Manual Setup Steps (if automated fix doesn't work)

### 1. Ensure 32-bit mode is enabled
Make sure the `.csproj` file has the correct configuration:
```xml
<PropertyGroup>
  <OutputType>WinExe</OutputType>
  <TargetFramework>net9.0</TargetFramework>
  <PlatformTarget>x86</PlatformTarget>
  <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  <BuiltInComInteropSupport>true</BuiltInComInteropSupport>
</PropertyGroup>
```

### 2. Register the ZKTeco DLLs
1. Copy all ZKTeco SDK DLL files to the `SDK` folder in the application directory
2. Using administrator privileges, run this command for 32-bit registration:
   ```
   %SystemRoot%\SysWOW64\regsvr32.exe "path\to\AttandenceDesktop\SDK\zkemkeeper.dll"
   ```

### 3. Fix the database initialization
If the database file is corrupted:
1. Close the application
2. Rename or delete the `TimeAttendance.db` file in the application's output directory
3. Restart the application to recreate the database

### 4. Troubleshooting

#### Cannot connect to ZKTeco device
1. Ensure the device is powered on and connected to the network
2. Verify the IP address and port settings are correct
3. Check that the zkemkeeper.dll is registered properly
4. Run the application as administrator

#### Database errors persist
1. Close the application
2. Delete the `TimeAttendance.db` file
3. Run the application from the command line with the `--create-sample-data` argument

#### General COM issues
If you encounter COM registration issues, try these steps:
1. Open an Administrator Command Prompt
2. Run: `%SystemRoot%\SysWOW64\regsvr32.exe /u "path\to\SDK\zkemkeeper.dll"` to unregister
3. Run: `%SystemRoot%\SysWOW64\regsvr32.exe "path\to\SDK\zkemkeeper.dll"` to re-register
4. Copy all SDK DLL files to the application's output bin directory

## Technical Background
The ZKTeco SDK is a 32-bit COM component, which means:
1. The application must be compiled for x86 (32-bit)
2. The SDK DLLs must be registered using the 32-bit version of regsvr32
3. All dependent libraries must be compatible with 32-bit mode

The error about ExportService is a symptom of architecture mismatch - when running in 32-bit mode, some dependencies like QuestPDF may need special handling.

## Support
If you continue to experience issues after following these steps, please provide:
1. The application log file (app_log.txt)
2. Information about your system (OS version, architecture)
3. The specific error message you're encountering 