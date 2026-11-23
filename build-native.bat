@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-native.ps1"
if errorlevel 1 (
  echo Build failed. Read the messages above.
  pause
  exit /b 1
)
echo Portable program created in dist-native.
pause
