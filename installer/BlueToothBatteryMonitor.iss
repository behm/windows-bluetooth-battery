; Inno Setup script for Bluetooth Battery Monitor
; Requires Inno Setup 6+ — https://jrsoftware.org/isinfo.php

#define MyAppName      "Bluetooth Battery Monitor"
#define MyAppVersion   "1.0.0"
#define MyAppPublisher "Bluetooth Battery Monitor"
#define MyAppExeName   "BluetoothBatteryMonitor.exe"
#define PublishDir     "..\BluetoothBatteryMonitor\bin\publish"

[Setup]
AppId={{B4E7F2A1-9C3D-4E5F-8A1B-2D6E9F3C7A50}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
OutputDir=..\bin\installer
OutputBaseFilename=BluetoothBatteryMonitor-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
SetupIconFile=..\Assets\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
CloseApplications=yes
RestartApplications=no
; Minimum Windows 10 1809 (build 17763)
MinVersion=10.0.17763

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostart"; Description: "Start automatically with Windows"; GroupDescription: "Other:"

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Registry]
; Auto-start registry entry (same key AutoStartManager uses)
Root: HKCU; Subkey: "SOFTWARE\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "BluetoothBatteryMonitor"; ValueData: """{app}\{#MyAppExeName}"""; Flags: uninsdeletevalue; Tasks: autostart

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{cmd}"; Parameters: "/C taskkill /IM {#MyAppExeName} /F"; Flags: runhidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}\logs"