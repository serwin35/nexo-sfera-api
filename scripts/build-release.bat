@echo off
:: Wrapper dla build-release.ps1
:: Uruchom: build-release.bat [wersja]
:: Przykład: build-release.bat 1.0.0

setlocal

set "VERSION=%~1"
set "SCRIPT_DIR=%~dp0"

if "%VERSION%"=="" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build-release.ps1"
) else (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%build-release.ps1" -Version "%VERSION%"
)

if %ERRORLEVEL% neq 0 (
    echo.
    echo Build nie powiodl sie. Sprawdz bledy powyzej.
    pause
    exit /b %ERRORLEVEL%
)

echo.
pause
