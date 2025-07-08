# Fix 32-bit compatibility issues for AttandenceDesktop
# Run as administrator

$ErrorActionPreference = "Stop"

# Check if running as administrator
$isAdmin = ([Security.Principal.WindowsPrincipal] [Security.Principal.WindowsIdentity]::GetCurrent()).IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
if (-not $isAdmin) {
    Write-Host "ERROR: This script requires administrator privileges. Please run PowerShell as Administrator." -ForegroundColor Red
    exit 1
}

# Detect if running on 64-bit OS
$is64BitOS = [Environment]::Is64BitOperatingSystem
Write-Host "Operating System: $([Environment]::OSVersion.VersionString)" -ForegroundColor Cyan
Write-Host "Is 64-bit OS: $is64BitOS" -ForegroundColor Cyan

# Set paths
$appDir = $PSScriptRoot
$sdkDir = Join-Path $appDir "SDK"
$nativeDir = Join-Path $appDir "native"
$x86Dir = Join-Path $nativeDir "x86"
$outputDir = Join-Path $appDir "bin\Debug\net9.0"

# Ensure directories exist
if (-not (Test-Path $sdkDir)) { New-Item -Path $sdkDir -ItemType Directory -Force | Out-Null }
if (-not (Test-Path $x86Dir)) { New-Item -Path $x86Dir -ItemType Directory -Force | Out-Null }
if (-not (Test-Path $outputDir)) { New-Item -Path $outputDir -ItemType Directory -Force | Out-Null }

Write-Host "1. Looking for zkemkeeper.dll..." -ForegroundColor Green

# Search paths for ZKTeco DLLs
$searchPaths = @(
    $sdkDir,
    (Join-Path $appDir "..\connectTest\GhalibHRAttendance\SDK"),
    "D:\config\Fingerprint\sdk",
    "C:\Program Files\ZKTeco\ZKBioTime\sdk",
    "C:\Program Files (x86)\ZKTeco\ZKBioTime\sdk"
)

$zkemkeeperDll = $null
foreach ($path in $searchPaths) {
    if (Test-Path $path) {
        $dllPath = Join-Path $path "zkemkeeper.dll"
        if (Test-Path $dllPath) {
            $zkemkeeperDll = $dllPath
            Write-Host "Found zkemkeeper.dll at $zkemkeeperDll" -ForegroundColor Green
            break
        }
    }
}

if (-not $zkemkeeperDll) {
    Write-Host "ERROR: Could not find zkemkeeper.dll in any of the search paths" -ForegroundColor Red
    exit 1
}

# Copy SDK files
Write-Host "2. Copying SDK files..." -ForegroundColor Green
$requiredDlls = @(
    "zkemkeeper.dll", 
    "zkemsdk.dll", 
    "commpro.dll", 
    "comms.dll",
    "plcommpro.dll", 
    "plcomms.dll"
)

$sourcePath = [System.IO.Path]::GetDirectoryName($zkemkeeperDll)
foreach ($dll in $requiredDlls) {
    $sourceDll = Join-Path $sourcePath $dll
    if (Test-Path $sourceDll) {
        # Function to safely copy files (avoid copying to self)
        function SafeCopy {
            param([string]$source, [string]$destination)
            
            # Create destination directory if it doesn't exist
            $destDir = [System.IO.Path]::GetDirectoryName($destination)
            if (-not (Test-Path $destDir)) {
                New-Item -Path $destDir -ItemType Directory -Force | Out-Null
            }
            
            # Only try to resolve paths if both files exist
            if ((Test-Path $source) -and (Test-Path $destination)) {
                if ((Resolve-Path $source).Path -ne (Resolve-Path $destination).Path) {
                    Copy-Item -Path $source -Destination $destination -Force
                    return $true
                } else {
                    Write-Host "  - Skipping copy: Source and destination are the same: $source" -ForegroundColor Yellow
                    return $false
                }
            } else {
                # Source exists (we checked earlier) but destination doesn't exist yet
                Copy-Item -Path $source -Destination $destination -Force
                return $true
            }
        }
        
        # Copy to SDK dir (skip if source is already in SDK dir)
        $sdkDest = Join-Path $sdkDir $dll
        if (SafeCopy -source $sourceDll -destination $sdkDest) {
            Write-Host "  - Copied $dll to SDK directory" -ForegroundColor Cyan
        }
        
        # Copy to native x86 dir
        $x86Dest = Join-Path $x86Dir $dll
        if (SafeCopy -source $sourceDll -destination $x86Dest) {
            Write-Host "  - Copied $dll to native/x86 directory" -ForegroundColor Cyan
        }
        
        # Copy to application root
        $rootDest = Join-Path $appDir $dll
        if (SafeCopy -source $sourceDll -destination $rootDest) {
            Write-Host "  - Copied $dll to application root directory" -ForegroundColor Cyan
        }
        
        # Copy to output directory
        $outputDest = Join-Path $outputDir $dll
        if (SafeCopy -source $sourceDll -destination $outputDest) {
            Write-Host "  - Copied $dll to output directory" -ForegroundColor Cyan
        }
    } else {
        Write-Host "  - WARNING: Could not find $dll in source path" -ForegroundColor Yellow
    }
}

# Register the zkemkeeper.dll using appropriate regsvr32
Write-Host "3. Registering zkemkeeper.dll..." -ForegroundColor Green

# Use 32-bit regsvr32 on 64-bit Windows
$regsvr32Path = ""
if ($is64BitOS) {
    $regsvr32Path = "$env:SystemRoot\SysWOW64\regsvr32.exe"
    Write-Host "Using 32-bit regsvr32 on 64-bit Windows: $regsvr32Path" -ForegroundColor Cyan
} else {
    $regsvr32Path = "$env:SystemRoot\System32\regsvr32.exe"
    Write-Host "Using regsvr32 on 32-bit Windows: $regsvr32Path" -ForegroundColor Cyan
}

# Check if regsvr32 exists
if (!(Test-Path $regsvr32Path)) {
    Write-Host "ERROR: regsvr32 not found at $regsvr32Path" -ForegroundColor Red
    exit 1
}

# Register the SDK dll
try {
    $dllToRegister = Join-Path $sdkDir "zkemkeeper.dll"
    Write-Host "Registering DLL: $dllToRegister" -ForegroundColor Cyan
    $process = Start-Process -FilePath $regsvr32Path -ArgumentList "/s `"$dllToRegister`"" -PassThru -Wait
    if ($process.ExitCode -eq 0) {
        Write-Host "SUCCESS: DLL registered successfully!" -ForegroundColor Green
    } else {
        Write-Host "ERROR: DLL registration failed with exit code $($process.ExitCode)" -ForegroundColor Red
    }
} catch {
    Write-Host "ERROR: Failed to register DLL: $_" -ForegroundColor Red
}

# Also register the x86 dll
try {
    $dllToRegister = Join-Path $x86Dir "zkemkeeper.dll"
    Write-Host "Registering DLL: $dllToRegister" -ForegroundColor Cyan
    $process = Start-Process -FilePath $regsvr32Path -ArgumentList "/s `"$dllToRegister`"" -PassThru -Wait
    if ($process.ExitCode -eq 0) {
        Write-Host "SUCCESS: DLL registered successfully!" -ForegroundColor Green
    } else {
        Write-Host "ERROR: DLL registration failed with exit code $($process.ExitCode)" -ForegroundColor Red
    }
} catch {
    Write-Host "ERROR: Failed to register DLL: $_" -ForegroundColor Red
}

Write-Host "`nAll tasks completed. The application should now work properly in 32-bit mode." -ForegroundColor Green
Write-Host "If you still have issues, please try recompiling the application with PlatformTarget=x86 explicitly set in the .csproj file." -ForegroundColor Yellow

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 