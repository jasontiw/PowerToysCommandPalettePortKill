param(
    [string]$Version = "0.0.2.0",
    [string[]]$Platforms = @("x64", "arm64"),
    [string]$CertBase64 = $env:CERT_BASE64,
    [string]$CertPassword = $env:CERT_PASSWORD,
    [string]$CertPath,
    [string]$CertPass
)

$ErrorActionPreference = "Stop"

$ExtensionName = "PortKill"
$ProjectDir = $PSScriptRoot
$ProjectFile = "$ProjectDir\$ExtensionName.csproj"

# Read publisher and identity from project file
$csprojContent = Get-Content $ProjectFile -Raw
if ($csprojContent -match '<AppxPackagePublisher>([^<]+)</AppxPackagePublisher>') {
    $Publisher = $matches[1]
}
if ($csprojContent -match '<AppxPackageIdentityName>([^<]+)</AppxPackageIdentityName>') {
    $IdentityName = $matches[1]
}

Write-Host "=== Building $ExtensionName MSIX ===" -ForegroundColor Green
Write-Host "Version: $Version" -ForegroundColor Yellow
Write-Host "Identity: $IdentityName" -ForegroundColor Cyan
Write-Host "Publisher: $Publisher" -ForegroundColor Cyan

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

    # Backup and remove AppxManifest so dotnet publish uses only csproj properties
    $AppxManifestBackup = "$ProjectDir\Package.appxmanifest.backup"
    if (Test-Path $AppxManifest) {
        if (Test-Path $AppxManifestBackup) { Remove-Item $AppxManifestBackup -Force }
        Move-Item $AppxManifest $AppxManifestBackup
    }

    $runtimeId = "win-$Platform"
    $stagingDir = "$ProjectDir\bin\staging\$Platform"

    if (Test-Path "$ProjectDir\bin") {
        Remove-Item "$ProjectDir\bin" -Recurse -Force -ErrorAction SilentlyContinue
    }
    New-Item -ItemType Directory -Path $stagingDir -Force | Out-Null

    Write-Host "Publishing for $runtimeId..." -ForegroundColor Yellow

    dotnet publish $ProjectFile `
        --configuration Release `
        --runtime $runtimeId `
        -p:PublishDir="$stagingDir\" `
        -p:PublishTrimmed=false `
        -p:Version=$Version `
        -p:GenerateAppxPackageOnBuild=false `
        -p:EnableMsixTooling=false `
        -p:WindowsPackageType=None `
        -p:AppxPackageIdentityName=$IdentityName `
        -p:AppxPackagePublisher=$Publisher `
        -p:AppxProcessorArchitecture=$Platform `
        -p:TargetPlatform=$Platform

    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $Platform"
        continue
    }

    # Restore AppxManifest after publish (backup exists for next platform)
    if (-not (Test-Path $AppxManifest) -and (Test-Path $AppxManifestBackup)) {
        Move-Item $AppxManifestBackup $AppxManifest
    }

    Copy-Item $AppxManifest "$stagingDir\AppxManifest.xml" -Force

    $assetsSrc = "$ProjectDir\Assets"
    if (-not (Test-Path "$stagingDir\Assets")) {
        Copy-Item $assetsSrc "$stagingDir\Assets" -Recurse -Force
    }

    $content = Get-Content "$stagingDir\AppxManifest.xml" -Raw
    
    # Fix version from parameter (only in Identity element)
    $content = [regex]::Replace($content, '(Identity[^>]*Version=")[^"]+(")', "`${1}$Version`${2}", [System.Text.RegularExpressions.RegexOptions]::IgnoreCase)
    
# Fix asset paths (add scale suffix)
    $content = $content -replace 'Square150x150Logo="Assets\\[^"]+"', 'Square150x150Logo="Assets\Square150x150Logo.scale-200.png"'
    $content = $content -replace 'Square44x44Logo="Assets\\[^"]+"', 'Square44x44Logo="Assets\Square44x44Logo.scale-200.png"'
    $content = $content -replace 'Wide310x150Logo="Assets\\[^"]+"', 'Wide310x150Logo="Assets\Wide310x150Logo.scale-200.png"'
    $content = $content -replace 'SplashScreen Image="Assets\\[^"]+"', 'SplashScreen Image="Assets\SplashScreen.scale-200.png"'
    
    # Check BEFORE fix
    $beforeLang = ([regex]'(<Resource[^>]*Language=")[^"]+(")').Match($content).Value
    Write-Host "BEFORE language: $beforeLang" -ForegroundColor Yellow
    
    # Fix invalid language
    $content = $content.Replace('Language="x-generate"', 'Language="en-us"')
    
    # Fix ProcessorArchitecture - ensure it matches the current platform
    $arch = if ($Platform -eq "arm64") { "arm64" } else { "x64" }
    $content = $content -replace 'ProcessorArchitecture="[^"]*"', "ProcessorArchitecture=""$arch"""
    
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

# === GENERATE BUNDLE ===
Write-Host "`n=== Creating Bundle ===" -ForegroundColor Green

$bundleMapping = "$ProjectDir\bundle_mapping.txt"

# Clean up old bundle files before creating new ones
$oldBundles = Get-ChildItem "$ProjectDir\*.msixbundle" -ErrorAction SilentlyContinue
foreach ($old in $oldBundles) {
    Write-Host "Removing old bundle: $($old.Name)" -ForegroundColor Cyan
    Remove-Item $old.FullName -Force
}

$bundleContent = "[Files]`n"

foreach ($Platform in $Platforms) {
    $msixName = "${ExtensionName}_${Version}_${Platform}.msix"
    $msixSrcPath = "AppPackages\$Platform\$msixName"
    $msixPath = "$ProjectDir\$msixSrcPath"
    if (Test-Path $msixPath) {
        # Formato: "ruta/origen" "nombre_destino"
        $bundleContent += "`"$msixSrcPath`" `"$msixName`"`n"
    }
}

$bundleContent | Set-Content $bundleMapping -Encoding UTF8

# Clean up old bundle mapping files
Get-ChildItem "$ProjectDir\bundle_mapping*.txt" -ErrorAction SilentlyContinue | 
    Where-Object { $_.Name -ne "bundle_mapping.txt" } | 
    ForEach-Object { Remove-Item $_.FullName -Force }

$bundleName = "${ExtensionName}_${Version}_Bundle.msixbundle"
$bundlePath = "$ProjectDir\$bundleName"

# Remove old bundle if exists
if (Test-Path $bundlePath) {
    Remove-Item $bundlePath -Force
}

Write-Host "Creating bundle..." -ForegroundColor Yellow
& $makeappx bundle /f $bundleMapping /p $bundlePath

if ($LASTEXITCODE -ne 0) {
    Write-Warning "Bundle creation failed, but MSIX files were created."
} else {
    $bundle = Get-Item $bundlePath
    $sizeMB = [math]::Round($bundle.Length / 1MB, 2)
    Write-Host "Created: $bundleName ($sizeMB MB)" -ForegroundColor Green
}

Write-Host "`n=== Build Complete ===" -ForegroundColor Green
Write-Host "MSIX files: $ProjectDir\AppPackages\" -ForegroundColor Yellow
Write-Host "Bundle:   $bundlePath" -ForegroundColor Yellow