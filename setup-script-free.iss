; Inno Setup 스크립트 - BetaChip 무료 버전 v2.3.0
; Windows 11 보안 정책 대응 강화
#define MyAppName "BetaChip"
#define MyAppVersion "2.4.1"
#define MyAppPublisher "Sia"
#define MyAppURL "https://github.com/Sia-Le-Blanc/BetaChip"
#define MyAppExeName "MosaicCensorSystem.exe"
#define MyBuildPath "MosaicCensorSystem\bin\Release\Free\net8.0-windows"

[Setup]
AppId={{C2A62B8D-8792-4547-B864-763B85B58A2F}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=.\install
OutputBaseFilename=BetaChip-v{#MyAppVersion}-Setup
SetupIconFile=
Compression=lzma
SolidCompression=yes
WizardStyle=modern
; ★★★ Windows 11 보안 대응: 관리자 권한 요청 ★★★
PrivilegesRequired=admin
PrivilegesRequiredOverridesAllowed=dialog
SetupLogging=yes
ShowLanguageDialog=no
AllowNoIcons=yes

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "빠른 실행 아이콘 만들기"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
; ★★★ 메인 프로그램 파일들 ★★★
Source: "{#MyBuildPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ★★★ Windows 11 보안 대응: 다중 위치에 모델 파일 설치 ★★★
; 1. 메인 설치 폴더
Source: "{#MyBuildPath}\Resources\best.onnx"; DestDir: "{app}"; Flags: ignoreversion; AfterInstall: CheckModelFileMain
Source: "{#MyBuildPath}\Resources\best.onnx"; DestDir: "{app}\Resources"; Flags: ignoreversion

; 2. 사용자 로컬 데이터 폴더 (Windows 11에서 가장 안전)
Source: "{#MyBuildPath}\Resources\best.onnx"; DestDir: "{localappdata}\{#MyAppName}"; Flags: ignoreversion; AfterInstall: CheckModelFileLocal

; 3. 사용자 앱 데이터 폴더
Source: "{#MyBuildPath}\Resources\best.onnx"; DestDir: "{userappdata}\{#MyAppName}"; Flags: ignoreversion

; 4. 내 문서 폴더 백업
Source: "{#MyBuildPath}\Resources\best.onnx"; DestDir: "{userdocs}\{#MyAppName}"; Flags: ignoreversion

; 5. 사용자 프로필 폴더
Source: "{#MyBuildPath}\Resources\best.onnx"; DestDir: "{userappdata}\..\Local\{#MyAppName}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Comment: "BetaChip - AI 기반 실시간 검열 시스템"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "BetaChip - AI 실시간 검열"
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

; ★★★ Windows 11 접근성을 위한 추가 바로가기 ★★★
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "BetaChip 자동 시작 (선택적)"; Tasks: autostartup

[Tasks]
Name: "autostartup"; Description: "Windows 시작시 자동 실행"; GroupDescription: "추가 옵션"; Flags: unchecked

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: files; Name: "{app}\*.log"
Type: files; Name: "{localappdata}\{#MyAppName}\*.log"
Type: files; Name: "{userappdata}\{#MyAppName}\*.log"
Type: filesandordirs; Name: "{localappdata}\{#MyAppName}"
Type: filesandordirs; Name: "{userappdata}\{#MyAppName}"
Type: filesandordirs; Name: "{userdocs}\{#MyAppName}"

[Code]
{ ★★★ Windows 11 보안 대응 개선된 모델 파일 검증 ★★★ }
procedure CheckModelFileMain();
var
  ModelPath: String;
begin
  ModelPath := ExpandConstant('{app}\Resources\best.onnx');
  
  if FileExists(ModelPath) then
  begin
    Log('Main model file installed successfully: ' + ModelPath);
  end
  else
  begin
    Log('Warning: Main model file installation failed: ' + ModelPath);
  end;
end;

procedure CheckModelFileLocal();
var
  LocalPath: String;
begin
  LocalPath := ExpandConstant('{localappdata}\{#MyAppName}\best.onnx');
  
  if FileExists(LocalPath) then
  begin
    Log('Local AppData model file installed successfully: ' + LocalPath);
  end
  else
  begin
    Log('Warning: Local AppData model file installation failed: ' + LocalPath);
  end;
end;

{ ★★★ Windows 11 보안 설정 확인 및 권한 검증 ★★★ }
function IsWindows11(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  Result := (Version.Major >= 10) and (Version.Build >= 22000);
end;

procedure SetSecurePermissions();
var
  AppPath: String;
  LocalPath: String;
begin
  AppPath := ExpandConstant('{app}');
  LocalPath := ExpandConstant('{localappdata}\{#MyAppName}');
  
  try
    { Windows 11에서는 사용자 데이터 폴더의 권한을 명시적으로 설정 }
    if IsWindows11() then
    begin
      Log('Windows 11 detected - Setting secure permissions');
      { 여기서는 폴더 생성만 확인하고 시스템이 기본 권한을 설정하도록 함 }
      if not DirExists(LocalPath) then
        ForceDirectories(LocalPath);
    end;
  except
    Log('Permission setting failed, but continuing installation');
  end;
end;

{ ★★★ 설치 완료 후 모든 백업 위치 검증 ★★★ }
procedure CurStepChanged(CurStep: TSetupStep);
var
  MainModelPath, LocalModelPath, UserModelPath, DocsModelPath: String;
  SuccessCount: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    SuccessCount := 0;
    
    { Windows 11 보안 권한 설정 }
    SetSecurePermissions();
    
    { 모든 백업 위치 확인 }
    MainModelPath := ExpandConstant('{app}\Resources\best.onnx');
    LocalModelPath := ExpandConstant('{localappdata}\{#MyAppName}\best.onnx');
    UserModelPath := ExpandConstant('{userappdata}\{#MyAppName}\best.onnx');
    DocsModelPath := ExpandConstant('{userdocs}\{#MyAppName}\best.onnx');
    
    if FileExists(MainModelPath) then
    begin
      Log('✓ Main installation path OK: ' + MainModelPath);
      Inc(SuccessCount);
    end;
    
    if FileExists(LocalModelPath) then
    begin
      Log('✓ Local AppData path OK: ' + LocalModelPath);
      Inc(SuccessCount);
    end;
    
    if FileExists(UserModelPath) then
    begin
      Log('✓ User AppData path OK: ' + UserModelPath);
      Inc(SuccessCount);
    end;
    
    if FileExists(DocsModelPath) then
    begin
      Log('✓ Documents path OK: ' + DocsModelPath);
      Inc(SuccessCount);
    end;
    
    Log('Model file installation summary: ' + IntToStr(SuccessCount) + '/4 locations successful');
    
    { 한 곳이라도 성공했으면 OK }
    if SuccessCount > 0 then
    begin
      Log('Model file installation completed successfully');
    end
    else
    begin
      Log('WARNING: No model file locations were successful');
    end;
  end;
end;

{ ★★★ Windows 11 전용 안내 메시지 ★★★ }
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpWelcome then
  begin
    if IsWindows11() then
    begin
      WizardForm.WelcomeLabel2.Caption := 
        '이 설치 프로그램은 Windows 11의 보안 정책에 최적화되어 있습니다.' + #13#10 + #13#10 +
        '설치 중 Windows 보안 경고가 나타날 수 있으나, 이는 정상적인 과정입니다.' + #13#10 + #13#10 +
        '더 나은 호환성을 위해 관리자 권한으로 실행하는 것을 권장합니다.';
    end;
  end;
end;

{ ★★★ 간소화된 시스템 요구사항 확인 (불필요한 .NET 체크 제거) ★★★ }
function InitializeSetup(): Boolean;
var
  Version: TWindowsVersion;
begin
  Result := True;
  
  GetWindowsVersionEx(Version);
  
  { Windows 10 이상만 확인 }
  if Version.Major < 10 then
  begin
    MsgBox('이 프로그램은 Windows 10 이상에서만 실행됩니다.' + #13#10 +
           '현재 시스템: Windows ' + IntToStr(Version.Major) + '.' + IntToStr(Version.Minor),
           mbError, MB_OK);
    Result := False;
    Exit;
  end;
  
  { Windows 11 사용자에게 보안 관련 안내 }
  if IsWindows11() then
  begin
    Log('Windows 11 detected - Enhanced security mode');
    MsgBox('Windows 11이 감지되었습니다.' + #13#10 + #13#10 +
           'Windows 보안 기능으로 인해 설치 후 첫 실행시 보안 경고가 나타날 수 있습니다.' + #13#10 +
           '이는 정상적인 과정이니 "추가 정보" → "실행"을 선택해주세요.' + #13#10 + #13#10 +
           '설치를 계속 진행합니다.',
           mbInformation, MB_OK);
  end;
end;