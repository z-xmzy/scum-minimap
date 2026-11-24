; SCUM MiniMap Windows installer (Inno Setup 6).
; Build with installer\Build-Installer.ps1, which passes /DAppVersion and /DPackageDir.
; Installs per user (no administrator rights): the app keeps settings, routes and logs in app\data
; beside the executable, which must therefore be writable.

#ifndef AppVersion
  #error Pass /DAppVersion=x.y.z (installer\Build-Installer.ps1 does this).
#endif
#ifndef PackageDir
  #define PackageDir "..\package\SCUM-MiniMap"
#endif
#ifndef OutputDir
  #define OutputDir "..\release"
#endif

#define AppName "SCUM 小地图"
#define AppExe "SkynettMiniMap.exe"
#define RepoUrl "https://github.com/z-xmzy/scum-minimap"
#define UpstreamUrl "https://github.com/Mike-Rafone-SCUM/MiniMap"

[Setup]
; Keep AppId fixed forever: it is how upgrades find the existing installation.
#ifdef TestInstall
; Separate identity so test installs never take over the real installation's uninstall entry or shortcuts.
AppId={{6F0B7C52-3E8A-4D4B-9C2E-5A1D7E3B9F42}
#else
AppId={{6F0B7C52-3E8A-4D4B-9C2E-5A1D7E3B9F41}
#endif
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} {#AppVersion}
AppPublisher=z-xmzy
AppPublisherURL={#RepoUrl}
AppSupportURL={#RepoUrl}/issues
AppUpdatesURL={#RepoUrl}/releases
AppComments=基于 Mike-Rafone-SCUM/MiniMap 改编（{#UpstreamUrl}）
VersionInfoVersion={#AppVersion}.0
VersionInfoProductVersion={#AppVersion}
VersionInfoDescription={#AppName} 安装程序
DefaultDirName={localappdata}\Programs\SCUM-MiniMap
#ifdef TestInstall
DefaultGroupName={#AppName} 测试
#else
DefaultGroupName={#AppName}
#endif
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
UsePreviousAppDir=yes
LicenseFile=LICENSE-installer.txt
OutputDir={#OutputDir}
OutputBaseFilename=SCUM-MiniMap-Setup-{#AppVersion}
SetupIconFile=..\source\resources\App-Icon.ico
UninstallDisplayIcon={app}\app\{#AppExe}
UninstallDisplayName={#AppName}
WizardStyle=modern
Compression=lzma2/max
SolidCompression=yes
LZMAUseSeparateProcess=yes
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0
CloseApplications=yes
RestartApplications=no
SetupLogging=yes

[Languages]
Name: "zh"; MessagesFile: "ChineseSimplified.isl"
Name: "en"; MessagesFile: "compiler:Default.isl"

[CustomMessages]
zh.RoadMarker=路网标记工具 RoadMarker
en.RoadMarker=RoadMarker road marking tool
zh.MainProgram=小地图主程序（必需）
en.MainProgram=MiniMap (required)
zh.WaitForClose=检测到小地图仍在运行。请先关闭它（托盘图标右键退出），然后点“重试”。
en.WaitForClose=SCUM MiniMap is still running. Close it (tray icon > Exit) and click Retry.
zh.RemoveData=是否同时删除设置、已保存路线和日志（app\data）？%n%n选“否”会保留，重新安装后可继续使用。
en.RemoveData=Also delete settings, saved routes and logs (app\data)?%n%nChoose No to keep them for a later reinstall.
zh.Upstream=上游项目 Mike-Rafone-SCUM MiniMap
en.Upstream=Upstream project Mike-Rafone-SCUM MiniMap

[Types]
Name: "full"; Description: "{code:FullTypeName}"
Name: "compact"; Description: "{code:CompactTypeName}"

[Components]
Name: "main"; Description: "{cm:MainProgram}"; Types: full compact; Flags: fixed
Name: "roadmarker"; Description: "{cm:RoadMarker}"; Types: full

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"

[InstallDelete]
; Versioned data is replaced wholesale so files dropped by a newer release do not linger.
; User data in app\data is never touched here.
Type: filesandordirs; Name: "{app}\app\fine"
Type: filesandordirs; Name: "{app}\app\assets"

[Files]
Source: "{#PackageDir}\app\*"; DestDir: "{app}\app"; Components: main; Excludes: "\data,\data\*,\temp,\temp\*,*.update.exe,*.partial,*.previous"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#PackageDir}\使用说明.md"; DestDir: "{app}"; Components: main; Flags: ignoreversion
Source: "{#PackageDir}\启动 SCUM 地图.cmd"; DestDir: "{app}"; Components: main; Flags: ignoreversion
Source: "LICENSE-installer.txt"; DestDir: "{app}"; DestName: "许可与出处.txt"; Components: main; Flags: ignoreversion
Source: "{#PackageDir}\dist\*"; DestDir: "{app}\dist"; Components: roadmarker; Flags: ignoreversion recursesubdirs createallsubdirs

[Dirs]
Name: "{app}\app\data"; Flags: uninsneveruninstall
Name: "{app}\app\temp"

[Icons]
Name: "{group}\{#AppName}"; Filename: "{app}\app\{#AppExe}"; WorkingDir: "{app}\app"
Name: "{group}\使用说明"; Filename: "{app}\使用说明.md"
Name: "{group}\许可与出处"; Filename: "{app}\许可与出处.txt"
Name: "{group}\{cm:Upstream}"; Filename: "{#UpstreamUrl}"
Name: "{group}\{cm:UninstallProgram,{#AppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#AppName}"; Filename: "{app}\app\{#AppExe}"; WorkingDir: "{app}\app"; Tasks: desktopicon

[Run]
; Also runs after a silent in-app update (no skipifsilent), so MiniMap comes back by itself.
; Pass /NOLAUNCH=1 to install without starting it (deployment scripts, tests).
Filename: "{app}\app\{#AppExe}"; WorkingDir: "{app}\app"; Description: "{cm:LaunchProgram,{#AppName}}"; Flags: nowait postinstall; Check: ShouldLaunch

[UninstallDelete]
Type: filesandordirs; Name: "{app}\app\temp"

[Code]
const
  AppMutex = 'ScumMiniMapOverlay';

function ShouldLaunch(): Boolean;
begin
  Result := ExpandConstant('{param:NOLAUNCH|0}') <> '1';
end;

function FullTypeName(Param: String): String;
begin
  if ActiveLanguage = 'zh' then Result := '完整安装' else Result := 'Full installation';
end;

function CompactTypeName(Param: String): String;
begin
  if ActiveLanguage = 'zh' then Result := '仅小地图' else Result := 'MiniMap only';
end;

{ The in-app updater starts this setup and then exits; give it time to close before asking. }
function WaitForMiniMapExit(Seconds: Integer): Boolean;
var
  I: Integer;
begin
  for I := 1 to Seconds * 4 do
  begin
    if not CheckForMutexes(AppMutex) then
    begin
      Result := True;
      Exit;
    end;
    Sleep(250);
  end;
  Result := not CheckForMutexes(AppMutex);
end;

function InitializeSetup(): Boolean;
begin
  Result := True;
  { Silent runs come from the in-app updater; PrepareToInstall finishes the hand-over. }
  if WizardSilent then
    Exit;
  while not WaitForMiniMapExit(20) do
  begin
    if MsgBox(CustomMessage('WaitForClose'), mbError, MB_RETRYCANCEL) <> IDRETRY then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

{ Ends MiniMap processes started from this installation only. Needed for 2.5.0, whose updater
  hid its window and tray icon but left the process (and its mutex) running. }
procedure StopInstalledMiniMap();
var
  Code: Integer;
  Script: String;
begin
  Script := ExpandConstant('{app}');
  StringChangeEx(Script, '''', '''''', True);
  Script := '$p=''' + Script + '\'';' +
    'Get-Process -Name SkynettMiniMap -ErrorAction SilentlyContinue | ' +
    'Where-Object { $_.Path -and $_.Path.StartsWith($p,[StringComparison]::OrdinalIgnoreCase) } | ' +
    'Stop-Process -Force';
  Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' + Script + '"',
    '', SW_HIDE, ewWaitUntilTerminated, Code);
  Log('Stopped running MiniMap under ' + ExpandConstant('{app}') + ', exit code ' + IntToStr(Code));
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if WaitForMiniMapExit(20) then
    Exit;
  StopInstalledMiniMap();
  if not WaitForMiniMapExit(10) then
    Result := CustomMessage('WaitForClose');
end;

function InitializeUninstall(): Boolean;
begin
  Result := True;
  while CheckForMutexes(AppMutex) do
  begin
    if UninstallSilent or (MsgBox(CustomMessage('WaitForClose'), mbError, MB_RETRYCANCEL) <> IDRETRY) then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

{ Remove this installation's "start with SCUM" scheduled task, which would otherwise point at a
  deleted program. Only tasks named by the app and referring to this folder are touched. }
procedure RemoveAutoLaunchTask();
var
  Code: Integer;
  Script: String;
begin
  Script := ExpandConstant('{app}');
  StringChangeEx(Script, '''', '''''', True);
  Script := '$p=''' + Script + ''';' +
    'Get-ScheduledTask -TaskName ''SCUM MiniMap Auto Launch S-*'' -ErrorAction SilentlyContinue | ' +
    'Where-Object { ($_.Actions | ForEach-Object { $_.Arguments }) -join '' '' -like (''*''+$p+''*'') } | ' +
    'Unregister-ScheduledTask -Confirm:$false';
  Exec(ExpandConstant('{sys}\WindowsPowerShell\v1.0\powershell.exe'),
    '-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command "' + Script + '"',
    '', SW_HIDE, ewWaitUntilTerminated, Code);
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
begin
  if CurUninstallStep = usUninstall then
    RemoveAutoLaunchTask();
  if CurUninstallStep = usPostUninstall then
  begin
    if DirExists(ExpandConstant('{app}\app\data')) and (not UninstallSilent) then
      if MsgBox(CustomMessage('RemoveData'), mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES then
        DelTree(ExpandConstant('{app}\app\data'), True, True, True);
    RemoveDir(ExpandConstant('{app}\app'));
    RemoveDir(ExpandConstant('{app}'));
  end;
end;
