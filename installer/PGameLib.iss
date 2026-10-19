; Inno Setup script for PGameLib
; -------------------------------------
; Build the app first (build.ps1 produces dist\portable), then compile this script with
; Inno Setup 6 (https://jrsoftware.org/isdl.php):
;
;     "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" installer\PGameLib.iss
;
; It bundles the self-contained portable build, so the installed app needs no .NET runtime.
; Installs per-user (no administrator rights required).

#define AppName "PGameLib"
#define AppVersion "1.0.0"
#define AppPublisher "PGameLib"
#define AppExe "PGameLib.exe"

[Setup]
AppId={{B6F2B0E1-7C4A-4E2F-9A3D-1C2E5A7D9F10}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#AppPublisher}
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes
; Per-user install → no admin prompt
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
OutputDir=..\dist\installer
OutputBaseFilename=PGameLib-Setup-{#AppVersion}
SetupIconFile=..\src\PGameLib\Assets\app.ico
UninstallDisplayIcon={app}\{#AppExe}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a &desktop shortcut"; GroupDescription: "Additional shortcuts:"

[Files]
; Everything produced by the portable publish (dist\portable).
Source: "..\dist\portable\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\{#AppExe}"
Name: "{group}\Uninstall {#AppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\{#AppExe}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#AppExe}"; Description: "Launch {#AppName}"; Flags: nowait postinstall skipifsilent

; Note: the uninstaller removes the installed program files only. The user's library
; (%AppData%\PGameLib) is intentionally left in place so re-installing keeps the library.
