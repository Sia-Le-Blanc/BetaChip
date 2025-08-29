; Inno Setup 스크립트 - BetaChip 무료 버전 v2.3.0
#define MyAppName "BetaChip"
#define MyAppVersion "2.3.0"
#define MyAppPublisher "Sia"
#define MyAppURL "https://github.com/Sia-Le-Blanc/BetaChip"
#define MyAppExeName "MosaicCensorSystem.exe"
; ★★★ 빌드 결과물 경로 - 상대 경로 사용으로 이식성 향상 ★★★
#define MyBuildPath "MosaicCensorSystem\bin\Release\Free\net8.0-windows"

[Setup]
; 중요: AppId를 유료 버전과 다르게 설정하여 충돌을 방지합니다.
AppId={{C2A62B8D-8792-4547-B864-763B85B58A2F}}
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
OutputBaseFilename=BetaChip-v{#MyAppVersion}-Setup
SetupIconFile=
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest
; ★★★ 설치 과정에서 더 나은 사용자 경험 ★★★
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
; ★★★ 메인 프로그램 파일들 복사 ★★★
Source: "{#MyBuildPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

; ★★★ 중요한 모델 파일을 다중 위치에 백업 복사 (안전성 향상) ★★★
Source: "{#MyBuildPath}\Resources\best.onnx"; DestDir: "{app}"; Flags: ignoreversion; AfterInstall: CheckModelFile
Source: "{#MyBuildPath}\Resources\best.onnx"; DestDir: "{app}\Resources"; Flags: ignoreversion
; 사용자 데이터 폴더에도 백업 (안티바이러스 간섭 대비)
Source: "{#MyBuildPath}\Resources\best.onnx"; DestDir: "{userappdata}\{#MyAppName}"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userappdata}\Microsoft\Internet Explorer\Quick Launch\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: quicklaunchicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; 설치 중 생성된 로그 파일들도 삭제
Type: files; Name: "{app}\*.log"
Type: files; Name: "{app}\fatal_error.log"
Type: files; Name: "{app}\init_error.log"

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

{ ★★★ 설치 완료 후 최종 검증 ★★★ }
procedure CurStepChanged(CurStep: TSetupStep);
var
  ModelPath: String;
  BackupPath: String;
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
  
  { .NET 8 런타임 확인 (간단한 체크) }
  if not RegKeyExists(HKLM, 'SOFTWARE\dotnet\Setup\InstalledVersions\x64\Microsoft.NETCore.App') and
     not RegKeyExists(HKLM, 'SOFTWARE\WOW6432Node\dotnet\Setup\InstalledVersions\x64\Microsoft.NETCore.App') then
  begin
    if MsgBox('이 프로그램을 실행하려면 .NET 8 Runtime이 필요합니다.' + #13#10 + #13#10 +
              '지금 Microsoft 다운로드 페이지를 여시겠습니까?',
              mbConfirmation, MB_YESNO) = IDYES then
    begin
      ShellExec('open', 'https://dotnet.microsoft.com/download/dotnet/8.0', '', '', SW_SHOWNORMAL, ewNoWait, ErrorCode);
    end;
    
    { .NET이 없어도 설치는 계속 진행 (런타임에서 오류 메시지 표시) }
    MsgBox('설치를 계속 진행하지만, .NET 8 Runtime 설치 후 프로그램을 실행해주세요.',
           mbInformation, MB_OK);
  end;
end;