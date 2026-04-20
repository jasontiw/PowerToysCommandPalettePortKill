param(
    [string]$MsixPath = "PortKill\PortKill\AppPackages\x64\PortKill_0.0.2.0_x64.msix"
)

$signtool = $null
$searchPaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\signtool.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.*\x64\signtool.exe"
)

foreach ($path in $searchPaths) {
    $signtool = Get-ChildItem $path -Recurse -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
    if ($signtool) { break }
}

if (-not $signtool) {
    Write-Error "signtool.exe not found. Install Windows SDK."
    exit 1
}

Write-Host "Using: $($signtool.FullName)" -ForegroundColor Cyan
Write-Host "MSIX: $MsixPath" -ForegroundColor Yellow

if (-not (Test-Path $MsixPath)) {
    Write-Error "MSIX file not found: $MsixPath"
    exit 1
}

& $signtool.FullName verify /pa /v $MsixPath

Write-Host "Verification complete" -ForegroundColor Green