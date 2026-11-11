; AltHub - Inno Setup script (for a real .exe installer, like RoALT ships).
; Build it with Inno Setup 6+ :   ISCC.exe installer\AltHub.iss
; It expects a prebuilt AltHub.exe in the project root (run Build.bat first).
; Output: installer\Output\AltHub-Setup.exe  (per-user, no admin needed).

#define AppName "AltHub"
#define AppVersion "1.3.8"
#define AppExe "AltHub.exe"

[Setup]
AppId={{7D3E5A1C-4B2F-4E8A-9C6D-A17B0C3E9F52}}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher=AltHub
DefaultDirName={localappdata}\Programs\AltHub
DefaultGroupName=AltHub
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
OutputDir=Output
OutputBaseFilename=AltHub-Setup
SetupIconFile=..\app.ico
UninstallDisplayIcon={app}\app.ico
Compression=lzma2
SolidCompression=yes
WizardStyle=modern

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Shortcuts:"

[Files]
Source: "..\AltHub.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\app.ico";           DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md";         DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist
Source: "..\LICENSE";           DestDir: "{app}"; Flags: ignoreversion skipifsourcedoesntexist

[Icons]
Name: "{group}\AltHub"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\app.ico"
Name: "{userdesktop}\AltHub"; Filename: "{app}\{#AppExe}"; IconFilename: "{app}\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Start AltHub"; Flags: nowait postinstall skipifsilent
