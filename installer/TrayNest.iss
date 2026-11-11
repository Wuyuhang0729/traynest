#ifndef Payload
  #error Payload must name the verified complete application directory
#endif
#ifndef AppVersion
  #define AppVersion "0.2.0-preview.17"
#endif
#ifndef Output
  #define Output "..\artifacts"
#endif

[Setup]
#ifdef TestProfile
AppId=TrayNest.TN017.Tests
AppName=TrayNest TN017 安装验证
#else
AppId=TrayNest
AppName=TrayNest
#endif
AppVersion={#AppVersion}
AppPublisher=TrayNest
AppPublisherURL=https://github.com/Wuyuhang0729/traynest
DefaultDirName={localappdata}\Programs\TrayNest
PrivilegesRequired=lowest
ArchitecturesAllowed=x64os
ArchitecturesInstallIn64BitMode=x64os
MinVersion=10.0.22000
DefaultGroupName=TrayNest
DisableProgramGroupPage=yes
UsePreviousAppDir=yes
UsePreviousTasks=yes
DisableWelcomePage=no
WizardStyle=modern
WizardSizePercent=100
SetupIconFile=..\src\NestedTray\Resources\TrayNest.ico
UninstallDisplayIcon={app}\TrayNest.ico
LicenseFile=..\LICENSE
InfoBeforeFile=..\docs\INSTALLATION-NOTES.txt
OutputDir={#Output}
OutputBaseFilename=TrayNest-Setup-v{#AppVersion}-win-x64
Compression=lzma2/fast
SolidCompression=yes
CloseApplications=no
RestartApplications=no
AlwaysRestart=no
AppMutex={code:GetAppMutex}
Uninstallable=yes

[Languages]
Name: chinesesimp; MessagesFile: compiler:Languages\ChineseSimplified.isl

[LangOptions]
DialogFontSize=10

[Tasks]
Name: desktopicon; Description: 创建桌面快捷方式; Flags: checkedonce
Name: startup; Description: 登录 Windows 后启动 TrayNest; Flags: unchecked; Check: IsFresh

[Files]
Source: {#Payload}\TrayNest.Maintenance.exe; Flags: dontcopy
Source: {#Payload}\WebView2Loader.dll; Flags: dontcopy
Source: {#Payload}\*; DestDir: {app}; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: {autodesktop}\TrayNest; Filename: {app}\NestedTray.exe; WorkingDir: {app}; IconFilename: {app}\TrayNest.ico; Tasks: desktopicon
Name: {group}\TrayNest; Filename: {app}\NestedTray.exe; WorkingDir: {app}; IconFilename: {app}\TrayNest.ico; Check: IsProduction

[UninstallRun]
Filename: "{app}\TrayNest.Maintenance.exe"; Parameters: "--cleanup-startup ""{app}"" ""{code:GetProfile}"" ""{code:GetUninstallState}"""; Flags: runhidden waituntilterminated; RunOnceId: TrayNestStartup

[Code]
var
  StatePath: String;
  Prepared, Completed, WasExisting: Boolean;
  VerificationFailed: Boolean;
  RuntimePage: TWizardPage;
  RuntimeNote: TNewStaticText;
  RuntimeDownload: TNewButton;

function Q(Value: String): String;
begin
  Result := '"' + Value + '"';
end;

function GetProfile(Param: String): String;
begin
#ifdef TestProfile
  Result := '{#TestProfile}';
#else
  Result := ExpandConstant('{localappdata}\NestedTray');
#endif
end;

function GetUninstallState(Param: String): String;
begin
  Result := GetProfile('') + '\maintenance\uninstall.json';
end;

function GetAppMutex(Param: String): String;
begin
#ifdef TestProfile
  Result := 'Local\TrayNest.TN017.Tests';
#else
  Result := 'Local\NestedTray-' + GetUserNameString;
#endif
end;

function IsFresh: Boolean;
begin
#ifdef TestProfile
  Result := False;
#else
  if Prepared then Result := not WasExisting
  else Result := not FileExists(ExpandConstant('{app}\NestedTray.exe'));
#endif
end;

function IsProduction: Boolean;
begin
#ifdef TestProfile
  Result := False;
#else
  Result := True;
#endif
end;

function Helper(Operation: String): Boolean;
var Code: Integer; Registration: String;
begin
#ifdef TestProfile
  Registration := 'TrayNest.TN017.Tests_is1';
#else
  Registration := 'TrayNest_is1';
#endif
  Result := Exec(ExpandConstant('{tmp}\TrayNest.Maintenance.exe'), Operation + ' ' + Q(ExpandConstant('{app}')) + ' ' + Q(GetProfile('')) + ' ' + Q(StatePath) + ' ' + Q('{#AppVersion}') + ' ' + Q(Registration), '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
end;

function FailureText: String;
var Lines: TArrayOfString; I: Integer;
begin
  Result := '';
  if LoadStringsFromFile(StatePath + '.error.txt', Lines) then begin
    for I := 0 to GetArrayLength(Lines)-1 do Result := Result + Lines[I] + #13#10;
  end else Result := '安装保护检查未完成，请保留原程序及备份后重试。';
end;

procedure OpenRuntime(Sender: TObject);
var Code: Integer;
begin
  ShellExec('open', 'https://developer.microsoft.com/microsoft-edge/webview2/', '', '', SW_SHOWNORMAL, ewNoWait, Code);
end;

procedure InitializeWizard;
begin
  ExtractTemporaryFile('TrayNest.Maintenance.exe');
  ExtractTemporaryFile('WebView2Loader.dll');
  RuntimePage := CreateCustomPage(wpLicense, '运行环境检查', '设置窗口需要 Microsoft Edge WebView2 Runtime。');
  RuntimeNote := TNewStaticText.Create(RuntimePage);
  RuntimeNote.Parent := RuntimePage.Surface;
  RuntimeNote.SetBounds(0, 12, RuntimePage.SurfaceWidth, ScaleY(80));
  RuntimeNote.WordWrap := True;
  RuntimeNote.Caption := '如果尚未安装，请打开微软官方页面自行安装 Runtime，再点击“下一步”重新检测。不会静默下载或安装依赖。';
  RuntimeDownload := TNewButton.Create(RuntimePage);
  RuntimeDownload.Parent := RuntimePage.Surface;
  RuntimeDownload.Caption := '打开微软官方下载';
  RuntimeDownload.SetBounds(0, ScaleY(110), ScaleX(180), ScaleY(30));
  RuntimeDownload.OnClick := @OpenRuntime;
  StatePath := GetProfile('') + '\maintenance\install-' + GetDateTimeString('yyyymmddhhnnsszzz', '-', ':') + '.json';
end;

function CheckRuntime: Boolean;
var Code: Integer;
begin
  Result := Exec(ExpandConstant('{tmp}\TrayNest.Maintenance.exe'), '--probe', '', SW_HIDE, ewWaitUntilTerminated, Code) and (Code = 0);
end;

function ShouldSkipPage(PageID: Integer): Boolean;
begin
  Result := (PageID = RuntimePage.ID) and CheckRuntime;
end;

function NextButtonClick(CurPageID: Integer): Boolean;
begin
  Result := True;
  if CurPageID = RuntimePage.ID then begin
    Result := CheckRuntime;
    if not Result then MsgBox('尚未检测到可用 WebView2 Runtime。安装后可在此重新检测。', mbInformation, MB_OK);
  end;
end;

function PrepareToInstall(var NeedsRestart: Boolean): String;
begin
  Result := '';
  if not CheckRuntime then begin Result := '缺少 WebView2 Runtime，请先安装微软官方运行环境。'; exit; end;
  WasExisting := FileExists(ExpandConstant('{app}\NestedTray.exe'));
  if not Helper('--prepare') then begin Result := FailureText; exit; end;
  Prepared := True;
end;

procedure CurStepChanged(CurStep: TSetupStep);
var Operation: String; Code: Integer;
begin
  if CurStep = ssPostInstall then begin
#ifdef TestProfile
    Operation := '--verify-only';
#else
    Operation := '--verify';
#endif
    if not Helper(Operation) then begin
      VerificationFailed := True;
      SuppressibleMsgBox(FailureText + #13#10 + '将尝试恢复原程序。', mbError, MB_OK, IDOK);
      if Helper('--restore') then begin
        Prepared := False;
        WizardForm.FinishedHeadingLabel.Caption := '安装未完成';
        WizardForm.FinishedLabel.Caption := '本次安装已撤回，已有原版已恢复。配置和布局保留。';
      end else begin
        WizardForm.FinishedHeadingLabel.Caption := '安装未完成，需要恢复';
        WizardForm.FinishedLabel.Caption := '请保留备份及恢复记录：' + StatePath;
      end;
      exit;
    end;
    Completed := True;
    if IsFresh and WizardIsTaskSelected('startup') then begin
      if not Exec(ExpandConstant('{app}\NestedTray.exe'), '--configure-startup enable ' + Q(ExpandConstant('{tmp}\startup-result.json')), '', SW_HIDE, ewWaitUntilTerminated, Code) or (Code <> 0) then
        SuppressibleMsgBox('程序已安装，但开机启动未能启用。可稍后在设置中重试。', mbInformation, MB_OK, IDOK);
    end;
  end;
end;

function GetCustomSetupExitCode: Integer;
begin
  if VerificationFailed then Result := 1 else Result := 0;
end;

procedure CurPageChanged(CurPageID: Integer);
begin
  if (CurPageID = wpFinished) and VerificationFailed then begin
    WizardForm.FinishedHeadingLabel.Caption := '安装未完成';
    WizardForm.FinishedLabel.Caption := '新版本未通过核对，请按刚才的恢复结果继续处理。配置与备份已保留。';
  end;
end;

procedure DeinitializeSetup;
begin
  if Prepared and not Completed then begin
    if Helper('--restore') then SuppressibleMsgBox('安装未完成，已撤回本次程序文件；已有原版时已恢复。配置和布局保留。', mbInformation, MB_OK, IDOK)
    else SuppressibleMsgBox('安装未完成且未能自动恢复。请保留备份，恢复记录：' + StatePath + #13#10 + FailureText, mbError, MB_OK, IDOK);
  end;
end;

procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
#ifndef TestProfile
var Path, Backup: String;
#endif
begin
  if CurUninstallStep = usPostUninstall then begin
#ifndef TestProfile
    if not UninstallSilent and (MsgBox('程序已卸载。是否同时清除当前账户的配置与布局？默认保留。', mbConfirmation, MB_YESNO or MB_DEFBUTTON2) = IDYES) then begin
      Path := GetProfile('');
      Backup := Path + '.removed-' + GetDateTimeString('yyyymmddhhnnss', '-', ':');
      if DirExists(Path) and not RenameFile(Path, Backup) then MsgBox('未能清除配置，原数据已保留。', mbError, MB_OK);
    end;
#endif
  end;
end;
