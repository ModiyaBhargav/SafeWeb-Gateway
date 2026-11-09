@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-And-Run.ps1"
if errorlevel 1 (
  echo.
  echo SafeWeb could not start. Review the error above.
  pause
)
endlocal
