; Inno Setup script for Clean VaiLon.
; Build with tools\Build-Release.ps1, which publishes CleanVaiLon.exe to publish\ and passes the version
; read from Directory.Build.props:
;   dotnet publish src\CleanVaiLon -c Release -o publish
;   ISCC.exe /DMyAppVersion=2.0.0 installer\CleanVaiLon.iss

#ifndef MyAppVersion
  #define MyAppVersion "0.0.0"
#endif
#define MyAppName "Clean VaiLon"
#define MyAppUrl "https://github.com/azxje1506/CleanVaiLon"
#define MyAppExe "CleanVaiLon.exe"

[Setup]
AppId={{85790FCE-9FC2-4EEC-AA1B-EB6F146A44FC}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher=azxje1506
AppPublisherURL={#MyAppUrl}
AppSupportURL={#MyAppUrl}/issues
AppUpdatesURL={#MyAppUrl}/releases
VersionInfoVersion={#MyAppVersion}
; Per-user install by default (no admin needed for setup); the app asks for admin itself when it starts.
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DefaultDirName={autopf}\CleanVaiLon
DisableProgramGroupPage=yes
LicenseFile=..\LICENSE
SetupIconFile=..\CleanVaiLon.ico
UninstallDisplayIcon={app}\{#MyAppExe}
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
Source: "..\publish\{#MyAppExe}"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\LICENSE"; DestDir: "{app}"; Flags: ignoreversion
Source: "..\README.md"; DestDir: "{app}"; Flags: ignoreversion

[InstallDelete]
; Files of the 1.x PowerShell version, replaced by CleanVaiLon.exe.
Type: files; Name: "{app}\CleanVaiLon.ps1"
Type: files; Name: "{app}\CleanVaiLon.bat"
Type: files; Name: "{app}\CleanVaiLon.ico"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; WorkingDir: "{app}"; Comment: "Windows checkup and cleanup"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExe}"; WorkingDir: "{app}"; Comment: "Windows checkup and cleanup"; Tasks: desktopicon

[Run]
; The exe asks for administrator rights itself (app manifest), so it is started through the shell.
Filename: "{app}\{#MyAppExe}"; WorkingDir: "{app}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent shellexec

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
