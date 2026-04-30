# PortKill - PowerToys Command Palette Extension

<a href="https://apps.microsoft.com/detail/9nmfqzvgr1dh?hl=en-us&gl=CO">
  <img src="https://get.microsoft.com/images/en-us%20dark.svg" width="150" alt="Get PortKill from Microsoft Store"/>
</a>

![Port Kill Demo](docs/App-UI.gif)

Quickly find and kill processes blocking TCP ports on Windows.

## Requirements

- Windows 10 version 2004 (build 19041) or later
- PowerToys with Command Palette enabled
- Developer Mode enabled in Windows Settings

## Quick Start

1. Open `PortKill/PortKill.sln` in Visual Studio
2. Select `Debug | x64` configuration
3. **Build > Deploy** (F5) - Deployment is required, not just build
4. Press **Win + Alt + Space** and type "Reload" to load the extension

## Usage

- **Win + Alt + Space** - Open Command Palette
- Type "Port Kill" to see available commands

## Local Build (Testing)

### Generate Signing Certificate

```powershell
cd PortKill

# Create and export certificate
..\export-cert.ps1 -Password "Publisher2026!"

# Install to Trusted People store
..\install-cert.ps1 -Password "Publisher2026!"
```

### Build MSIX

```powershell
cd PortKill/PortKill
.\build-msix.ps1 -Version "0.0.3.0" -CertPath "../PublisherCert.pfx" -CertPass "Publisher2026!"
```

Output in: `AppPackages/`

## Troubleshooting

- **Extension not showing**: Make sure you deployed (not just built), then type "Reload" in Command Palette
- **Can't kill process**: Some require admin privileges
- **VS Deploy error DEP1700**: Run as Administrator

## License

MIT License