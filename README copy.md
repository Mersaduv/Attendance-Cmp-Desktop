# ZKTeco SDK Setup

This directory contains the ZKTeco SDK DLLs required for the application to communicate with ZKTeco attendance devices.

## Files Required

The following DLLs should be present in this directory:
- zkemkeeper.dll - Main COM DLL for device communication
- zkemsdk.dll - SDK support library
- commpro.dll - Communication library
- comms.dll - Communication library
- plcommpro.dll - Communication library
- And other supporting DLLs

## Setup Instructions

To properly set up the SDK for development:

1. **Register the DLL:**
   Run the `register-dll.ps1` script with administrator privileges
   ```
   Right-click on AttandenceDesktop/register-dll.ps1 and choose "Run as Administrator"
   ```
   
2. **Update SDK Files:**
   If you need to update the SDK files from another location, use the `setup-sdk.ps1` script
   ```
   Right-click on AttandenceDesktop/setup-sdk.ps1 and choose "Run with PowerShell"
   ```
   
3. **Verify Architecture:**
   Verify that the DLL and project architectures match by running the `check-arch.ps1` script
   ```
   Right-click on AttandenceDesktop/check-arch.ps1 and choose "Run with PowerShell"
   ```

## Important Notes

- This project is configured to run in 32-bit mode (`x86`) to be compatible with the ZKTeco SDK
- The `<PlatformTarget>x86</PlatformTarget>` setting in the `.csproj` file is crucial for proper operation
- When running on 64-bit Windows, use `%SystemRoot%\SysWOW64\regsvr32.exe` to register the DLL

## Troubleshooting

If you encounter connection issues:
1. Verify that the DLL is properly registered
2. Ensure the device is connected to the network and accessible
3. Check that the IP address and port number are correct
4. Try running the application with administrator privileges 