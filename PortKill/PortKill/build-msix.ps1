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
        -p:PublishTrimmed=false

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