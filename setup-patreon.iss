; Inno Setup 스크립트 - BetaChip 후원자용 v2.3.0
; 스티커 기능 및 멀티모니터 지원 포함
#define MyAppName "BetaChip - 후원자용"
#define MyAppVersion "2.3.0-후원자용"
#define MyAppPublisher "Sia"
#define MyAppURL "https://github.com/Sia-Le-Blanc/BetaChip"
#define MyAppExeName "MosaicCensorSystem.exe"
; ★★★ 후원자용 빌드 결과물 경로 - 상대 경로 사용 ★★★
#define MyBuildPath "C:\Users\Sia\OneDrive\바탕 화면\main\BetaChip\MosaicCensorSystem\bin\Release\Patreon\net8.0-windows"

[Setup]
; 중요: AppId를 무료 버전과 다르게 설정하여 충돌을 방지합니다.
AppId={{D2633B8C-8792-4547-B864-763B85B58A2F}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; 최종 설치 파일 이름 설정
OutputDir=.\install
OutputBaseFilename=BetaChip-Patreon-v{#MyAppVersion}-Setup
SetupIconFile=
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
; ★★★ 후원자용 설치 과정 개선 ★★★
SetupLogging=yes
ShowLanguageDialog=no
AllowNoIcons=yes
; 후원자 버전 특별 표시
WizardImageFile=
WizardSmallImageFile=

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "quicklaunchicon"; Description: "빠른 실행 아이콘 만들기"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked; OnlyBelowVersion: 6.1

[Files]
; ★★★ 메인 프로그램 파일들 복사 (스티커 포함) ★★★
Source: "{#MyBuildPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ★★★ 중요한 모델 파일을 다중 위치에 백업 복사 ★★★
Source: "{#MyBuildPath}\Resources\best.onnx"; DestDir: "{app}"; Flags: ignoreversion; AfterInstall: CheckModelFile
Source: "{#MyBuildPath}\Resources\best.onnx"; DestDir: "{app}\Resources"; Flags: ignoreversion
; 사용자 데이터 폴더에도 백업
Source: "{#MyBuildPath}\Resources\best.onnx"; DestDir: "{userappdata}\{#MyAppName}"; Flags: ignoreversion

; ★★★ 후원자 전용: 스티커 파일들 별도 백업 ★★★
Source: "{#MyBuildPath}\Stickers\*"; DestDir: "{app}\Stickers"; Flags: ignoreversion recursesubdirs createallsubdirs; AfterInstall: CheckStickerFiles

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Comment: "BetaChip 후원자 전용 버전 - 스티커 기능 포함"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon; Comment: "BetaChip 후원자 전용"
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 설치 중 생성된 로그 파일들도 삭제
Type: files; Name: "{app}\*.log"
Type: files; Name: "{app}\fatal_error.log"
Type: files; Name: "{app}\init_error.log"
; 후원자 전용 임시 파일들
Type: files; Name: "{app}\capture_test.jpg"

[Code]
{ ★★★ 모델 파일 설치 검증 함수 ★★★ }
procedure CheckModelFile();
var
  ModelPath: String;
  ErrorMsg: String;
begin
  ModelPath := ExpandConstant('{app}\Resources\best.onnx');
  
  if not FileExists(ModelPath) then
  begin
    ErrorMsg := '경고: ONNX 모델 파일이 설치되지 않았습니다.' + #13#10 +
                '이는 안티바이러스 소프트웨어의 간섭일 수 있습니다.' + #13#10 + #13#10 +
                '해결 방법:' + #13#10 +
                '1. 안티바이러스 소프트웨어의 실시간 보호를 일시 비활성화' + #13#10 +
                '2. BetaChip을 예외 목록에 추가' + #13#10 +
                '3. 프로그램을 다시 설치';
    MsgBox(ErrorMsg, mbInformation, MB_OK);
  end
  else
  begin
    Log('Model file successfully installed: ' + ModelPath);
  end;
end;

{ ★★★ 후원자 전용: 스티커 파일 검증 ★★★ }
procedure CheckStickerFiles();
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
    Log('Sticker files successfully installed: ' + IntToStr(Count) + ' files');
  end
  else
  begin
    Log('Warning: No sticker files found after installation');
  end;
end;

{ ★★★ 설치 완료 후 최종 검증 ★★★ }
procedure CurStepChanged(CurStep: TSetupStep);
var
  ModelPath: String;
  BackupPath: String;
  StickerCount: Integer;
begin
  if CurStep = ssPostInstall then
  begin
    ModelPath := ExpandConstant('{app}\Resources\best.onnx');
    BackupPath := ExpandConstant('{app}\best.onnx');
    { 메인 모델 파일이 없으면 백업에서 복사 시도 }
    if not FileExists(ModelPath) and FileExists(BackupPath) then
    begin
      Log('Attempting to restore model file from backup...');
      if FileCopy(BackupPath, ModelPath, False) then
        Log('Model file restored from backup successfully')
      else
        Log('Failed to restore model file from backup');
    end;
  end;
end;

{ ★★★ 후원자 버전 환영 메시지 ★★★ }
procedure CurPageChanged(CurPageID: Integer);
begin
  if CurPageID = wpWelcome then
  begin
    WizardForm.WelcomeLabel2.Caption := 
      '이것은 BetaChip 후원자 전용 버전입니다!' + #13#10 + #13#10 +
      '포함된 기능:' + #13#10 +
      '• 🎯 재미있는 스티커 기능' + #13#10 +
      '• 🖥️ 멀티 모니터 지원' + #13#10 +
      '• ⚡ 향상된 성능' + #13#10 +
      '• 🎨 추가 검열 효과' + #13#10 + #13#10 +
      '후원해주셔서 감사합니다!';
  end;
end;

{ ★★★ 시작 전 시스템 요구사항 확인 ★★★ }
function InitializeSetup(): Boolean;
var
  Version: TWindowsVersion;
  ErrorCode: Integer;
begin
  Result := True;
  
  GetWindowsVersionEx(Version);
  
  { Windows 10 이상 확인 }
  if Version.Major < 10 then
  begin
    MsgBox('이 프로그램은 Windows 10 이상에서만 실행됩니다.' + #13#10 +
           '현재 시스템: Windows ' + IntToStr(Version.Major) + '.' + IntToStr(Version.Minor),
           mbError, MB_OK);
    Result := False;
    Exit;
  end;
  
  { .NET 8 런타임 확인 }
  if not RegKeyExists(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\Microsoft.NETCore.App') and
     not RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\Microsoft.NETCore.App') then
  begin
    if MsgBox('이 프로그램을 실행하려면 .NET 8 Runtime이 필요합니다.' + #13#10 + #13#10 +
              '지금 Microsoft 다운로드 페이지를 여시겠습니까?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    
    { .NET이 없어도 설치는 계속 진행 }
    MsgBox('설치를 계속 진행하지만, .NET 8 Runtime 설치 후 프로그램을 실행해주세요.',
           mbInformation, MB_OK);
  end;
end;