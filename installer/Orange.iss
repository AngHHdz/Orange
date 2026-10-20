; Orange installer (Inno Setup 6). Build it with ..\build.ps1, which publishes the app first.
;
; Installs per user (no admin prompt) into %LOCALAPPDATA%\Programs\Orange. That matters:
; Orange must run as a normal user, or Windows blocks drag & drop from Explorer into it.

#ifndef MyAppVersion
  #define MyAppVersion "1.0.0"
#endif
#define MyAppName "Orange"
#define MyAppExe "Orange.exe"
#define SourceDir "..\publish"

[Setup]
AppId={{6B2E1F4A-9C3D-4E8B-A7F2-5D1C0E9B3A64}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=koko
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
DisableDirPage=auto
PrivilegesRequired=lowest
OutputDir=..\dist
OutputBaseFilename=Orange-Setup-{#MyAppVersion}
SetupIconFile=..\Orange\Assets\orange.ico
UninstallDisplayIcon={app}\{#MyAppExe}
UninstallDisplayName={#MyAppName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
; Close a running Orange before replacing its files.
CloseApplications=force
RestartApplications=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "spanish"; MessagesFile: "compiler:Languages\Spanish.isl"
Name: "portuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[CustomMessages]
english.AutoStart=Start Orange when Windows starts
spanish.AutoStart=Iniciar Orange con Windows
portuguese.AutoStart=Iniciar o Orange com o Windows

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"
Name: "autostart"; Description: "{cm:AutoStart}"

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[InstallDelete]
; Shortcut left by the development build.
Type: files; Name: "{userprograms}\Orange.lnk"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; Tasks: desktopicon

[Registry]
; Same value Orange's own "Start with Windows" switch uses, so both stay in sync.
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueType: string; ValueName: "Orange"; \
  ValueData: """{app}\{#MyAppExe}"""; Flags: uninsdeletevalue; Tasks: autostart
Root: HKCU; Subkey: "Software\Microsoft\Windows\CurrentVersion\Run"; ValueName: "Orange"; \
  Flags: deletevalue; Tasks: not autostart

[Run]
Filename: "{app}\{#MyAppExe}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent

[UninstallRun]
Filename: "{sys}\taskkill.exe"; Parameters: "/im {#MyAppExe} /f"; Flags: runhidden; RunOnceId: "StopOrange"

; Settings, logs and the phone key in %LOCALAPPDATA%\Orange are kept, so a reinstall remembers them.
