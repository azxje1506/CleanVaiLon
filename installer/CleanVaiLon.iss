; Inno Setup script for Clean VaiLon.
; Build with tools\Build-Release.ps1, which passes the version read from CleanVaiLon.ps1:
;   ISCC.exe /DMyAppVersion=1.3.0 installer\CleanVaiLon.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#define MyAppName "Clean VaiLon"
#define MyAppUrl "https://github.com/azxje1506/CleanVaiLon"
#define PowerShell "{sys}\WindowsPowerShell\v1.0\powershell.exe"

[Setup]
AppId={{85790FCE-9FC2-4EEC-AA1B-EB6F146A44FC}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=azxje1506
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases
; Per-user install by default (no admin needed for setup); the app asks for admin itself when it starts.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\CleanVaiLon
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
SetupIconFile=..\CleanVaiLon.ico
UninstallDisplayIcon={app}\CleanVaiLon.ico
UninstallDisplayName={#MyAppName}
OutputDir=..\dist
OutputBaseFilename=CleanVaiLon-Setup-{#MyAppVersion}
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
MinVersion=10.0
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

[Languages]
Name: "en"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
Source: "..\CleanVaiLon.ps1"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\CleanVaiLon.bat"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\CleanVaiLon.ico"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{#PowerShell}"; Parameters: "-NoProfile -ExecutionPolicy Bypass -STA -File ""{app}\CleanVaiLon.ps1"""; WorkingDir: "{app}"; IconFilename: "{app}\CleanVaiLon.ico"; Comment: "Windows checkup and cleanup"; Flags: runminimized
Name: "{autodesktop}\{#MyAppName}"; Filename: "{#PowerShell}"; Parameters: "-NoProfile -ExecutionPolicy Bypass -STA -File ""{app}\CleanVaiLon.ps1"""; WorkingDir: "{app}"; IconFilename: "{app}\CleanVaiLon.ico"; Comment: "Windows checkup and cleanup"; Flags: runminimized; Tasks: desktopicon

[Run]
Filename: "{#PowerShell}"; Parameters: "-NoProfile -ExecutionPolicy Bypass -STA -File ""{app}\CleanVaiLon.ps1"""; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent runminimized

[UninstallRun]
; Remove the "start with Windows" logon task if the user turned it on in Settings.
Filename: "{sys}\schtasks.exe"; Parameters: "/Delete /TN CleanVaiLon /F"; Flags: runhidden; RunOnceId: "RemoveLogonTask"

[Code]
// After uninstalling, offer to remove settings and the change log as well.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataDir: string;
begin
  if CurUninstallStep <> usPostUninstall then exit;
  DataDir := ExpandConstant('{localappdata}\CleanVaiLon');
  if not DirExists(DataDir) then exit;
  if UninstallSilent then exit;
  if MsgBox('Also remove Clean VaiLon settings and change log?' + #13#10 + DataDir, mbConfirmation, MB_YESNO) = IDYES then
    DelTree(DataDir, True, True, True);
end;
