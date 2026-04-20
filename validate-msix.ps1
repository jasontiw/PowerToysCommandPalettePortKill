param(
    [string]$MsixPath = "PortKill\PortKill\AppPackages\x64\PortKill_0.0.2.0_x64.msix",
    [string]$OutputDir = "PortKill\PortKill\AppPackages\x64\unpacked"
)

$makeappx = $null
$searchPaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.*\x64\makeappx.exe"
)

foreach ($path in $searchPaths) {
    $makeappx = Get-ChildItem $path -Recurse -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
    if ($makeappx) { break }
}

if (-not $makeappx) {
    Write-Error "makeappx.exe not found. Install Windows SDK."
    exit 1
}

Write-Host "Using: $($makeappx.FullName)" -ForegroundColor Cyan
Write-Host "MSIX: $MsixPath" -ForegroundColor Yellow

if (-not (Test-Path $MsixPath)) {
    Write-Error "MSIX file not found: $MsixPath"
    exit 1
}

if (Test-Path $OutputDir) {
    Remove-Item $OutputDir -Recurse -Force
}
New-Item -ItemType Directory -Path $OutputDir -Force | Out-Null

& $makeappx.FullName unpack /p $MsixPath /d $OutputDir

Write-Host "Unpacked to: $OutputDir" -ForegroundColor Green