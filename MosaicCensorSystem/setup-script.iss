; Inno Setup 스크립트 예제
; 이 스크립트는 MosaicCensorSystem을 위한 설치 프로그램을 생성합니다.

#define MyAppName "Mosaic Censor System"
#define MyAppVersion "1.0"
#define MyAppPublisher "Sia"
#define MyAppURL "https://github.com/Sia-Le-Blanc/BetaChip"
#define MyAppExeName "MosaicCensorSystem.exe"
#define MyBuildPath "bin\x64\Release\net8.0-windows"

[Setup]
; 참고: AppId는 고유해야 합니다.
AppId={{C1542A9B-9883-4246-A487-752A94A49A1F}}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DisableProgramGroupPage=yes
; 최종 설치 파일이 저장될 위치와 이름 설정
OutputDir=.\install
OutputBaseFilename=MosaicCensorSystem-Setup
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
; TODO: 만약 Resources 폴더가 빌드 경로에 없다면 아래 줄의 주석을 해제하고 경로를 맞추세요.
; Source: ".\Resources\*"; DestDir: "{app}\Resources"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent