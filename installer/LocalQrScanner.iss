#ifndef PluginSource
  #define PluginSource "..\Community.PowerToys.Run.Plugin.LocalQrScanner\bin\x64\Release\net9.0-windows10.0.26100.0"
#endif
#ifndef OutputDirectory
  #define OutputDirectory "..\artifacts"
#endif
#ifndef AppVersion
  #define AppVersion "0.1.1"
#endif
#ifndef VersionInfoVersion
  #define VersionInfoVersion "0.1.1.0"
#endif

#define AppName "Local QR Scanner for PowerToys Run"

[Setup]
AppId={{91E4E983-74A4-4831-B98B-34E2A0B329A4}
AppName={#AppName}
AppVerName={#AppName} {#AppVersion}
AppVersion={#AppVersion}
AppPublisher=LocalQrScanner Contributors
DefaultDirName={localappdata}\Microsoft\PowerToys\PowerToys Run\Plugins\LocalQrScanner
DisableDirPage=yes
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
UsePreviousAppDir=no
Uninstallable=yes
UninstallDisplayName={#AppName}
OutputDir={#OutputDirectory}
OutputBaseFilename=LocalQrScanner-Setup-{#AppVersion}-x64
Compression=lzma2/max
SolidCompression=yes
WizardStyle=modern dynamic
CloseApplications=yes
RestartApplications=no
SetupLogging=yes
MinVersion=10.0.19041
ArchitecturesAllowed=x64compatible
VersionInfoVersion={#VersionInfoVersion}
VersionInfoCompany=LocalQrScanner Contributors
VersionInfoDescription=Offline QR scanner for PowerToys Run
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Files]
Source: "{#PluginSource}\*"; DestDir: "{app}"; Excludes: "*.pdb"; Flags: ignoreversion recursesubdirs createallsubdirs

[Run]
Filename: "{code:GetPowerToysPath}"; Description: "Start PowerToys"; Flags: nowait postinstall skipifsilent; Check: IsPowerToysInstalled

[Code]
function IsPowerToysRunning: Boolean;
var
  ResultCode: Integer;
  PowerShellPath: String;
  Parameters: String;
begin
  PowerShellPath := ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe');
  Parameters := '-NoProfile -NonInteractive -Command "if (Get-Process -Name PowerToys,PowerToys.PowerLauncher -ErrorAction SilentlyContinue) { exit 42 } else { exit 0 }"';
  Result := Exec(PowerShellPath, Parameters, '', SW_HIDE, ewWaitUntilTerminated, ResultCode) and (ResultCode = 42);
end;

function GetPowerToysPath(Param: String): String;
var
  Candidate: String;
begin
  Candidate := ExpandConstant('{commonpf}\PowerToys\PowerToys.exe');
  if FileExists(Candidate) then
  begin
    Result := Candidate;
    Exit;
  end;

  Candidate := ExpandConstant('{localappdata}\PowerToys\PowerToys.exe');
  if FileExists(Candidate) then
  begin
    Result := Candidate;
    Exit;
  end;

  Candidate := ExpandConstant('{localappdata}\Microsoft\PowerToys\PowerToys.exe');
  if FileExists(Candidate) then
  begin
    Result := Candidate;
    Exit;
  end;

  Result := '';
end;

function IsPowerToysInstalled: Boolean;
begin
  Result := GetPowerToysPath('') <> '';
end;

function InitializeSetup: Boolean;
begin
  Result := True;
  if IsPowerToysRunning then
  begin
    MsgBox('PowerToys is running. Exit it from the system tray, then run this installer again.', mbError, MB_OK);
    Result := False;
  end;
end;

function InitializeUninstall: Boolean;
begin
  Result := True;
  if IsPowerToysRunning then
  begin
    MsgBox('PowerToys is running. Exit it from the system tray, then run the uninstaller again.', mbError, MB_OK);
    Result := False;
  end;
end;
