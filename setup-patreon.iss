; Inno Setup 스크립트 - BetaChip 후원자용 (v2.4.2 개정판)
; 레지스트리 기반 경로 관리로 안정성 극대화

#define MyAppName "BetaChip" ; 레지스트리 키 공유를 위해 내부 이름은 통일
#define MyAppDisplayName "BetaChip - 후원자용" ; 사용자에게 보여지는 이름
#define MyAppVersion "2.4.2-Patreon"
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
; 모델과 스티커 폴더의 절대 경로를 레지스트리에 기록합니다.
Root: HKLM64; Subkey: "SOFTWARE\{#MyAppName}\MosaicCensorSystem"; ValueType: string; ValueName: "ModelPath"; ValueData: "{app}\Resources\best.onnx"; Flags: uninsdeletekey
Root: HKLM64; Subkey: "SOFTWARE\{#MyAppName}\MosaicCensorSystem"; ValueType: string; ValueName: "StickerPath"; ValueData: "{app}\Stickers"; Flags: uninsdeletekey

[Icons]
Name: "{autoprograms}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon
Name: "{userstartup}\{#MyAppDisplayName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: autostartup

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppDisplayName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
Type: filesandordirs; Name: "{app}"
Type: filesandordirs; Name: "{localappdata}\{#MyAppName}"