# Script to check DLL architecture
$dllPath = ".\SDK\zkemkeeper.dll"
if (Test-Path $dllPath) {
    $fileStream = New-Object System.IO.FileStream($dllPath, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read)
    $binaryReader = New-Object System.IO.BinaryReader($fileStream)
    
    # Seek to PE header offset
    $fileStream.Position = 0x3C
    $peHeaderOffset = $binaryReader.ReadUInt32()
    
    # Go to Machine type field (PE + 4)
    $fileStream.Position = $peHeaderOffset + 4
    $machineType = $binaryReader.ReadUInt16()
    
    # 0x014c = x86 (32-bit), 0x8664 = x64 (64-bit)
    if ($machineType -eq 0x014c) {
        Write-Host "DLL Architecture: x86 (32-bit)" -ForegroundColor Green
    } elseif ($machineType -eq 0x8664) {
        Write-Host "DLL Architecture: x64 (64-bit)" -ForegroundColor Yellow
    } else {
        Write-Host "Unknown architecture: $machineType" -ForegroundColor Red
    }
    
    $binaryReader.Close()
    $fileStream.Close()
} else {
    Write-Host "File not found: $dllPath" -ForegroundColor Red
}

# Check project architecture
$projectFile = ".\AttandenceDesktop.csproj"
if (Test-Path $projectFile) {
    $content = Get-Content $projectFile -Raw
    if ($content -match '<PlatformTarget>x86</PlatformTarget>') {
        Write-Host "Project Architecture: x86 (32-bit)" -ForegroundColor Green
    } elseif ($content -match '<PlatformTarget>x64</PlatformTarget>') {
        Write-Host "Project Architecture: x64 (64-bit)" -ForegroundColor Yellow
    } elseif ($content -match '<PlatformTarget>AnyCPU</PlatformTarget>') {
        Write-Host "Project Architecture: AnyCPU" -ForegroundColor Yellow
        Write-Host "Warning: AnyCPU may cause issues with 32-bit DLLs. Consider changing to x86." -ForegroundColor Red
    } else {
        Write-Host "Project Architecture: Not explicitly specified" -ForegroundColor Red
        Write-Host "Warning: Implicit architecture may cause issues with 32-bit DLLs. Add <PlatformTarget>x86</PlatformTarget> to project file." -ForegroundColor Red
    }
} else {
    Write-Host "Project file not found: $projectFile" -ForegroundColor Red
}

# Check process architecture
Write-Host "Current process is " -NoNewline
if ([Environment]::Is64BitProcess) {
    Write-Host "64-bit" -ForegroundColor Yellow
    Write-Host "Warning: Running as 64-bit may cause issues with 32-bit DLLs" -ForegroundColor Red
} else {
    Write-Host "32-bit" -ForegroundColor Green
}

# Check OS architecture
Write-Host "Operating system is " -NoNewline
if ([Environment]::Is64BitOperatingSystem) {
    Write-Host "64-bit" -ForegroundColor Yellow
} else {
    Write-Host "32-bit" -ForegroundColor Green
}

Write-Host "Press any key to exit..."
$null = $Host.UI.RawUI.ReadKey("NoEcho,IncludeKeyDown") 