<#
.SYNOPSIS
    Detects the project root directory dynamically.
.DESCRIPTION
    Returns the project root directory based on where this script is located.
    Supports scripts in project root or in subdirectories (e.g., scripts/).
#>

function Get-ProjectRoot {
    [CmdletBinding()]
    param(
        # Optional path override (useful for testing)
        [Parameter()]
        [string]$ScriptPath = $PSScriptRoot
    )

    if ([string]::IsNullOrEmpty($ScriptPath)) {
        throw "Cannot determine project root: $PSScriptRoot is empty"
    }

    # If script is in subdirectory (e.g., scripts/), go up one level
    $testRoot = $ScriptPath
    
    # Check if PortKill subdirectory exists - if so, we're likely at project root
    if (Test-Path (Join-Path $ScriptPath "PortKill")) {
        return $ScriptPath
    }
    
    # Try parent directory
    $parent = Split-Path $ScriptPath -Parent
    if ($parent -and (Test-Path (Join-Path $parent "PortKill"))) {
        return $parent
    }
    
    # Fallback: assume script path is project root
    return $ScriptPath
}

# Export for use as module
Export-ModuleMember -Function Get-ProjectRoot