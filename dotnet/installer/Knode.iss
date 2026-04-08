; Knode Windows installer (Inno Setup 6+)
; Run from repo: powershell -File dotnet/build-installer.ps1

#define MyAppName "Knode"
#define MyAppVersion "0.2.0"
#define MyAppPublisher "KindleNotesAgent"
#define MyAppExeName "Knode.exe"
#define PublishDir "..\publish"

[Setup]
AppId={{B8F3E2C1-4A9D-4F6E-9C2B-1E7D0A5F3C88}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
OutputDir=..\dist-installer
OutputBaseFilename=KnodeSetup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
; x64compatible avoids deprecated plain x64 identifier (Inno Setup 6.2+)
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "Launch Knode"; Flags: nowait postinstall skipifsilent
