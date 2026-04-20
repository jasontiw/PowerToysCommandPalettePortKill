$makeappx = "C:\Program Files (x86)\Windows Kits\10\bin\10.0.26100.0\x64\makeappx.exe"
$msix = "D:\Projects\PowerToysCommandPalettePortKill\PortKill\AppPackages\x64\PortKill_0.0.2.0_x64.msix"
$out = "D:\Projects\PowerToysCommandPalettePortKill\PortKill\AppPackages\x64\unpacked"

& $makeappx unpack /p $msix /d $out
Write-Host "--- Checking language in MSIX ---"
Get-Content "$out\AppxManifest.xml" | Select-String "Language"