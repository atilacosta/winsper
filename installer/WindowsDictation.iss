#define AppName "Windows Dictation"
#define AppVersion "0.1.0"
#define Publisher "Windows Dictation"
#define PublishDir "..\artifacts\publish\WindowsDictation"

[Setup]
AppId={{F4F224D5-55F6-4060-8AE2-2D344A8124F0}
AppName={#AppName}
AppVersion={#AppVersion}
AppPublisher={#Publisher}
DefaultDirName={autopf}\Windows Dictation
DefaultGroupName=Windows Dictation
DisableProgramGroupPage=yes
OutputDir=..\artifacts\installer
OutputBaseFilename=WindowsDictationSetup-{#AppVersion}
Compression=lzma2
SolidCompression=yes
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64
PrivilegesRequired=lowest
UninstallDisplayIcon={app}\WindowsDictation.exe
; Configure a SignTool entry in Inno Setup, then uncomment the next lines for signed releases.
; SignTool=signtool
; SignedUninstaller=yes

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\Windows Dictation"; Filename: "{app}\WindowsDictation.exe"

[Run]
Filename: "{app}\WindowsDictation.exe"; Description: "Launch Windows Dictation"; Flags: nowait postinstall skipifsilent
