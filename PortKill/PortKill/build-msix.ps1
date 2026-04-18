param(
    [string]$ExtensionName = "PortKill",
    [string]$Configuration = "Release",
    [string]$Version = "0.0.1.0",
    [string[]]$Platforms = @("x64", "arm64")
)

$ErrorActionPreference = "Stop"

Write-Host "Building $ExtensionName MSIX packages..." -ForegroundColor Green
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Platforms: $($Platforms -join ', ')" -ForegroundColor Yellow

$ProjectDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectFile = "$ProjectDir\$ExtensionName.csproj"
$ManifestFile = "$ProjectDir\app.manifest"
$ManifestBackup = "$ProjectDir\app.manifest.backup"
$AppxManifestFile = "$ProjectDir\Package.appxmanifest"
$AppxManifestBackup = "$ProjectDir\Package.appxmanifest.backup"

# Find makeappx.exe - check multiple common locations
$makeappx = $null

# Try standard Windows Kit locations
$locations = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.*\x64\makeappx.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\11.*\x64\makeappx.exe"
)

foreach ($loc in $locations) {
    $found = Get-ChildItem $loc -Recurse -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
    if ($found) {
        $makeappx = $found
        break
    }
}

# Fallback: use where.exe to find makeappx in PATH
if (-not $makeappx) {
    $makeappx = Get-Command makeappx.exe -ErrorAction SilentlyContinue | Select-Object -First 1
}

if (-not $makeappx) {
    Write-Error "makeappx.exe not found. Please install Windows SDK."
    Write-Host "Checked paths:" -ForegroundColor Yellow
    foreach ($loc in $locations) { Write-Host "  $loc" -ForegroundColor Gray }
    exit 1
}

Write-Host "Using makeappx: $($makeappx.Source)" -ForegroundColor Cyan

# Create backup of original manifests
if (Test-Path $ManifestBackup) {
    Remove-Item $ManifestBackup -Force
}
Copy-Item $ManifestFile $ManifestBackup

if (Test-Path $AppxManifestBackup) {
    Remove-Item $AppxManifestBackup -Force
}
Copy-Item $AppxManifestFile $AppxManifestBackup

# Cleanup before build
if (Test-Path "$ProjectDir\bin") { 
    Remove-Item -Path "$ProjectDir\bin" -Recurse -Force -ErrorAction SilentlyContinue 
}
if (Test-Path "$ProjectDir\obj") { 
    Remove-Item -Path "$ProjectDir\obj" -Recurse -Force -ErrorAction SilentlyContinue 
}

Write-Host "Restoring NuGet packages..." -ForegroundColor Yellow
dotnet restore $ProjectFile

foreach ($Platform in $Platforms) {
    Write-Host "`n=== Building $Platform MSIX ===" -ForegroundColor Cyan
    
    # Restore original manifests before each build
    Copy-Item $ManifestBackup $ManifestFile -Force
    Copy-Item $AppxManifestBackup $AppxManifestFile -Force
    
    Write-Host "Patching manifest version to $Version..." -ForegroundColor Yellow
    
    # Patch app.manifest (assembly version)
    (Get-Content $ManifestFile) -replace '\$\((Version|PackageVersion)\)', $Version |
    Set-Content $ManifestFile
    
    # Patch Package.appxmanifest (Identity version)
    $content = Get-Content $AppxManifestFile -Raw
    $content = $content -replace 'Version="[^"]*"', "Version=`"$Version`""
    Set-Content -Path $AppxManifestFile -Value $content
    
    $platformArg = if ($Platform -eq "arm64") { "ARM64" } else { "x64" }
    $runtimeId = "win-$Platform"
    $packageDir = "AppPackages\$Platform"
    $stagingDir = "$packageDir\staging"
    
    if (Test-Path $packageDir) {
        Remove-Item -Path $packageDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null
    
    Write-Host "Building for $platformArg (Runtime: $runtimeId)..." -ForegroundColor Yellow
    
    # Build the app (without passing version properties - use csproj hardcoded version)
    dotnet publish $ProjectFile `
        --configuration $Configuration `
        -p:Platform=$platformArg `
        -p:RuntimeIdentifier=$runtimeId `
        -p:PublishDir="$stagingDir\" `
        -p:PublishTrimmed=false `
        -p:Version=$Version
    
    if ($LASTEXITCODE -ne 0) { 
        Write-Warning "Build failed for $Platform with exit code: $LASTEXITCODE"
        continue
    }
    
    # Verify we have the exe
    $exePath = "$stagingDir\$ExtensionName.exe"
    if (-not (Test-Path $exePath)) {
        $altExe = Get-ChildItem -Path $stagingDir -Recurse -Filter "*.exe" -ErrorAction SilentlyContinue | Select-Object -First 1
        if ($altExe) {
            $exePath = $altExe.FullName
            Write-Host "Found exe: $($altExe.Name)" -ForegroundColor Yellow
        } else {
            Write-Error "No exe found in $stagingDir"
            continue
        }
    }
    
    # Copy manifest as AppxManifest.xml
    Copy-Item "$ProjectDir\Package.appxmanifest" "$stagingDir\AppxManifest.xml" -Force
    
    # The publish should have copied Assets, verify and copy if needed
    $assetsInStaging = Get-ChildItem "$stagingDir\Assets" -ErrorAction SilentlyContinue
    if (-not $assetsInStaging) {
        Write-Host "Copying Assets to staging..." -ForegroundColor Yellow
        Copy-Item "$ProjectDir\Assets" "$stagingDir\Assets" -Recurse -Force
    }
    
    Write-Host "Creating MSIX package..." -ForegroundColor Yellow
    
    # Create MSIX using makeappx (no validation)
    $msixName = "PortKill_${Version}_${Platform}.msix"
    $msixPath = "$packageDir\$msixName"
    
    & $makeappx.FullName pack /d $stagingDir /p $msixPath /nv
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create MSIX for $Platform. Exit code: $LASTEXITCODE"
        # Debug: show staging dir contents
        Write-Host "Staging dir contents:" -ForegroundColor Yellow
        Get-ChildItem -Path $stagingDir -Recurse | Select-Object FullName
        continue
    }
    
    # Verify MSIX was created
    if (Test-Path $msixPath) {
        $msix = Get-Item $msixPath
        $sizeMB = [math]::Round($msix.Length / 1MB, 2)
        Write-Host "Created MSIX: $msixName ($sizeMB MB)" -ForegroundColor Green
    } else {
        Write-Error "MSIX not found at $msixPath"
    }
    
    # Cleanup staging folder (keep MSIX in packageDir)
    Remove-Item -Path $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
}

Write-Host "`n=== Build completed! ===" -ForegroundColor Green
Write-Host ""
Write-Host "To install the extension:" -ForegroundColor Cyan
Write-Host "1. Double-click the MSIX file to install" -ForegroundColor White
Write-Host "2. Open Command Palette (Win+Shift+P)" -ForegroundColor White
Write-Host "3. Type 'Reload' and select 'Reload Command Palette Extension'" -ForegroundColor White
Write-Host ""
Write-Host "MSIX files location: AppPackages\x64\ and AppPackages\arm64\" -ForegroundColor Yellow

# Cleanup backup files
if (Test-Path $ManifestBackup) {
    Remove-Item $ManifestBackup -Force
}
if (Test-Path $AppxManifestBackup) {
    Remove-Item $AppxManifestBackup -Force
}
