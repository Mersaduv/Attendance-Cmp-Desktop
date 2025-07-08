# Register ZKTeco DLL files script
# This script needs to be run as Administrator

# Check if running as administrator
if (-NOT ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Warning "This script needs to be run as Administrator. Attempting to elevate..."
    Start-Process PowerShell -Verb RunAs "-NoProfile -ExecutionPolicy Bypass -File `"$PSCommandPath`""
    exit
}

Write-Host "Running with administrator privileges" -ForegroundColor Green

# Define paths
$scriptPath = Split-Path -Parent $MyInvocation.MyCommand.Definition
$sdkPath = Join-Path -Path $scriptPath -ChildPath "SDK"
$zkemkeeperPath = Join-Path -Path $sdkPath -ChildPath "zkemkeeper.dll"
$nativeX86Path = Join-Path -Path $scriptPath -ChildPath "native\x86"
$outputPath = Join-Path -Path $scriptPath -ChildPath "bin\Debug\net9.0"

# Print environment info
Write-Host "Current directory: $scriptPath"
Write-Host "SDK path: $sdkPath"
Write-Host "Native x86 path: $nativeX86Path"
Write-Host "Output path: $outputPath"
Write-Host "Is 64-bit OS: $([Environment]::Is64BitOperatingSystem)"

# Ensure SDK directory exists
if (-not (Test-Path $sdkPath)) {
    Write-Host "SDK directory not found at: $sdkPath" -ForegroundColor Red
    # Try alternate location
    $sdkPath = Join-Path -Path $outputPath -ChildPath "SDK"
    $zkemkeeperPath = Join-Path -Path $sdkPath -ChildPath "zkemkeeper.dll"
    
    if (-not (Test-Path $sdkPath)) {
        Write-Host "SDK directory not found at alternate location: $sdkPath" -ForegroundColor Red
        exit 1
    }
    else {
        Write-Host "Using SDK directory at alternate location: $sdkPath" -ForegroundColor Yellow
    }
}

# Check if zkemkeeper.dll exists
if (-not (Test-Path $zkemkeeperPath)) {
    Write-Host "zkemkeeper.dll not found at: $zkemkeeperPath" -ForegroundColor Red
    
    # Try to find zkemkeeper.dll in various locations
    $possibleLocations = @(
        (Join-Path -Path $scriptPath -ChildPath "zkemkeeper.dll"),
        (Join-Path -Path $outputPath -ChildPath "zkemkeeper.dll"),
        (Join-Path -Path $outputPath -ChildPath "SDK\zkemkeeper.dll"),
        (Join-Path -Path $nativeX86Path -ChildPath "zkemkeeper.dll")
    )
    
    $found = $false
    foreach ($location in $possibleLocations) {
        if (Test-Path $location) {
            $zkemkeeperPath = $location
            $found = $true
            Write-Host "Found zkemkeeper.dll at: $zkemkeeperPath" -ForegroundColor Green
            break
        }
    }
    
    if (-not $found) {
        Write-Host "zkemkeeper.dll not found in any standard location" -ForegroundColor Red
        exit 1
    }
}

# Register the DLL using the appropriate regsvr32
try {
    Write-Host "Attempting to register zkemkeeper.dll at: $zkemkeeperPath" -ForegroundColor Cyan
    
    if ([Environment]::Is64BitOperatingSystem) {
        # On 64-bit Windows, use SysWOW64\regsvr32.exe for 32-bit DLLs
        $regsvr32Path = "$env:SystemRoot\SysWOW64\regsvr32.exe"
        Write-Host "Using 32-bit regsvr32 from SysWOW64 on 64-bit Windows" -ForegroundColor Yellow
    } else {
        # On 32-bit Windows, use System32\regsvr32.exe
        $regsvr32Path = "$env:SystemRoot\System32\regsvr32.exe"
        Write-Host "Using standard regsvr32 on 32-bit Windows" -ForegroundColor Yellow
    }
    
    Write-Host "Registration command: $regsvr32Path /s `"$zkemkeeperPath`""
    
    # Register the DLL
    $process = Start-Process -FilePath $regsvr32Path -ArgumentList "/s `"$zkemkeeperPath`"" -Wait -PassThru
    
    if ($process.ExitCode -eq 0) {
        Write-Host "zkemkeeper.dll registered successfully!" -ForegroundColor Green
    } else {
        Write-Host "Failed to register zkemkeeper.dll. Exit code: $($process.ExitCode)" -ForegroundColor Red
    }
    
    # Copy DLL to the output directory if needed
    if (-not (Test-Path (Join-Path -Path $outputPath -ChildPath "zkemkeeper.dll"))) {
        Write-Host "Copying zkemkeeper.dll to output directory: $outputPath" -ForegroundColor Cyan
        Copy-Item -Path $zkemkeeperPath -Destination $outputPath -Force
    }
    
    # Copy to native directory if needed
    if (-not (Test-Path $nativeX86Path)) {
        New-Item -ItemType Directory -Path $nativeX86Path -Force | Out-Null
        Write-Host "Created native\x86 directory" -ForegroundColor Yellow
    }
    
    if (-not (Test-Path (Join-Path -Path $nativeX86Path -ChildPath "zkemkeeper.dll"))) {
        Write-Host "Copying zkemkeeper.dll to native\x86 directory: $nativeX86Path" -ForegroundColor Cyan
        Copy-Item -Path $zkemkeeperPath -Destination $nativeX86Path -Force
    }
    
    Write-Host "ZKTeco DLL setup completed." -ForegroundColor Green
    Write-Host "Press any key to exit..." -ForegroundColor Cyan
    $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | Out-Null
} 
catch {
    Write-Host "Error registering DLL: $_" -ForegroundColor Red
    Write-Host "Press any key to exit..." -ForegroundColor Cyan
    $host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") | Out-Null
} 