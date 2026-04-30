<#
.SYNOPSIS
    Unpacks and inspects an MSIX package.
.DESCRIPTION
    Extracts MSIX contents and checks manifest values.
    Uses dynamic paths based on script location.
.PARAMETER MsixPath
    Path to MSIX file. Default: auto-detect from AppPackages.
.PARAMETER OutputDir
    Output directory for unpacked files. Default: auto-detect.
.PARAMETER MakeAppxPath
    Path to makeappx.exe. Default: auto-discover.
#>

param(
    [Parameter()]
    [string]$MsixPath,
    
    [Parameter()]
    [string]$OutputDir,
    
    [Parameter()]
    [string]$MakeAppxPath,
    
    [Parameter()]
    [ValidateSet("x64", "arm64")]
    [string]$Architecture = "x64"
)

$ErrorActionPreference = "Stop"

# Detect project root (supports scripts at project root or in scripts subdirectory)
$ProjectRoot = $PSScriptRoot
if (-not (Test-Path (Join-Path $PSScriptRoot "PortKill"))) {
    $parent = Split-Path $PSScriptRoot -Parent
    if ($parent -and (Test-Path (Join-Path $parent "PortKill"))) {
        $ProjectRoot = $parent
    }
}
# If PortKill subdirectory exists, use it as project root (nested project structure)
if (Test-Path (Join-Path $ProjectRoot "PortKill\PortKill")) {
    $ProjectRoot = Join-Path $ProjectRoot "PortKill"
}

Write-Host "Project root: $ProjectRoot" -ForegroundColor Cyan

# Find makeappx.exe
if (-not $MakeAppxPath) {
    $sdkBase = "C:\Program Files (x86)\Windows Kits\10\bin"
    $searchPatterns = @(
        (Join-Path $sdkBase "*\x64\makeappx.exe"),
        (Join-Path $sdkBase "10.*\x64\makeappx.exe")
    )
    
    foreach ($pattern in $searchPatterns) {
        $makeappx = Get-ChildItem $pattern -Recurse -ErrorAction SilentlyContinue | 
            Sort-Object Name -Descending | 
            Select-Object -First 1
        
        if ($makeappx) { break }
    }
    
    if (-not $makeappx) {
        Write-Error "makeappx.exe not found. Install Windows SDK."
        exit 1
    }
    $MakeAppxPath = $makeappx.FullName
}

Write-Host "Using makeappx: $MakeAppxPath" -ForegroundColor Cyan

# Find MSIX if not specified
if (-not $MsixPath) {
    $msixDir = Join-Path $ProjectRoot "PortKill\AppPackages\$Architecture"
    if (Test-Path $msixDir) {
        $msixFiles = Get-ChildItem (Join-Path $msixDir "*.msix") | 
            Sort-Object LastWriteTime -Descending | 
            Select-Object -First 1
        
        if ($msixFiles) {
            $MsixPath = $msixFiles.FullName
        }
    }
}

if (-not $MsixPath) {
    Write-Error "MSIX not found. Use -MsixPath to specify."
    exit 1
}

Write-Host "MSIX: $MsixPath" -ForegroundColor Yellow

# Output directory
if (-not $OutputDir) {
    $OutputDir = Join-Path (Split-Path $MsixPath -Parent) "unpacked"
}

if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

# Unpack
& $MakeAppxPath unpack /p $MsixPath /d $OutputDir

if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to unpack MSIX."
    exit 1
}

Write-Host "Unpacked to: $OutputDir" -ForegroundColor Green

# Check language
Write-Host "`n--- Checking manifest ---" -ForegroundColor Cyan
$manifest = Get-Content (Join-Path $OutputDir "AppxManifest.xml") -Raw

$identity = ([xml]$manifest).Package.Identity
Write-Host "Name: $($identity.Name)" -ForegroundColor White
Write-Host "Version: $($identity.Version)" -ForegroundColor White
Write-Host "Publisher: $($identity.Publisher)" -ForegroundColor White

$resources = ([xml]$manifest).Package.Resources.Resource
Write-Host "Language: $($resources.Language)" -ForegroundColor White