; Inno Setup 스크립트 - BetaChip 무료 버전 (v4.0.0)
; GPU 가속 설정 가이드 기능 추가

#define MyAppName "BetaChip"
#define MyAppVersion "4.0.0"
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
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=admin
SetupLogging=yes

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "autostartup"; Description: "Windows 시작시 자동 실행"; GroupDescription: "추가 옵션"; Flags: unchecked

[Files]
; 1. 메인 프로그램 파일들을 설치합니다.
Source: "{#MyBuildPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; 2. ONNX 모델 파일을 'Resources' 폴더, 단 한 곳에만 설치합니다.
Source: "{#MyBuildPath}\Resources\best.onnx"; DestDir: "{app}\Resources"; Flags: ignoreversion

[Registry]
; 파일의 전체 경로를 저장 (폴더가 아닌 파일 경로)
Root: HKLM64; Subkey: "SOFTWARE\BetaChip\MosaicCensorSystem"; ValueType: string; ValueName: "ModelPath"; ValueData: "{app}\Resources\best.onnx"; Flags: uninsdeletekey

; 폴백용 Resources 폴더 경로도 저장
Root: HKLM64; Subkey: "SOFTWARE\BetaChip\MosaicCensorSystem"; ValueType: string; ValueName: "ResourcesPath"; ValueData: "{app}\Resources"; Flags: uninsdeletekey

; 설치 경로 저장
Root: HKLM64; Subkey: "SOFTWARE\BetaChip\MosaicCensorSystem"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: autostartup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "{localappdata}\{#MyAppName}"

[Code]
function InitializeSetup(): Boolean;
var
  InstalledVersion: String;
begin
  Result := True;
  
  if RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C2A62B8D-8792-4547-B864-763B85B58A2F}_is1', 
                          'DisplayVersion', InstalledVersion) then
  begin
    if MsgBox('기존 BetaChip (' + InstalledVersion + ')이 설치되어 있습니다.' + #13#10 +
              '새 버전({#MyAppVersion})으로 업데이트하시겠습니까?' + #13#10#13#10 +
              '※ 기존 설정은 유지됩니다.', 
              mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
      Exit;
    end;
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    MsgBox('BetaChip v4.0.0 설치가 완료되었습니다!' + #13#10#13#10 +
           '✨ 주요 기능:' + #13#10 +
           '  • 실시간 AI 기반 화면 검열' + #13#10 +
           '  • 다양한 검열 효과 (모자이크/블러/검은박스)' + #13#10 +
           '  • GPU 가속 지원' + #13#10#13#10 +
           '🆕 v4.0.0 업데이트:' + #13#10 +
           '  • GPU 설정 가이드 기능 추가' + #13#10 +
           '  • CUDA/cuDNN 자동 감지 및 설치 안내' + #13#10 +
           '  • cuDNN 자동 복사 기능' + #13#10#13#10 +
           '💡 추가 기능이 필요하다면 후원자 버전을 확인해보세요!', 
           mbInformation, MB_OK);
  end;
end;