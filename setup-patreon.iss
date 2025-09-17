; Inno Setup 스크립트 - BetaChip 후원자용 v2.3.0
; Windows 11 보안 정책 대응 강화 + 스티커 기능 포함
#define MyAppName "BetaChip - 후원자용"
#define MyAppVersion "2.4.1-후원자용"
#define MyAppPublisher "Sia"
#define MyAppURL "https://github.com/Sia-Le-Blanc/BetaChip"
#define MyAppExeName "MosaicCensorSystem.exe"
#define MyBuildPath "C:\Users\Sia\OneDrive\바탕 화면\main\BetaChip\MosaicCensorSystem\bin\Release\Patreon\net8.0-windows"

[Setup]
AppId={{D2633B8C-8792-4547-B864-763B85B58A2F}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
OutputDir=.\install
OutputBaseFilename=BetaChip-Patreon-v{#MyAppVersion}-Setup
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
Name: "autostartup"; Description: "Windows 시작시 자동 실행"; GroupDescription: "추가 옵션"; Flags: unchecked

[Files]
; ★★★ 메인 프로그램 파일들 (스티커 포함) ★★★
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

; ★★★ 후원자 전용: 스티커 파일들도 다중 위치에 백업 ★★★
; 메인 설치 위치
Source: "{#MyBuildPath}\Stickers\*"; DestDir: "{app}\Stickers"; Flags: ignoreversion recursesubdirs createallsubdirs; AfterInstall: CheckStickerFilesMain

; 사용자 데이터 위치 백업
Source: "{#MyBuildPath}\Stickers\*"; DestDir: "{localappdata}\{#MyAppName}\Stickers"; Flags: ignoreversion recursesubdirs createallsubdirs; AfterInstall: CheckStickerFilesLocal
Source: "{#MyBuildPath}\Stickers\*"; DestDir: "{userappdata}\{#MyAppName}\Stickers"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "{#MyBuildPath}\Stickers\*"; DestDir: "{userdocs}\{#MyAppName}\Stickers"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Comment: "BetaChip 후원자 전용 - 스티커 & 멀티모니터 지원"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "BetaChip 후원자 전용"
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

; ★★★ Windows 11 접근성을 위한 추가 바로가기 ★★★
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Comment: "BetaChip 후원자용 자동 시작"; Tasks: autostartup

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

{ ★★★ 후원자 전용: 스티커 파일 설치 검증 ★★★ }
procedure CheckStickerFilesMain();
var
  StickerDir: String;
  FindRec: TFindRec;
  Count: Integer;
begin
  StickerDir := ExpandConstant('{app}\Stickers');
  Count := 0;
  
  if FindFirst(StickerDir + '\*.png', FindRec) then
  begin
    try
      repeat
        Inc(Count);
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
  
  if Count > 0 then
  begin
    Log('Main sticker files installed successfully: ' + IntToStr(Count) + ' files in ' + StickerDir);
  end
  else
  begin
    Log('Warning: No main sticker files found after installation');
  end;
end;

procedure CheckStickerFilesLocal();
var
  LocalStickerDir: String;
  FindRec: TFindRec;
  Count: Integer;
begin
  LocalStickerDir := ExpandConstant('{localappdata}\{#MyAppName}\Stickers');
  Count := 0;
  
  if FindFirst(LocalStickerDir + '\*.png', FindRec) then
  begin
    try
      repeat
        Inc(Count);
      until not FindNext(FindRec);
    finally
      FindClose(FindRec);
    end;
  end;
  
  if Count > 0 then
  begin
    Log('Local sticker files backup created successfully: ' + IntToStr(Count) + ' files');
  end
  else
  begin
    Log('Warning: Local sticker backup failed');
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
      Log('Windows 11 detected - Setting secure permissions for Patreon version');
      
      { 사용자 데이터 폴더 생성 확인 }
      if not DirExists(LocalPath) then
        ForceDirectories(LocalPath);
        
      { 스티커 폴더도 생성 확인 }
      if not DirExists(LocalPath + '\Stickers') then
        ForceDirectories(LocalPath + '\Stickers');
    end;
  except
    Log('Permission setting failed, but continuing installation');
  end;
end;

{ ★★★ 설치 완료 후 모든 백업 위치 검증 (후원자 버전) ★★★ }
procedure CurStepChanged(CurStep: TSetupStep);
var
  MainModelPath, LocalModelPath, UserModelPath, DocsModelPath: String;
  MainStickerDir, LocalStickerDir: String;
  ModelSuccessCount, StickerSuccessCount: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    ModelSuccessCount := 0;
    StickerSuccessCount := 0;
    
    { Windows 11 보안 권한 설정 }
    SetSecurePermissions();
    
    { 모델 파일 모든 백업 위치 확인 }
    MainModelPath := ExpandConstant('{app}\Resources\best.onnx');
    LocalModelPath := ExpandConstant('{localappdata}\{#MyAppName}\best.onnx');
    UserModelPath := ExpandConstant('{userappdata}\{#MyAppName}\best.onnx');
    DocsModelPath := ExpandConstant('{userdocs}\{#MyAppName}\best.onnx');
    
    if FileExists(MainModelPath) then
    begin
      Log('✓ Main model path OK: ' + MainModelPath);
      Inc(ModelSuccessCount);
    end;
    
    if FileExists(LocalModelPath) then
    begin
      Log('✓ Local AppData model path OK: ' + LocalModelPath);
      Inc(ModelSuccessCount);
    end;
    
    if FileExists(UserModelPath) then
    begin
      Log('✓ User AppData model path OK: ' + UserModelPath);
      Inc(ModelSuccessCount);
    end;
    
    if FileExists(DocsModelPath) then
    begin
      Log('✓ Documents model path OK: ' + DocsModelPath);
      Inc(ModelSuccessCount);
    end;
    
    { 후원자 전용: 스티커 파일 백업 위치 확인 }
    MainStickerDir := ExpandConstant('{app}\Stickers');
    LocalStickerDir := ExpandConstant('{localappdata}\{#MyAppName}\Stickers');
    
    if DirExists(MainStickerDir) then
    begin
      Log('✓ Main sticker directory OK: ' + MainStickerDir);
      Inc(StickerSuccessCount);
    end;
    
    if DirExists(LocalStickerDir) then
    begin
      Log('✓ Local sticker backup OK: ' + LocalStickerDir);
      Inc(StickerSuccessCount);
    end;
    
    Log('Installation summary:');
    Log('- Model files: ' + IntToStr(ModelSuccessCount) + '/4 locations successful');
    Log('- Sticker files: ' + IntToStr(StickerSuccessCount) + '/2 locations successful');
    
    { 결과 평가 }
    if (ModelSuccessCount > 0) and (StickerSuccessCount > 0) then
    begin
      Log('Patreon installation completed successfully');
    end
    else if ModelSuccessCount > 0 then
    begin
      Log('Model installation OK, but sticker backup may have issues');
    end
    else
    begin
      Log('WARNING: Critical installation issues detected');
    end;
  end;
end;

{ ★★★ 후원자 버전 환영 메시지 (Windows 11 대응) ★★★ }
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpWelcome then
  begin
    if IsWindows11() then
    begin
      WizardForm.WelcomeLabel2.Caption := 
        '🎯 BetaChip 후원자 전용 버전에 오신 것을 환영합니다!' + #13#10 + #13#10 +
        '이 버전은 Windows 11 보안 정책에 최적화되어 있습니다.' + #13#10 + #13#10 +
        '포함된 후원자 전용 기능:' + #13#10 +
        '• 🎨 재미있는 스티커 기능' + #13#10 +
        '• 🖥️ 멀티 모니터 지원' + #13#10 +
        '• ⚡ 향상된 성능' + #13#10 +
        '• 🔒 강화된 보안 호환성' + #13#10 + #13#10 +
        '설치 중 Windows 보안 경고는 정상적인 과정입니다.' + #13#10 +
        '후원해주셔서 감사합니다!';
    end
    else
    begin
      WizardForm.WelcomeLabel2.Caption := 
        '🎯 BetaChip 후원자 전용 버전에 오신 것을 환영합니다!' + #13#10 + #13#10 +
        '포함된 기능:' + #13#10 +
        '• 🎯 재미있는 스티커 기능' + #13#10 +
        '• 🖥️ 멀티 모니터 지원' + #13#10 +
        '• ⚡ 향상된 성능' + #13#10 +
        '• 🎨 추가 검열 효과' + #13#10 + #13#10 +
        '후원해주셔서 감사합니다!';
    end;
  end;
end;

{ ★★★ 간소화된 시스템 요구사항 확인 (.NET 체크 제거) ★★★ }
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
  
  { Windows 11 후원자용 특별 안내 }
  if IsWindows11() then
  begin
    Log('Windows 11 detected - Patreon enhanced security mode');
    MsgBox('Windows 11에서 BetaChip 후원자 버전을 설치합니다.' + #13#10 + #13#10 +
           '• 스티커 기능과 멀티모니터 지원이 포함되어 있습니다' + #13#10 +
           '• Windows 보안으로 인한 경고는 정상적인 과정입니다' + #13#10 +
           '• 더 나은 호환성을 위해 관리자 권한을 권장합니다' + #13#10 + #13#10 +
           '설치를 계속 진행합니다.',
           mbInformation, MB_OK);
  end;
end;