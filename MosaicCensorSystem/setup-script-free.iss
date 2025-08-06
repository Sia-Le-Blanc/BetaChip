; Inno Setup 스크립트 - BetaChip 무료 버전
#define MyAppName "BetaChip"
#define MyAppVersion "2.0.0"
#define MyAppPublisher "Sia"
#define MyAppURL "https://github.com/Sia-Le-Blanc/BetaChip"
#define MyAppExeName "MosaicCensorSystem.exe"
; ★★★ 무료 버전 빌드 결과물 경로를 정확히 지정합니다 ★★★
#define MyBuildPath "C:\Users\Sia\OneDrive\바탕 화면\main\BetaChip\MosaicCensorSystem\bin\Release\Free\net8.0-windows"

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
OutputBaseFilename=BetaChip-Setup
SetupIconFile=
Compression=lzma
SolidCompression=yes
WizardStyle=modern
PrivilegesRequired=lowest

[Languages]
Name: "korean"; MessagesFile: "compiler:Languages\Korean.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[Files]
; 중요: Source 경로는 이 스크립트 파일을 기준으로 합니다.
; 무료 버전 빌드 폴더의 모든 파일을 설치 폴더로 복사합니다.
Source: "{#MyBuildPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent