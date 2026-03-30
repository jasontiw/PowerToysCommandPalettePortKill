# Port Kill - PowerToys Command Palette Extension

A Command Palette extension for Microsoft PowerToys that allows developers to quickly find and kill processes blocking TCP ports on Windows.

## Features

- **List Active Ports** - View all TCP/UDP ports currently in use with process information, and kill any process directly
- **Kill by Process Name** - Kill all processes matching a name (e.g., node, python)
- **Common ports** - Quick view of frequently used development ports (3000, 4200, 5000, 5173, 8000, 8080, 9000)
- **Confirmation Dialog** - Shows process details (PID, memory, path, start time) before killing
- **System Process Protection** - Prevents accidental killing of critical system processes
- **Dock Integration** - Shows port status in the PowerToys Dock with visual indicators (✓ free, ✗ occupied)

## Requirements

- Windows 11 (22H2+)
- PowerToys with Command Palette enabled
- Visual Studio 2022+ with:
  - .NET desktop development workload
  - Windows application development workload (WinUI)
- Developer Mode enabled in Windows Settings

## Quick Start

### Prerequisites

1. **Install PowerToys** from Microsoft Store or GitHub
2. **Enable Developer Mode**:
   - Open Settings > Privacy & security > For developers
   - Enable "Developer Mode"
3. **Install Visual Studio 2022+** with required workloads

### Building and Deploying

1. Open the solution in Visual Studio:
   ```
   cd PortKill
   start PortKill.sln
   ```

2. Select your target platform:
   - `x64` for 64-bit Windows
   - `ARM64` for ARM devices

3. **Deploy** the extension (not just build):
   - Go to **Build** > **Deploy PortKill**
   - Or press `Ctrl + B` then confirm deployment

   > **Important**: Building alone (`Ctrl + Shift + B`) is not enough. You must deploy for PowerToys to detect the extension.

4. Reload Command Palette:
   - Open Command Palette: **Win + Alt + Space**
   - Type "Reload" and select "Reload Command Palette Extension"

5. Find the extension:
   - Type "Port Kill" in the Command Palette
   - You'll see the main menu with options

## Usage

### Via Command Palette

1. Press **Win + Alt + Space** to open Command Palette
2. Type "Port Kill" to see available commands:
   - **List active ports** - Shows all ports in use, allows killing any process
   - **Common ports** - Quick view of dev ports

### Via Dock

The Dock shows common development ports at the bottom of your screen:
- **✓** (green) - Port is free
- **✗** (red) - Port is occupied (click to kill)

## Project Structure

```
PortKill/
├── PortKill.sln
├── Directory.Build.props
├── Directory.Packages.props
├── nuget.config
└── PortKill/
    ├── Program.cs                    # Entry point
    ├── PortKill.cs                   # Extension implementation
    ├── PortKillCommandsProvider.cs    # Command provider
    ├── PortKill.csproj
    ├── app.manifest
    ├── Package.appxmanifest
    ├── Assets/                       # Icons and images
    ├── Commands/
    │   ├── KillProcessCommand.cs     # Kill process logic
    │   └── NoOpCommand.cs            # Placeholder command
    ├── Dock/
    │   └── PortKillDockBand.cs       # Dock integration
    ├── Models/
    │   ├── KillResult.cs              # Kill operation results
    │   ├── PortInfo.cs                # Port data model
    │   ├── PortProcessEntry.cs        # Combined port+process
    │   └── ProcessInfo.cs            # Process data model
    ├── Pages/
    │   ├── CommonDevPortsPage.cs      # Common ports view
    │   ├── ConfirmKillPage.cs         # Kill confirmation
    │   ├── KillPortPage.cs            # Kill by port
    │   ├── ListPortsPage.cs           # List all ports
    │   └── PortKillPage.cs            # Main menu
    └── Services/
        └── PortService.cs             # Core port/process logic
```

## How It Works

### Port Detection
Uses `netstat -ano` to get accurate PID-to-port mapping, which is more reliable than .NET APIs for this use case.

### Process Information
Retrieves process details including:
- Process name
- Memory usage (Working Set)
- Start time
- Executable path

### System Protection
Prevents killing critical system processes like:
- System, csrss, wininit, lsass, services
- PID 0 (Idle) and PID 4 (System)

## Development

### Making Changes

1. Make your code changes
2. **Deploy** again (Build > Deploy)
3. Reload Command Palette (type "Reload")
4. Test your changes

### Troubleshooting

**Extension not showing up:**
- Make sure you deployed, not just built
- Try reloading: type "Reload" in Command Palette
- Check Developer Mode is enabled

**Can't kill process:**
- Some processes require admin privileges
- System processes are protected by design

**Ports not updating:**
- Click refresh in the Dock
- Reopen the port list in Command Palette

## References

- [PowerToys Command Palette Documentation](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/)
- [Creating Extensions](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/creating-an-extension)
- [Extension Samples](https://learn.microsoft.com/en-us/windows/powertoys/command-palette/samples)
- [PowerToys GitHub](https://github.com/microsoft/PowerToys)

## Publishing to WinGet

This extension can be distributed via WinGet. WinGet enables automatic discovery and installation directly within Command Palette.

### Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Inno Setup 6](https://jrsoftware.org/isdl.php) (for local builds)
- GitHub CLI (`gh`)
- [WingetCreate](https://github.com/microsoft/wingetcreate)

### Local Build (Testing)

To build installers locally:

```powershell
cd PortKill/PortKill
.\build-exe.ps1 -Version "0.0.1.0"
```

This creates:
- `bin\Release\installer\PortKill-Setup-0.0.1.0-x64.exe` (Intel/AMD)
- `bin\Release\installer\PortKill-Setup-0.0.1.0-arm64.exe` (ARM)

### Automated Build with GitHub Actions

The repository includes `.github/workflows/release-extension.yml` that automatically:
1. Builds for x64 and ARM64
2. Creates installers using Inno Setup
3. Publishes a GitHub Release with the installers

**To trigger a release:**

```powershell
gh workflow run release-extension.yml -f "release_notes=Your release notes here"
```

Or manually via GitHub: Actions > Release Extension > Run workflow

### Submitting to WinGet

#### First Submission (Manual)

1. Create a GitHub Release with your installers
2. Run WingetCreate:

```powershell
wingetcreate new "path/to/PortKill-Setup-0.0.1.0-x64.exe" "path/to/PortKill-Setup-0.0.1.0-arm64.exe"
```

3. When prompted:
   - Press Enter to accept the suggested values
   - Answer "No" to optional modifications
   - Answer "Yes" to submit to WinGet

#### Subsequent Updates

Use the included `update-winget.yml` workflow or:

```powershell
wingetcreate update Your.PackageIdentityName `
  --version 0.0.2.0 `
  --urls "https://github.com/yourrepo/releases/download/v0.0.2.0/PortKill-Setup-0.0.2.0-x64.exe|x64" "https://github.com/yourrepo/releases/download/v0.0.2.0/PortKill-Setup-0.0.2.0-arm64.exe|arm64" `
  --token YOUR_GITHUB_TOKEN \
  --submit
```

### WinGet Manifest Requirements

Your manifest must include:
- `windows-commandpalette-extension` tag for discovery
- WindowsAppRuntime as a dependency (if using Windows App SDK)

## License

MIT License - See LICENSE file for details.

## Acknowledgments

Inspired by [port-kill](https://github.com/treadiehq/port-kill) - A Rust CLI tool with similar functionality.
