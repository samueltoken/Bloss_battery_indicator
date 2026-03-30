#define MyAppName "Bloss"
#ifndef AppVersion
  #define AppVersion "1.0.1"
#endif
#ifndef PublishDir
  #define PublishDir "..\..\artifacts\staging\installer-publish"
#endif
#ifndef OutputDir
  #define OutputDir "..\..\release\installer"
#endif

[Setup]
AppId={{A2416A57-C0A4-42D4-BF28-D588B2A0CC26}
AppName={#MyAppName}
AppVersion={#AppVersion}
DefaultDirName={autopf}\Bloss
DefaultGroupName={#MyAppName}
SetupIconFile=..\..\BluetoothBatteryWidget.App\Assets\app.ico
UninstallDisplayIcon={app}\Bloss.exe
OutputDir={#OutputDir}
OutputBaseFilename=setup
Compression=lzma
SolidCompression=yes
WizardStyle=modern
WizardImageBackColor=$000000C8
WizardSmallImageBackColor=$000000C8
ArchitecturesInstallIn64BitMode=x64compatible

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; Flags: unchecked

[Files]
Source: "{#PublishDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.pdb"

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\Bloss.exe"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\Bloss.exe"; Tasks: desktopicon
Name: "{autoprograms}\Uninstall {#MyAppName}"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\Bloss.exe"; Description: "Launch {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  GeneratedUninstallExe: string;
  FriendlyUninstallExe: string;
begin
  if CurStep = ssPostInstall then
  begin
    GeneratedUninstallExe := ExpandConstant('{uninstallexe}');
    FriendlyUninstallExe := ExpandConstant('{app}\uninstall.exe');
    if FileExists(GeneratedUninstallExe) then
    begin
      CopyFile(GeneratedUninstallExe, FriendlyUninstallExe, False);
    end;
  end;
end;
