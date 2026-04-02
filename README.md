# BetaChip — MosaicCensorSystem

실시간 화면 검열 솔루션. 화면을 캡처하고 YOLOv8 ONNX 모델로 감지 대상을 찾아 모자이크/블러/검은박스 오버레이를 씌운다.

---

## 목차

1. [프로젝트 개요](#1-프로젝트-개요)
2. [기술 스택](#2-기술-스택)
3. [필수 환경 설정 (처음 클론 후 반드시)](#3-필수-환경-설정-처음-클론-후-반드시)
4. [빌드 및 실행](#4-빌드-및-실행)
5. [현재 개발 상태](#5-현재-개발-상태)
6. [알려진 이슈](#6-알려진-이슈)
7. [프로젝트 구조](#7-프로젝트-구조)
8. [아키텍처 개요](#8-아키텍처-개요)
9. [구독 티어 및 기능 제한](#9-구독-티어-및-기능-제한)
10. [Git 주의사항](#10-git-주의사항)

---

## 1. 프로젝트 개요

| 항목 | 내용 |
|------|------|
| 버전 | 6.0.0 (Unified Edition) |
| 대상 OS | Windows 10/11, x64 전용 |
| UI 프레임워크 | Windows Forms (.NET 8.0) |
| AI 모델 | YOLOv8 (HBB / OBB 각 1개) |

**동작 방식:**
1. 화면을 실시간 캡처 (Win32 P/Invoke)
2. YOLOv8 ONNX 모델로 감지 영역 추론 (GPU 가속)
3. SORT 알고리즘으로 오브젝트 트래킹
4. 투명 전체화면 오버레이 Form 위에 검열 렌더링

---

## 2. 기술 스택

| 구성 요소 | 버전 |
|-----------|------|
| .NET SDK | **8.0.412** (global.json으로 고정) |
| C# | latest (LangVersion) |
| ONNX Runtime GPU | 1.19.2 |
| OpenCvSharp4 | 4.9.0.20240103 |
| System.Management | 8.0.0 |

---

## 3. 필수 환경 설정 (처음 클론 후 반드시)

### 3-1. .NET 8 SDK 설치

```
winget install Microsoft.DotNet.SDK.8
```
또는 [https://dotnet.microsoft.com/download/dotnet/8.0](https://dotnet.microsoft.com/download/dotnet/8.0) 에서 **SDK 8.0.412** 이상 설치.

설치 확인:
```
dotnet --version
# 8.0.412 이상이어야 함
```

### 3-2. NVIDIA GPU 드라이버 (CUDA 12.x 지원)

ONNX Runtime GPU 1.19.2는 CUDA 12.x 기반. GPU 없으면 CPU fallback으로 동작하지만 속도 저하.

- [NVIDIA Driver Downloads](https://www.nvidia.com/Download/index.aspx)
- CUDA Toolkit은 별도 설치 불필요 (ONNX Runtime이 자체 cuDNN 포함)

### 3-3. Visual Studio 2022 또는 VS Code + C# Dev Kit

**VS Code 사용 시:**
- [C# Dev Kit 확장](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csdevkit) 설치
- [.NET 8 확장](https://marketplace.visualstudio.com/items?itemName=ms-dotnettools.csharp) 설치

**Visual Studio 2022 사용 시:**
- ".NET 데스크톱 개발" 워크로드 포함해서 설치

### 3-4. Sticker 이미지 파일 복원 (중요)

`.gitignore`에 `*.png`가 등록되어 있어 **스티커 PNG 파일들은 git에서 제외됨**.
스티커 기능을 사용하려면 별도로 파일을 복사해야 한다.

필요 위치:
```
Resources/
  Stickers/           ← PNG 파일들 여기에
  OverlayText/        ← 텍스트 오버레이 PNG들 여기에
```

스티커 없어도 모자이크/블러/검은박스 기능은 정상 동작.

### 3-5. NuGet 패키지 복원

```
cd BetaChip
dotnet restore
```

---

## 4. 빌드 및 실행

### 빌드

```bash
# Debug 빌드
dotnet build MosaicCensorSystem/MosaicCensorSystem.csproj -c Debug

# Release 빌드
dotnet build MosaicCensorSystem/MosaicCensorSystem.csproj -c Release
```

빌드 결과물 위치:
```
MosaicCensorSystem/bin/Debug/net8.0-windows/Unified/
MosaicCensorSystem/bin/Release/net8.0-windows/Unified/
```

### 실행 방법

**방법 1: 배치 파일 (추천)**
```
RunBetaChip.bat
```
메뉴에서 선택:
- `1` — 일반 실행
- `2` — 호환 모드 (고DPI / 멀티모니터 문제 시)
- `3` — 관리자 권한으로 실행
- `6` — 개발 모드 (오프라인, mock 구독 데이터)

**방법 2: dotnet CLI**
```bash
dotnet run --project MosaicCensorSystem/MosaicCensorSystem.csproj

# 호환 모드
dotnet run --project MosaicCensorSystem/MosaicCensorSystem.csproj -- --compat

# 개발 모드 (DEBUG 빌드 전용)
dotnet run --project MosaicCensorSystem/MosaicCensorSystem.csproj -- --dev
```

### 타입 체크 (빌드 전 검증)

```bash
npx tsc --noEmit  # 해당 없음 (C# 프로젝트)
dotnet build --no-restore 2>&1 | grep -E "error|warning"
```

---

## 5. 현재 개발 상태

### 최근 작업 내용 (2026-04 기준)

최근 커밋 히스토리:
- **UI 대격변** — MockupUIForm 전면 재설계
- **UI 대폭 수정** — 기존 UI 대규모 수정
- 그 이전: OBB 모델 탐지 경로 문제 수정, 속도 테스트 진행 중

### 작업 진행 중인 사항

- [ ] `MosaicProcessor.cs` 빌드 에러 수정 (아래 이슈 참고)
- [ ] UI 재설계 이후 기능 연동 검증
- [ ] 성능 벤치마크 (속도 테스트)

---

## 6. 알려진 이슈

### [CS0111] MosaicProcessor.cs — Preprocess 중복 메서드 에러

**파일:** `MosaicCensorSystem/Detection/MosaicProcessor.cs` 주변 324번째 라인  
**에러:** `CS0111: 'MosaicProcessor' 형식이 이미 동일한 매개변수 형식으로 'Preprocess' 멤버를 정의합니다`  
**원인:** UI 대격변 작업 중 동일한 시그니처의 `Preprocess` 메서드가 두 번 정의된 것으로 추정  
**해결:** 중복된 두 번째 `Preprocess` 메서드 제거 또는 시그니처 변경

확인 방법:
```bash
grep -n "private.*Preprocess" MosaicCensorSystem/Detection/MosaicProcessor.cs
```

### 구독 API 서버 연결 (로컬 서버 필요)

앱 실행 시 `http://localhost:5020/api/subscription/{userId}` 에 연결 시도.  
서버 없으면 자동으로 오프라인 모드 전환 (기능 제한 없이 동작).  
**개발 시:** `--dev` 플래그 사용하면 mock 데이터로 구독 상태 시뮬레이션.

### GPU 미감지 시 초기 설정 팝업

처음 실행 시 `gpu_checked.flag` 파일이 없으면 GPU 설정 다이얼로그(`GpuSetupForm`)가 뜸.  
한 번 설정 후 파일이 생성되므로 이후 실행부터는 뜨지 않음.  
git에서 제외된 파일이므로 새 환경에서는 항상 처음에 한 번 뜸. (정상 동작)

---

## 7. 프로젝트 구조

```
BetaChip/
├── MosaicCensorSystem/
│   ├── Capture/
│   │   └── ScreenCapturer.cs          # Win32 P/Invoke 기반 화면 캡처
│   ├── Detection/
│   │   ├── MosaicProcessor.cs         # ONNX Runtime 추론 엔진 (핵심)
│   │   ├── IProcessor.cs
│   │   └── SortTracker.cs             # 오브젝트 트래킹 (SORT 알고리즘)
│   ├── Diagnostics/
│   │   └── OnnxDiagnostics.cs         # ONNX 진단 유틸
│   ├── Helpers/
│   │   └── GpuDetector.cs             # GPU 감지
│   ├── Management/
│   │   ├── IOverlayManager.cs
│   │   ├── MultiMonitorManager.cs     # 멀티모니터 오버레이 관리
│   │   └── SingleMonitorManager.cs
│   ├── Models/
│   │   └── SubscriptionInfo.cs        # 구독 티어 모델
│   ├── Overlay/
│   │   ├── FullscreenOverlay.cs       # 전체화면 투명 Form
│   │   ├── IOverlay.cs
│   │   └── OverlayTextManager.cs      # 오버레이 텍스트 렌더링
│   ├── Properties/
│   │   ├── Strings.resx               # 한국어 문자열 (기본)
│   │   └── Strings.en.resx            # 영어 문자열 (폴백)
│   ├── Services/
│   │   └── ApiService.cs              # 구독 API REST 통신
│   ├── UI/
│   │   ├── MockupUIForm.cs            # 메인 UI Form (최근 대격변)
│   │   ├── GuiController.cs           # UI 이벤트 컨트롤러
│   │   ├── IGuiController.cs
│   │   ├── GpuSetupForm.cs            # GPU 설정 다이얼로그
│   │   └── ScrollablePanel.cs
│   ├── Utils/
│   │   ├── DisplayCompatibility.cs    # DPI 스케일링 호환성 처리
│   │   └── UserSettings.cs            # 사용자 설정 저장
│   ├── CensorService.cs               # 핵심 서비스 오케스트레이터
│   ├── MosaicApp.cs                   # 앱 초기화 (구독 로드 → UI 실행)
│   ├── Program.cs                     # 진입점 (CLI 파싱, 모델 파일 검증)
│   ├── Config.cs                      # 전역 상수
│   └── ModelRegistry.cs               # AI 모델 메타데이터 레지스트리
│
├── Resources/
│   ├── best.onnx                      # HBB 모델 (~12MB) ← git 추적됨
│   ├── bestobb.onnx                   # OBB 모델 (~39MB) ← git 추적됨
│   ├── Stickers/                      # PNG 파일 ← git 제외 (*.png)
│   └── OverlayText/                   # 텍스트 오버레이 PNG ← git 제외
│
├── MosaicCensorSystem.sln
├── global.json                        # .NET SDK 8.0.412 버전 고정
├── Directory.Build.props              # 전역 MSBuild 속성
├── RunBetaChip.bat                    # 실행 메뉴 배치 파일
├── BetaChip_AutoDiagnose.bat          # DPI/모니터 자동 진단
├── CLAUDE.md                          # AI 에이전트 지시문
└── README.md                          # 이 파일
```

---

## 8. 아키텍처 개요

```
Program.cs (진입점)
  └── MosaicApp.cs (앱 생명주기)
        ├── ApiService → 구독 정보 로드
        ├── MockupUIForm (메인 UI)
        └── CensorService (서비스 오케스트레이터)
              ├── ScreenCapturer (화면 캡처 루프)
              ├── MosaicProcessor (ONNX 추론)
              │     └── SortTracker (오브젝트 트래킹)
              └── IOverlayManager
                    ├── SingleMonitorManager
                    └── MultiMonitorManager
                          └── FullscreenOverlay (렌더링)
```

### 주요 설계 원칙

- **Thread-local 추론 컨텍스트**: `InferenceContext`를 스레드별로 분리해 레이스 컨디션 방지
- **구독 게이팅**: 모든 프리미엄 기능은 `SubscriptionInfo.Tier` 체크 후 활성화
- **오프라인 폴백**: API 서버 없으면 자동으로 오프라인 모드
- **DPI 호환성**: `DisplayCompatibility.cs`에서 고DPI 환경 전처리

### AI 모델

| 모델 파일 | 방식 | 특징 |
|-----------|------|------|
| `best.onnx` | HBB (수평 바운딩박스) | 기본 모델, 필수 |
| `bestobb.onnx` | OBB (회전 바운딩박스) | 선택적, 없으면 UI에서 비활성화 |

추론 파이프라인: `Preprocess (패딩+정규화)` → `ONNX 세션 실행` → `Postprocess (NMS)` → `SortTracker`

---

## 9. 구독 티어 및 기능 제한

| 티어 | 멀티모니터 | 캡션 | 프리미엄 스티커 |
|------|-----------|------|----------------|
| free | ✗ | ✗ | ✗ |
| plus | ✓ | ✓ | ✓ |
| patreon | ✓ | ✓ | ✓ |

> **DEBUG 빌드에서는 항상 `patreon` 티어로 강제** — 개발 중 기능 제한 없음

---

## 10. Git 주의사항

### git에서 제외된 파일 목록

| 패턴 | 이유 |
|------|------|
| `bin/`, `obj/` | 빌드 결과물 |
| `*.exe`, `*.dll`, `*.pdb` | 바이너리 |
| `*.png` | 스티커/오버레이 이미지 |
| `*.log` | 로그 파일 |
| `gpu_checked.flag` | 로컬 GPU 설정 플래그 |
| `install/` | 인스톨러 빌드 결과물 |

### ONNX 모델 파일 (git 추적됨)

`Resources/best.onnx` (~12MB), `Resources/bestobb.onnx` (~39MB) 는 **git에 포함**.  
`git clone` 또는 `git pull` 후 별도 다운로드 불필요.

단, LFS 없이 일반 git으로 추적 중이므로 저장소 크기가 큼 (~50MB+).  
클론 속도가 느리면 정상.
