<#
.SYNOPSIS
    Exports or creates a code signing certificate.
.DESCRIPTION
    Exports existing certificate or creates new one.
    Uses dynamic paths based on script location.
.PARAMETER Subject
    Certificate subject. Default: CN=4C4372AF-9C35-4D16-BA6B-7FD6D3CB0870.
.PARAMETER OutputPath
    Output path for PFX. Default: auto-detect.
.PARAMETER Password
    Password for the exported PFX.
#>

param(
    [Parameter()]
    [string]$Subject = "CN=4C4372AF-9C35-4D16-BA6B-7FD6D3CB0870",
    
    [Parameter()]
    [string]$OutputPath,
    
    [Parameter()]
    [string]$Password
)

$ErrorActionPreference = "Stop"

# Detect project root
# If we're in scripts subdirectory, go up one level
$ProjectRoot = $PSScriptRoot
if ($ProjectRoot -match "[\\/]scripts$") {
    $ProjectRoot = Split-Path $PSScriptRoot -Parent
}

Write-Host "Project root: $ProjectRoot" -ForegroundColor Cyan

# Default output path
if (-not $OutputPath) {
    # Output to PortKill/PublisherCert.pfx (relative to project root)
    # build-msix.ps1 expects ../PublisherCert.pfx from PortKill/PortKill/
    $OutputPath = Join-Path $ProjectRoot "PortKill\PublisherCert.pfx"
}

$OutputDir = Split-Path $OutputPath -Parent
if ($OutputDir -and -not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

# Get password
if (-not $Password) {
    Write-Host "Enter password for certificate:" -ForegroundColor Yellow
    $secure = Read-Host -AsSecureString
    $bstr = [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($secure)
    $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto($bstr)
    [System.Runtime.InteropServices.Marshal]::ZeroFreeBSTR($bstr)
}

$securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText

# Try to find existing certificate
$cert = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert -ErrorAction SilentlyContinue | 
    Where-Object { $_.Subject -eq $Subject }

if (-not $cert) {
    Write-Host "Certificate not found. Creating new..." -ForegroundColor Yellow
    
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $Subject -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(5)
    
    if (-not $cert) {
        Write-Error "Failed to create certificate"
        exit 1
    }
    
    Write-Host "Created certificate with thumbprint: $($cert.Thumbprint)" -ForegroundColor Cyan
}

Write-Host "Exporting to: $OutputPath" -ForegroundColor Yellow

# Export
Export-PfxCertificate -Cert $cert -FilePath $OutputPath -Password $securePassword | Out-Null

Write-Host "Certificate exported successfully!" -ForegroundColor Green