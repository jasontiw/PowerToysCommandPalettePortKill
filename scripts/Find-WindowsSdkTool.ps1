<#
.SYNOPSIS
    Finds Windows SDK tools (makeappx.exe, signtool.exe, etc.)
.DESCRIPTION
    Searches for Windows SDK tools in standard installation locations.
    Returns the most recent version found by default.
#>

function Find-WindowsSdkTool {
    [CmdletBinding()]
    param(
        # Tool name to find (e.g., "makeappx.exe", "signtool.exe")
        [Parameter(Mandatory = $true)]
        [string]$ToolName,
        
        # Specific version to use (optional)
        [Parameter()]
        [string]$Version,
        
        # Architecture
        [Parameter()]
        [ValidateSet("x64", "x86", "arm64")]
        [string]$Architecture = "x64"
    )

    $sdkBase = "C:\Program Files (x86)\Windows Kits\10\bin"
    
    if ($Version) {
        # Use specific version
        $toolPath = Join-Path $sdkBase "$Version\$Architecture\$ToolName"
        if (Test-Path $toolPath) {
            return Get-Item $toolPath
        }
        Write-Warning "Specified version $Version not found, searching for alternatives..."
    }
    
    # Search patterns
    $searchPatterns = @(
        (Join-Path $sdkBase "*\$Architecture\$ToolName"),
        (Join-Path $sdkBase "10.*\$Architecture\$ToolName")
    )
    
    $foundTool = $null
    
    foreach ($pattern in $searchPatterns) {
        $candidates = Get-ChildItem $pattern -Recurse -ErrorAction SilentlyContinue | 
            Sort-Object Name -Descending | 
            Select-Object -First 1
        
        if ($candidates) {
            $foundTool = $candidates
            break
        }
    }
    
    if (-not $foundTool) {
        throw "Windows SDK tool '$ToolName' not found. Install Windows SDK."
    }
    
    Write-Verbose "Found: $($foundTool.FullName)"
    return $foundTool
}

# Export for use as module
Export-ModuleMember -Function Find-WindowsSdkTool