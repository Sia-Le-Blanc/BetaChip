@echo off
setlocal enabledelayedexpansion
chcp 65001 >nul
title BetaChip 자동 진단 도구

echo ╔════════════════════════════════════════╗
echo ║   BetaChip 자동 진단 및 복구 도구     ║
echo ╚════════════════════════════════════════╝
echo.

:: 1. Windows 버전 확인
echo [1/5] Windows 버전 확인 중...
for /f "tokens=4-5 delims=. " %%i in ('ver') do set VERSION=%%i.%%j
echo     ✓ Windows %VERSION% 감지됨

:: 2. DPI 설정 확인
echo [2/5] DPI 설정 확인 중...
for /f "tokens=3" %%a in ('reg query "HKCU\Control Panel\Desktop\WindowMetrics" /v AppliedDPI 2^>nul ^| findstr AppliedDPI') do set DPI=%%a
if not defined DPI set DPI=96
set /a SCALE=%DPI%*100/96
echo     ✓ 현재 DPI: %DPI% (배율: %SCALE%%%)

:: 3. 모니터 수 확인  
echo [3/5] 모니터 구성 확인 중...
set MONITOR_COUNT=0
for /f %%i in ('wmic path Win32_PnPEntity where "Service='monitor'" get Status 2^>nul ^| findstr "OK"') do set /a MONITOR_COUNT+=1
echo     ✓ 활성 모니터 수: %MONITOR_COUNT%

:: 4. 문제 감지
echo [4/5] 잠재적 문제 확인 중...
set ISSUES=0
set RECOMMEND_COMPAT=NO

:: DPI 문제 확인
if %SCALE% GTR 125 (
    echo     ⚠ 높은 DPI 스케일 감지됨 (%SCALE%%%)
    set /a ISSUES+=1
    set RECOMMEND_COMPAT=YES
)

:: 멀티모니터 + 높은 DPI
if %MONITOR_COUNT% GTR 1 if %SCALE% GTR 100 (
    echo     ⚠ 멀티모니터 + DPI 스케일링 조합 감지됨
    set /a ISSUES+=1
    set RECOMMEND_COMPAT=YES
)

:: Windows 11 특수 처리
echo %VERSION% | findstr "10.0.22" >nul
if %errorlevel%==0 (
    echo     ⚠ Windows 11 감지 - 추가 호환성 검사 필요
    set /a ISSUES+=1
)

if %ISSUES%==0 (
    echo     ✓ 문제가 감지되지 않았습니다.
) else (
    echo     ⚠ %ISSUES%개의 잠재적 문제가 감지되었습니다.
)

:: 5. 권장 사항
echo [5/5] 권장 실행 방법 결정 중...
echo.
echo ════════════════════════════════════════
echo          진단 결과 및 권장사항
echo ════════════════════════════════════════

if "%RECOMMEND_COMPAT%"=="YES" (
    echo.
    echo 🔧 권장: 호환성 모드로 실행
    echo    이유: 화면 확대/축소 문제 가능성
    echo.
    echo 실행하시겠습니까? (Y/N)
    set /p run_choice=선택: 
    
    if /i "!run_choice!"=="Y" (
        echo.
        echo 호환성 모드로 BetaChip을 실행합니다...
        start "" "%~dp0MosaicCensorSystem.exe" --compat
    ) else (
        echo.
        echo 일반 모드로 실행하려면:
        echo   %~dp0MosaicCensorSystem.exe
        echo.
        echo 호환성 모드로 실행하려면:
        echo   %~dp0MosaicCensorSystem.exe --compat
    )
) else (
    echo.
    echo ✅ 시스템이 정상입니다.
    echo.
    echo BetaChip을 실행하시겠습니까? (Y/N)
    set /p run_choice=선택: 
    
    if /i "!run_choice!"=="Y" (
        echo.
        echo BetaChip을 실행합니다...
        start "" "%~dp0MosaicCensorSystem.exe"
    )
)

echo.
echo ════════════════════════════════════════
echo.

:: 추가 도움말
if %ISSUES% GTR 0 (
    echo 💡 추가 해결 방법:
    echo.
    echo 1. Windows 디스플레이 설정 조정:
    echo    설정 → 시스템 → 디스플레이 → 배율을 100%%로 변경
    echo.
    echo 2. 프로그램별 DPI 설정:
    echo    MosaicCensorSystem.exe 우클릭 → 속성 → 호환성
    echo    → "높은 DPI 설정 변경" → "시스템(고급)" 선택
    echo.
    echo 3. 레지스트리 설정 (고급):
    echo    이 스크립트를 관리자 권한으로 다시 실행하면
    echo    자동으로 레지스트리를 수정할 수 있습니다.
    echo.
)

echo 진단 도구를 종료하려면 아무 키나 누르세요...
pause >nul