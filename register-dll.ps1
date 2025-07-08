# Run with administrator privileges to register ZKTeco COM DLL
# This is specifically for registering 32-bit DLL in 64-bit Windows

# Get full path to zkemkeeper.dll
$dllPath = Join-Path $PSScriptRoot "SDK\zkemkeeper.dll"
Write-Host "DLL Path: $dllPath"

# Check if file exists
if (!(Test-Path $dllPath)) {
    Write-Host "ERROR: DLL not found at $dllPath" -ForegroundColor Red
    exit 1
}

# Check if running as administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script requires administrator privileges. Please run PowerShell as Administrator." -ForegroundColor Red
    exit 1
}

# Determine proper regsvr32 path based on OS architecture
$regsvr32Path = ""
if ([Environment]::Is64BitOperatingSystem) {
    $regsvr32Path = "$env:SystemRoot\SysWOW64\regsvr32.exe"
    Write-Host "Using 32-bit regsvr32 on 64-bit Windows: $regsvr32Path" -ForegroundColor Yellow
} else {
    $regsvr32Path = "$env:SystemRoot\System32\regsvr32.exe"
    Write-Host "Using regsvr32 on 32-bit Windows: $regsvr32Path" -ForegroundColor Yellow
}

# Check if regsvr32 exists
if (!(Test-Path $regsvr32Path)) {
    Write-Host "ERROR: regsvr32 not found at $regsvr32Path" -ForegroundColor Red
    exit 1
}

# Register the DLL
Write-Host "Registering DLL... (this may take a moment)" -ForegroundColor Cyan
try {
    $process = Start-Process -FilePath $regsvr32Path -ArgumentList "/s `"$dllPath`"" -PassThru -Wait
    if ($process.ExitCode -eq 0) {
        Write-Host "SUCCESS: DLL registered successfully!" -ForegroundColor Green
    } else {
        Write-Host "ERROR: DLL registration failed with exit code $($process.ExitCode)" -ForegroundColor Red
    }
} catch {
    Write-Host "ERROR: Failed to register DLL: $_" -ForegroundColor Red
}

# Copy DLL files to output directory
$outputDir = Join-Path $PSScriptRoot "bin\Debug\net9.0"
if (Test-Path $outputDir) {
    Write-Host "Copying DLL files to output directory: $outputDir" -ForegroundColor Cyan
    Copy-Item -Path (Join-Path $PSScriptRoot "SDK\*.dll") -Destination $outputDir -Force
    Write-Host "DLLs copied successfully." -ForegroundColor Green
} else {
    Write-Host "WARNING: Output directory not found: $outputDir" -ForegroundColor Yellow
}

Write-Host "Done. Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 