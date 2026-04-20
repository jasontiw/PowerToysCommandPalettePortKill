param(
    [string]$Subject = "CN=JasonTiw",
    [string]$OutputPath = "PortKill\PortKill\JasonTiw.pfx",
    [string]$Password = "YourPassword"
)

$securePassword = ConvertTo-SecureString -String $Password -Force -AsPlainText

$cert = Get-ChildItem -Path Cert:\CurrentUser\My -CodeSigningCert | Where-Object { $_.Subject -eq $Subject }

if (-not $cert) {
    Write-Error "Certificate not found: $Subject"
    Write-Host "Creating new certificate..." -ForegroundColor Yellow
    
    $cert = New-SelfSignedCertificate -Type CodeSigningCert -Subject $Subject -CertStoreLocation "Cert:\CurrentUser\My" -NotAfter (Get-Date).AddYears(5)
    Write-Host "Created certificate with thumbprint: $($cert.Thumbprint)" -ForegroundColor Cyan
}

$OutputDir = Split-Path -Parent $OutputPath
if ($OutputDir -and -not (Test-Path $OutputDir)) {
    New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null
}

Export-PfxCertificate -Cert $cert -FilePath $OutputPath -Password $securePassword | Out-Null

Write-Host "Exported to: $OutputPath" -ForegroundColor Green