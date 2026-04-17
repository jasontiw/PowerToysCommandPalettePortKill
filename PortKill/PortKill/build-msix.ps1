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
    $packageDir = "AppPackages\$Platform"
    
    if (Test-Path $packageDir) {
        Remove-Item -Path $packageDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null
    
    Write-Host "Building MSIX for $platformArg..." -ForegroundColor Yellow
    
    dotnet build $ProjectFile `
        --configuration $Configuration `
        -p:Platform=$platformArg `
        -p:RuntimeIdentifier="win-$Platform" `
        -p:GenerateAppxPackageOnBuild=true `
        -p:AppxPackageDir="$packageDir\" `
        -p:AppxBundle=Never `
        -p:PublishTrimmed=false `
        -p:Version=$Version `
        -p:PackageVersion=$Version `
        -p:ApplicationVersion=$Version

    if ($LASTEXITCODE -ne 0) { 
        Write-Warning "Build failed for $Platform with exit code: $LASTEXITCODE"
        continue
    }

    $msixFiles = Get-ChildItem -Path $packageDir -Recurse -Filter "*.msix" -ErrorAction SilentlyContinue
    if ($msixFiles) {
        foreach ($msix in $msixFiles) {
            $sizeMB = [math]::Round($msix.Length / 1MB, 2)
            Write-Host "Created MSIX: $($msix.Name) ($sizeMB MB)" -ForegroundColor Green
        }
    } else {
        Write-Warning "No MSIX files found in $packageDir"
        $binMsix = Get-ChildItem -Path "$ProjectDir\bin" -Recurse -Filter "*.msix" -ErrorAction SilentlyContinue
        if ($binMsix) {
            Write-Host "Found MSIX in bin directory:" -ForegroundColor Yellow
            foreach ($msix in $binMsix) {
                $sizeMB = [math]::Round($msix.Length / 1MB, 2)
                Write-Host "  $($msix.Name) ($sizeMB MB)" -ForegroundColor Green
            }
        }
    }
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
