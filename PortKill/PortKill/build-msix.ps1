param(
    [string]$Version = "0.0.1.0",
    [string[]]$Platforms = @("x64", "arm64"),
    [string]$CertBase64 = $env:CERT_BASE64,
    [string]$CertPassword = $env:CERT_PASSWORD,
    [string]$CertPath,
    [string]$CertPass
)

$ErrorActionPreference = "Stop"

$ExtensionName = "PortKill"
$ProjectDir = $PSScriptRoot

Write-Host "=== Building $ExtensionName MSIX ===" -ForegroundColor Green
Write-Host "Version: $Version" -ForegroundColor Yellow

$ProjectFile = "$ProjectDir\$ExtensionName.csproj"

$makeappx = $null
$signtool = $null
$searchPaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\makeappx.exe",
    "C:\Program Files (x86)\Windows Kits\10\bin\10.*\x64\makeappx.exe"
)

foreach ($path in $searchPaths) {
    $makeappx = Get-ChildItem $path -Recurse -ErrorAction SilentlyContinue | Sort-Object Name -Descending | Select-Object -First 1
    if ($makeappx) {
        $signtool = $makeappx.FullName -replace 'makeappx\.exe', 'signtool.exe'
        break
    }
}

if (-not $makeappx) {
    Write-Error "makeappx.exe not found. Install Windows SDK."
    exit 1
}

Write-Host "Using makeappx: $($makeappx.FullName)" -ForegroundColor Cyan

$AppManifest = "$ProjectDir\app.manifest"
$AppxManifest = "$ProjectDir\Package.appxmanifest"
$AppxManifestBackup = "$ProjectDir\Package.appxmanifest.backup"

if (Test-Path $AppxManifestBackup) {
    Remove-Item $AppxManifestBackup -Force
}
Copy-Item $AppxManifest $AppxManifestBackup

foreach ($Platform in $Platforms) {
    Write-Host "`n=== Building $Platform ===" -ForegroundColor Cyan

    Copy-Item $AppxManifestBackup $AppxManifest -Force

    $content = Get-Content $AppxManifest -Raw
    $arch = if ($Platform -eq "arm64") { "arm64" } else { "x64" }
    # Replace Version and ProcessorArchitecture ONLY in <Identity> element (preserves formatting)
    $content = $content -replace '(<Identity[^>]*?)Version="[^"]*"', "`$1Version=`"$Version`""
    $content = $content -replace '(ProcessorArchitecture=")[^"]*"', "`$1$arch`""
    # Patch Publisher to match your certificate
    $content = $content -replace '(Publisher=")CN=Microsoft Corporation[^"]*"', '$1CN=JasonTiw"'
    # Fix invalid language
    $content = $content -replace 'Language="x-generate"', 'Language="en-us"'
    [System.IO.File]::WriteAllText($AppxManifest, $content, [System.Text.UTF8Encoding]::new($true))

    $runtimeId = "win-$Platform"
    $stagingDir = "$ProjectDir\bin\staging\$Platform"

    if (Test-Path "$ProjectDir\bin") {
        Remove-Item "$ProjectDir\bin" -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

    Write-Host "Publishing for $runtimeId..." -ForegroundColor Yellow

    dotnet publish $ProjectFile `
        --configuration Release `
        -p:RuntimeIdentifier=$runtimeId `
        -p:PublishDir="$stagingDir\" `
        -p:PublishTrimmed=false `
        -p:Version=$Version `
        -p:WindowsPackageType=MSIX `
        -p:EnableMsixTooling=true

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $Platform"
        continue
    }

Copy-Item $AppxManifest "$stagingDir\AppxManifest.xml" -Force

    $assetsSrc = "$ProjectDir\Assets"
    if (-not (Test-Path "$stagingDir\Assets")) {
        Copy-Item $assetsSrc "$stagingDir\Assets" -Recurse -Force
    }

    $content = Get-Content "$stagingDir\AppxManifest.xml" -Raw
    
    # Fix asset paths (add scale suffix)
    $content = $content -replace 'Square150x150Logo="Assets\\[^"]+"', 'Square150x150Logo="Assets\Square150x150Logo.scale-200.png"'
    $content = $content -replace 'Square44x44Logo="Assets\\[^"]+"', 'Square44x44Logo="Assets\Square44x44Logo.scale-200.png"'
    $content = $content -replace 'Wide310x150Logo="Assets\\[^"]+"', 'Wide310x150Logo="Assets\Wide310x150Logo.scale-200.png"'
    $content = $content -replace 'SplashScreen Image="Assets\\[^"]+"', 'SplashScreen Image="Assets\SplashScreen.scale-200.png"'
    
    # Check BEFORE fix
    $beforeLang = ([regex]'(<Resource[^>]*Language=")[^"]+(")').Match($content).Value
    Write-Host "BEFORE language: $beforeLang" -ForegroundColor Yellow
    
    # Fix invalid language - SIMPLE REPLACE
    $content = $content.Replace('Language="x-generate"', 'Language="en-us"')
    
    # Verify AFTER fix
    $afterLang = ([regex]'(<Resource[^>]*Language=")[^"]+(")').Match($content).Value
    Write-Host "AFTER language: $afterLang" -ForegroundColor Green
    
    [System.IO.File]::WriteAllText("$stagingDir\AppxManifest.xml", $content, [System.Text.UTF8Encoding]::new($true))

    $packageDir = "$ProjectDir\AppPackages\$Platform"
    if (Test-Path $packageDir) {
        Remove-Item $packageDir -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $packageDir -Force | Out-Null

    $msixName = "${ExtensionName}_${Version}_${Platform}.msix"
    $msixPath = "$packageDir\$msixName"

    Write-Host "Creating MSIX..." -ForegroundColor Yellow

    & $makeappx pack /d $stagingDir /p $msixPath /nv

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Failed to create MSIX. Exit code: $LASTEXITCODE"
        continue
    }

    if ($CertBase64 -and $CertPassword) {
        Write-Host "Signing MSIX with base64 cert..." -ForegroundColor Yellow

        $tempCert = "$ProjectDir\temp_signing.pfx"
        [System.IO.File]::WriteAllBytes($tempCert, [Convert]::FromBase64String($CertBase64))

        & $signtool sign /fd SHA256 /f $tempCert /p $CertPassword $msixPath

        Remove-Item $tempCert -Force -ErrorAction SilentlyContinue

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Signing failed, but MSIX was created."
        }
    } elseif ($CertPath -and $CertPass) {
        Write-Host "Signing MSIX with cert file..." -ForegroundColor Yellow

        & $signtool sign /fd SHA256 /f $CertPath /p $CertPass $msixPath

        if ($LASTEXITCODE -ne 0) {
            Write-Warning "Signing failed, but MSIX was created."
        }
    } else {
        Write-Host "No certificate provided - MSIX unsigned (set CERT_BASE64/CERT_PASSWORD env vars or -CertPath/-CertPass)" -ForegroundColor Yellow
    }

    $msix = Get-Item $msixPath
    $sizeMB = [math]::Round($msix.Length / 1MB, 2)
    Write-Host "Created: $msixName ($sizeMB MB)" -ForegroundColor Green

    Remove-Item $stagingDir -Recurse -Force -ErrorAction SilentlyContinue
}

Remove-Item $AppxManifestBackup -Force -ErrorAction SilentlyContinue

Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "MSIX files: $ProjectDir\AppPackages\" -ForegroundColor Yellow