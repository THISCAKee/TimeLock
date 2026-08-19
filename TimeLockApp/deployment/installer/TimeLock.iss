#ifndef SourceDir
  #define SourceDir "publish"
#endif

#define MyAppName "TimeLock"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "TimeLock"
#define MyAppExeName "TimeLockApp.exe"

[Setup]
AppId={{B3B7F1C9-3D16-4C5D-A38F-7B7C3D7F5C9A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\TimeLock
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
PrivilegesRequired=admin
Uninstallable=yes
CreateUninstallRegKey=no
UninstallFilesDir={app}\.uninstall
OutputDir=output
OutputBaseFilename=TimeLock-Setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
UninstallDisplayIcon={app}\{#MyAppExeName}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "Create a desktop shortcut"; GroupDescription: "Additional shortcuts:"; Flags: unchecked
Name: "startupicon"; Description: "Start TimeLock automatically when signing in to Windows"; GroupDescription: "Additional startup options:"; Flags: checkedonce

[Files]
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\TimeLock"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\TimeLock"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{commonstartup}\TimeLock"; Filename: "{app}\{#MyAppExeName}"; Tasks: startupicon

[Dirs]
Name: "{app}\.uninstall"; Attribs: hidden

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
