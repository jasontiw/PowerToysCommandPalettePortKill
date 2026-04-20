<#
.SYNOPSIS
    Installs the code signing certificate to TrustedPeople store.
.DESCRIPTION
    Imports the PFX certificate for local MSIX signing.
    Uses dynamic path to find the certificate.
.PARAMETER CertPath
    Path to PFX certificate. Default: auto-detect *.pfx in project.
.PARAMETER Password
    Password for the PFX file.
#>

param(
    [Parameter()]
    [string]$CertPath,
    
    [Parameter()]
    [string]$Password
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

# Find certificate if not specified
if (-not $CertPath) {
    # Search in PortKill subdirectory
    $searchPaths = @(
        (Join-Path $ProjectRoot "PortKill\*.pfx"),
        (Join-Path $ProjectRoot "*.pfx")
    )
    
    foreach ($pattern in $searchPaths) {
        $certs = Get-ChildItem $pattern -ErrorAction SilentlyContinue | 
            Select-Object -First 1
        
        if ($certs) {
            $CertPath = $certs.FullName
            break
        }
    }
}

if (-not $CertPath) {
    Write-Error "Certificate not found. Use -CertPath to specify."
    exit 1
}

if (-not (Test-Path $CertPath)) {
    Write-Error "Certificate not found: $CertPath"
    exit 1
}

Write-Host "Certificate: $CertPath" -ForegroundColor Cyan

# Get password
if (-not $Password) {
    Write-Host "Enter certificate password:" -ForegroundColor Yellow
    $secure = Read-Host -AsSecureString
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

$securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText

# Import
Write-Host "Installing to TrustedPeople store..." -ForegroundColor Yellow
Import-PfxCertificate -FilePath $CertPath -CertStoreLocation "Cert:\LocalMachine\TrustedPeople" -Password $securePassword

Write-Host "Certificate installed successfully!" -ForegroundColor Green