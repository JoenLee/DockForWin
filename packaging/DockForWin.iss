#ifndef PayloadDir
  #error PayloadDir must be supplied
#endif
#ifndef DestinationDir
  #define DestinationDir "D:\DockForWin"
#endif

[Setup]
AppId={{F5D103D5-7084-4BC2-933D-833CA18DAE86}
AppName=DockForWin
AppVersion=0.6.0
AppVerName=DockForWin 0.6.0
AppPublisher=DockForWin
DefaultDirName={code:DefaultLocation}
DefaultGroupName=DockForWin
DisableProgramGroupPage=yes
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible
MinVersion=10.0.19041
OutputDir={#DestinationDir}
OutputBaseFilename=DockForWin-Setup-0.6.0-x64
SetupIconFile=..\src\DockForWindows\Assets\DockForWindows.ico
UninstallDisplayIcon={app}\DockForWindows.exe
VersionInfoVersion=0.6.0.0
VersionInfoProductName=DockForWin
Compression=lzma2/normal
SolidCompression=yes
WizardStyle=modern
CloseApplications=yes
RestartApplications=no
DisableReadyPage=no
UninstallDisplayName=DockForWin

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "可选快捷方式："; Flags: unchecked

[Files]
Source: "{#PayloadDir}\DockForWindows.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "Install-Readme.txt"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\DockForWin"; Filename: "{app}\DockForWindows.exe"; WorkingDir: "{app}"
Name: "{group}\程序坞设置"; Filename: "{app}\DockForWindows.exe"; Parameters: "--settings"; WorkingDir: "{app}"
Name: "{group}\卸载 DockForWin"; Filename: "{uninstallexe}"
Name: "{autodesktop}\DockForWin"; Filename: "{app}\DockForWindows.exe"; WorkingDir: "{app}"; Tasks: desktopicon

[Run]
Filename: "{app}\DockForWindows.exe"; Description: "启动 DockForWin"; Flags: nowait postinstall skipifsilent

[Code]
function OpenEvent(Access: Cardinal; Inherit: Boolean; Name: String): THandle;
  external 'OpenEventW@kernel32.dll stdcall';
function SetEvent(Handle: THandle): Boolean;
  external 'SetEvent@kernel32.dll stdcall';
function CloseHandle(Handle: THandle): Boolean;
  external 'CloseHandle@kernel32.dll stdcall';

procedure RequestDockExit;
var Event: THandle;
begin
  Event := OpenEvent(2, False, 'Local\DockForWindows.Development.Exit');
  if Event <> 0 then begin SetEvent(Event); CloseHandle(Event); Sleep(900); end;
end;

function DefaultLocation(Param: String): String;
begin
  if DirExists('D:\') then Result := 'D:\DockForWin\App'
  else Result := ExpandConstant('{localappdata}\Programs\DockForWin');
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin RequestDockExit; Result := ''; end;

function InitializeUninstall: Boolean;
begin RequestDockExit; Result := True; end;
