; Inno Setup 스크립트 - 베타칩 후원자용
; 이 스크립트는 스티커 기능이 포함된 버전을 설치합니다.
#define MyAppName "베타칩 - 후원자용"
#define MyAppVersion "1.0-Patreon"
#define MyAppPublisher "Sia"
#define MyAppURL "https://github.com/Sia-Le-Blanc/BetaChip"
#define MyAppExeName "MosaicCensorSystem.exe"
; ★★★ 빌드 경로를 후원자용으로 변경 ★★★
#define MyBuildPath "bin\x64\ReleasePatreon\net8.0-windows"

[Setup]
; 중요: AppId를 기존 버전과 다르게 설정하여 충돌을 방지합니다.
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
OutputBaseFilename=BetaChip-Patreon-Setup
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
Source: "{#MyBuildPath}\{#MyAppExeName}"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#MyBuildPath}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
; 스티커와 모델 파일이 포함된 Resources 폴더 전체를 복사합니다.
Source: ".\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs


[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent