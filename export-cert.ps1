<#
.SYNOPSIS
    Exports or creates a code signing certificate.
.DESCRIPTION
    Exports existing certificate or creates new one.
    Uses dynamic paths based on script location.
.PARAMETER Subject
    Certificate subject. Default: CN=JasonTiw.
.PARAMETER OutputPath
    Output path for PFX. Default: auto-detect.
.PARAMETER Password
    Password for the exported PFX.
#>

param(
    [Parameter()]
    [string]$Subject = "CN=JasonTiw",
    
    [Parameter()]
    [string]$OutputPath,
    
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

# Default output path
if (-not $OutputPath) {
    $OutputPath = Join-Path $ProjectRoot "PortKill\$Subject.pfx"
    $OutputPath = $OutputPath -replace "=", ""  # Remove = from CN
    $OutputPath = $OutputPath -replace ",", ""
    $OutputPath = $OutputPath -replace " ", "_"
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