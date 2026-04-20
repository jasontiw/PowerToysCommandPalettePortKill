<#
.SYNOPSIS
    Verifies the digital signature of an MSIX package.
.DESCRIPTION
    Uses signtool to verify the MSIX signature.
    Uses dynamic paths based on script location.
.PARAMETER MsixPath
    Path to MSIX file. Default: auto-detect from AppPackages.
.PARAMETER SignToolPath
    Path to signtool.exe. Default: auto-discover.
#>

param(
    [Parameter()]
    [string]$MsixPath,
    
    [Parameter()]
    [string]$SignToolPath,
    
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

# Find signtool.exe
if (-not $SignToolPath) {
    $sdkBase = "C:\Program Files (x86)\Windows Kits\10\bin"
    $searchPatterns = @(
        (Join-Path $sdkBase "*\x64\signtool.exe"),
        (Join-Path $sdkBase "10.*\x64\signtool.exe")
    )
    
    $signtool = $null
    foreach ($pattern in $searchPatterns) {
        $signtool = Get-ChildItem $pattern -Recurse -ErrorAction SilentlyContinue | 
            Sort-Object Name -Descending | 
            Select-Object -First 1
        
        if ($signtool) { break }
    }
    
    if (-not $signtool) {
        Write-Error "signtool.exe not found. Install Windows SDK."
        exit 1
    }
    $SignToolPath = $signtool.FullName
}

Write-Host "Using signtool: $SignToolPath" -ForegroundColor Cyan

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

if (-not (Test-Path $MsixPath)) {
    Write-Error "MSIX file not found: $MsixPath"
    exit 1
}

Write-Host "MSIX: $MsixPath" -ForegroundColor Yellow

# Verify signature
Write-Host "`nVerifying signature..." -ForegroundColor Yellow
& $SignToolPath verify /pa /v $MsixPath

if ($LASTEXITCODE -ne 0) {
    Write-Host "Verification FAILED" -ForegroundColor Red
    exit 1
}

Write-Host "`nVerification complete!" -ForegroundColor Green