; Inno Setup 스크립트 - BetaChip 후원자용 (v3.0.0 개정판)
; 레지스트리 기반 경로 관리로 안정성 극대화

#define MyAppName "BetaChip"
#define MyAppDisplayName "BetaChip - 후원자용"
#define MyAppVersion "3.0.0-Patreon"
#define MyAppPublisher "Sia"
#define MyAppURL "https://github.com/Sia-Le-Blanc/BetaChip"
#define MyAppExeName "MosaicCensorSystem.exe"
#define MyBuildPath "C:\Users\Sia\OneDrive\바탕 화면\main\BetaChip\MosaicCensorSystem\bin\Release\Patreon\net8.0-windows"

[Setup]
AppId={{D2633B8C-8792-4547-B864-763B85B58A2F}}
AppName={#MyAppDisplayName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppDisplayName}
DisableProgramGroupPage=yes
OutputDir=.\install
OutputBaseFilename=BetaChip-Patreon-v{#MyAppVersion}-Setup
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
; 3. 후원자용 스티커 파일들을 'Stickers' 폴더, 단 한 곳에만 설치합니다.
Source: "{#MyBuildPath}\Stickers\*"; DestDir: "{app}\Stickers"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; 파일의 전체 경로를 저장
Root: HKLM64; Subkey: "SOFTWARE\{#MyAppName}\MosaicCensorSystem"; ValueType: string; ValueName: "ModelPath"; ValueData: "{app}\Resources\best.onnx"; Flags: uninsdeletekey
Root: HKLM64; Subkey: "SOFTWARE\{#MyAppName}\MosaicCensorSystem"; ValueType: string; ValueName: "StickerPath"; ValueData: "{app}\Stickers"; Flags: uninsdeletekey

; 폴백용 경로들
Root: HKLM64; Subkey: "SOFTWARE\{#MyAppName}\MosaicCensorSystem"; ValueType: string; ValueName: "ResourcesPath"; ValueData: "{app}\Resources"; Flags: uninsdeletekey
Root: HKLM64; Subkey: "SOFTWARE\{#MyAppName}\MosaicCensorSystem"; ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey

[Icons]
Name: "{autoprograms}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: autostartup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppDisplayName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "{localappdata}\{#MyAppName}"

[Code]
function InitializeSetup(): Boolean;
var
  InstalledVersion: String;
begin
  Result := True;
  
  if RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{D2633B8C-8792-4547-B864-763B85B58A2F}_is1', 
                          'DisplayVersion', InstalledVersion) then
  begin
    if MsgBox('기존 BetaChip 후원자 버전 (' + InstalledVersion + ')이 설치되어 있습니다.' + #13#10 +
              '새 버전({#MyAppVersion})으로 업데이트하시겠습니까?' + #13#10#13#10 +
              '※ 기존 설정은 유지됩니다.', 
              mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
      Exit;
    end;
  end
  else if RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{C2A62B8D-8792-4547-B864-763B85B58A2F}_is1', 
                               'DisplayVersion', InstalledVersion) then
  begin
    MsgBox('기존 BetaChip 무료 버전이 설치되어 있습니다.' + #13#10 +
           '후원자 버전으로 업그레이드합니다!' + #13#10#13#10 +
           '추가 기능: 멀티 모니터 지원, 스티커', 
           mbInformation, MB_OK);
  end;
end;

procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    MsgBox('BetaChip 후원자 버전 설치가 완료되었습니다!' + #13#10#13#10 +
           '✨ 포함된 기능:' + #13#10 +
           '  • 실시간 AI 검열' + #13#10 +
           '  • 멀티 모니터 지원' + #13#10 +
           '  • 스티커 기능' + #13#10#13#10 +
           '💡 감사합니다!', 
           mbInformation, MB_OK);
  end;
end;