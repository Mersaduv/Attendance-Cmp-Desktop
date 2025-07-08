# PowerShell script to set up SDK libraries
# Run this script to copy the SDK DLL files to the project output directory

# Set this to the path of your ZKTeco SDK files
$sdkSourcePath = "D:\config\Fingerprint\sdk"
$projectPath = $PSScriptRoot
$outputDir = Join-Path -Path $projectPath -ChildPath "bin\Debug\net9.0"

# Create SDK directory if it doesn't exist
$sdkDir = Join-Path -Path $projectPath -ChildPath "SDK"
if (-not (Test-Path -Path $sdkDir)) {
    New-Item -Path $sdkDir -ItemType Directory | Out-Null
    Write-Host "Created SDK directory: $sdkDir"
}

# List of required SDK DLLs
$requiredDlls = @(
    "commpro.dll", 
    "comms.dll",
    "libareacode.dll",
    "plcommpro.dll", 
    "plcomms.dll",
    "plrscomm.dll", 
    "plusbcomm.dll",
    "plrscagent.dll",
    "rscomm.dll",
    "rscagent.dll",
    "tcpcomm.dll", 
    "usbcomm.dll",
    "usbstd.dll",
    "ZKCommuCryptoClient.dll",
    "zkemkeeper.dll",
    "zkemsdk.dll"
)

# Check if the source path exists
if (-not (Test-Path -Path $sdkSourcePath)) {
    Write-Host "SDK source path not found: $sdkSourcePath" -ForegroundColor Red
    Write-Host "Please update the sdkSourcePath variable to point to your SDK files." -ForegroundColor Red
    
    # Alternative: Try looking in connectTest project
    $alternativePath = Join-Path -Path $projectPath -ChildPath "..\connectTest\GhalibHRAttendance\SDK"
    if (Test-Path -Path $alternativePath) {
        Write-Host "Found alternative SDK source in connectTest project: $alternativePath" -ForegroundColor Green
        $sdkSourcePath = $alternativePath
    } else {
        Write-Host "Alternative SDK source not found either. Please provide the path manually." -ForegroundColor Yellow
        Exit 1
    }
}

# Copy required DLLs from source to SDK directory
Write-Host "Copying SDK files from: $sdkSourcePath" -ForegroundColor Cyan
foreach ($dll in $requiredDlls) {
    $sourcePath = Join-Path -Path $sdkSourcePath -ChildPath $dll
    $destPath = Join-Path -Path $sdkDir -ChildPath $dll
    
    if (Test-Path -Path $sourcePath) {
        Copy-Item -Path $sourcePath -Destination $destPath -Force
        Write-Host "Copied $dll to SDK directory"
    } else {
        Write-Host "Warning: Could not find $dll in source path. Please copy it manually." -ForegroundColor Yellow
    }
}

# Also copy to output directory directly
if (-not (Test-Path -Path $outputDir)) {
    Write-Host "Output directory not found. Build the project first." -ForegroundColor Yellow
} else {
    foreach ($dll in $requiredDlls) {
        $sourcePath = Join-Path -Path $sdkDir -ChildPath $dll
        $destPath = Join-Path -Path $outputDir -ChildPath $dll
        
        if (Test-Path -Path $sourcePath) {
            Copy-Item -Path $sourcePath -Destination $destPath -Force
            Write-Host "Copied $dll to output directory"
        }
    }
}

Write-Host "SDK setup complete. Make sure to build the project to ensure all files are copied to the output directory." -ForegroundColor Green
Write-Host "Remember to register the zkemkeeper.dll using register-dll.ps1 (run as administrator)" -ForegroundColor Yellow

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 