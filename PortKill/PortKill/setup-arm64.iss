; Inno Setup Script for Command Palette Extensions

#define AppVersion "0.0.1.0"

[Setup]
AppId={{f1172d0c-c4f4-4a1a-9560-e949e8993e17}}
AppName=PortKill
AppVersion={#AppVersion}
AppPublisher=Jason
DefaultDirName={autopf}\PortKill
OutputDir=bin\Release\installer
OutputBaseFilename=PortKill-Setup-{#AppVersion}-arm64
Compression=lzma
SolidCompression=yes
ArchitecturesAllowed=arm64
ArchitecturesInstallIn64BitMode=arm64
MinVersion=10.0.19041

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "bin\Release\win-arm64\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\PortKill"; Filename: "{app}\PortKill.exe"

[Registry]
Root: HKCU; Subkey: "SOFTWARE\Classes\CLSID\{{f1172d0c-c4f4-4a1a-9560-e949e8993e17}}"; ValueData: "PortKill"
Root: HKCU; Subkey: "SOFTWARE\Classes\CLSID\{{f1172d0c-c4f4-4a1a-9560-e949e8993e17}}\LocalServer32"; ValueData: "{app}\PortKill.exe -RegisterProcessAsComServer"

