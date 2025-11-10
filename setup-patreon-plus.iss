; Inno Setup 스크립트 - BetaChip 후원자 플러스용 (v3.0.0)
; 레지스트리 기반 경로 관리 + 캡션 기능 포함

#define MyAppName "BetaChip"
#define MyAppDisplayName "BetaChip - 후원자 플러스용"
#define MyAppVersion "3.0.0-PatreonPlus"
#define MyAppPublisher "Sia"
#define MyAppURL "https://github.com/Sia-Le-Blanc/BetaChip"
#define MyAppExeName "MosaicCensorSystem.exe"
#define MyBuildPath "C:\Users\Sia\OneDrive\바탕 화면\main\BetaChip\MosaicCensorSystem\bin\Release\PatreonPlus\net8.0-windows"

[Setup]
AppId={{E3744C9D-9903-4658-C975-874C96C69D3F}}
AppName={#MyAppDisplayName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppDisplayName}
DisableProgramGroupPage=yes
OutputDir=.\install
OutputBaseFilename=BetaChip-PatreonPlus-v{#MyAppVersion}-Setup
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
; 3. 후원자 플러스용 스티커 파일들을 'Stickers' 폴더에 설치합니다.
Source: "{#MyBuildPath}\Stickers\*"; DestDir: "{app}\Stickers"; Flags: ignoreversion recursesubdirs createallsubdirs
; 4. 후원자 플러스 전용: 캡션(OverlayText) 파일들을 'Resources\OverlayText' 폴더에 설치합니다.
Source: "{#MyBuildPath}\Resources\OverlayText\*"; DestDir: "{app}\Resources\OverlayText"; Flags: ignoreversion recursesubdirs createallsubdirs

[Registry]
; 모델, 스티커, 캡션 폴더의 절대 경로를 레지스트리에 기록합니다.
Root: HKLM64; Subkey: "SOFTWARE\{#MyAppName}\MosaicCensorSystem"; ValueType: string; ValueName: "ModelPath"; ValueData: "{app}\Resources\best.onnx"; Flags: uninsdeletekey
Root: HKLM64; Subkey: "SOFTWARE\{#MyAppName}\MosaicCensorSystem"; ValueType: string; ValueName: "StickerPath"; ValueData: "{app}\Stickers"; Flags: uninsdeletekey
Root: HKLM64; Subkey: "SOFTWARE\{#MyAppName}\MosaicCensorSystem"; ValueType: string; ValueName: "CaptionPath"; ValueData: "{app}\Resources\OverlayText"; Flags: uninsdeletekey

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
// 설치 전 버전 체크 및 안내
function InitializeSetup(): Boolean;
var
  InstalledVersion: String;
  ResultCode: Integer;
begin
  Result := True;
  
  // 기존 버전이 설치되어 있는지 확인
  if RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{E3744C9D-9903-4658-C975-874C96C69D3F}_is1', 
                          'DisplayVersion', InstalledVersion) then
  begin
    if MsgBox('기존 BetaChip 후원자 플러스 버전 (' + InstalledVersion + ')이 설치되어 있습니다.' + #13#10 +
              '새 버전({#MyAppVersion})으로 업데이트하시겠습니까?' + #13#10#13#10 +
              '※ 기존 설정은 유지됩니다.', 
              mbConfirmation, MB_YESNO) = IDNO then
    begin
      Result := False;
      Exit;
    end;
  end
  // 일반 후원자 버전이 설치되어 있는지 확인
  else if RegQueryStringValue(HKLM64, 'SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\{D2633B8C-8792-4547-B864-763B85B58A2F}_is1', 
                               'DisplayVersion', InstalledVersion) then
  begin
    MsgBox('기존 BetaChip 후원자 버전이 설치되어 있습니다.' + #13#10 +
           '후원자 플러스 버전으로 업그레이드합니다!' + #13#10#13#10 +
           '추가 기능: 캡션 기능 (화면에 랜덤 텍스트 표시)', 
           mbInformation, MB_OK);
  end;
end;

// 설치 완료 후 안내 메시지
procedure CurStepChanged(CurStep: TSetupStep);
begin
  if CurStep = ssPostInstall then
  begin
    MsgBox('BetaChip 후원자 플러스 버전 설치가 완료되었습니다!' + #13#10#13#10 +
           '✨ 포함된 기능:' + #13#10 +
           '  • 실시간 AI 검열' + #13#10 +
           '  • 멀티 모니터 지원' + #13#10 +
           '  • 스티커 기능' + #13#10 +
           '  • 캡션 기능 (NEW!)' + #13#10#13#10 +
           '💡 캡션 기능은 UI에서 "캡션 활성화" 체크박스로 켜고 끌 수 있습니다.' + #13#10 +
           '   감지 시 3~8초마다 화면에 랜덤한 위치에 텍스트가 나타납니다.', 
           mbInformation, MB_OK);
  end;
end;