@echo off
setlocal
cd /d "%~dp0"

REM One-click SafeWeb local development launcher.
REM It automatically starts elevated, bypasses the per-process PowerShell
REM execution policy, installs WinDivert once if needed, builds, tests it,
REM and starts the SafeWeb Gateway.

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\run-dev.ps1"

if errorlevel 1 (
    echo.
    echo SafeWeb stopped with an error. See the PowerShell output above.
    pause
)
endlocal
