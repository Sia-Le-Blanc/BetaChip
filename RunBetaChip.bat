@echo off
chcp 65001 >nul
title BetaChip 실행 도우미

echo ========================================
echo     BetaChip 실행 도우미 v1.0
echo ========================================
echo.

:menu
echo 실행 옵션을 선택하세요:
echo.
echo [1] 일반 실행
echo [2] 호환성 모드로 실행 (화면 문제 해결)
echo [3] 관리자 권한으로 실행
echo [4] 디스플레이 설정 확인
echo [5] 오류 로그 확인
echo [6] 개발자 모드로 실행 (오프라인/Mock 테스트, DEBUG 빌드 전용)
echo [7] 종료
echo.
set /p choice=선택 (1-7):

if "%choice%"=="1" goto normal
if "%choice%"=="2" goto compat
if "%choice%"=="3" goto admin
if "%choice%"=="4" goto checkdisplay
if "%choice%"=="5" goto errorlog
if "%choice%"=="6" goto devmode
if "%choice%"=="7" goto end

echo 잘못된 선택입니다. 다시 선택해주세요.
echo.
goto menu

:normal
echo.
echo 일반 모드로 실행합니다...
start "" "%~dp0MosaicCensorSystem.exe"
goto end

:compat
echo.
echo 호환성 모드로 실행합니다...
echo (화면 확대/축소 문제가 해결될 수 있습니다)
start "" "%~dp0MosaicCensorSystem.exe" --compat
goto end

:admin
echo.
echo 관리자 권한으로 실행합니다...
powershell -Command "Start-Process '%~dp0MosaicCensorSystem.exe' -Verb RunAs"
goto end

:checkdisplay
echo.
echo 현재 디스플레이 설정:
echo ----------------------------------------
wmic path Win32_DesktopMonitor get Caption,ScreenHeight,ScreenWidth /format:list 2>nul | findstr /v "^$"
echo.
echo DPI 설정:
reg query "HKCU\Control Panel\Desktop" /v LogPixels 2>nul
echo.
echo Windows 버전:
ver
echo.
echo ----------------------------------------
echo.
pause
cls
goto menu

:errorlog
echo.
echo 오류 로그 확인 중...
echo ----------------------------------------
set logpath=%LOCALAPPDATA%\BetaChip\error.log
if exist "%logpath%" (
    echo 최근 오류 내역:
    echo.
    type "%logpath%" | more
) else (
    echo 오류 로그가 없습니다.
)
echo ----------------------------------------
echo.
pause
cls
goto menu

:devmode
echo.
echo [개발자 모드] 실행합니다...
echo 실제 API 서버 없이 Mock 데이터로 동작합니다.
echo (DEBUG 빌드에서만 유효하며, 릴리즈 빌드에서는 일반 모드로 동작합니다)
echo.
set BETACHIP_DEV_MODE=true
start "" "%~dp0MosaicCensorSystem.exe" --dev
set BETACHIP_DEV_MODE=
goto end

:end
echo.
echo 프로그램을 종료합니다.
timeout /t 2 >nul
exit