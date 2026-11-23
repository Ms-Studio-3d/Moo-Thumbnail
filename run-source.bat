@echo off
setlocal
cd /d "%~dp0"
where npm >nul 2>&1
if errorlevel 1 (
  echo Install Node.js 24 LTS first. Portable EXE users do not need Node.js.
  pause
  exit /b 1
)
if not exist node_modules\electron\dist\electron.exe (
  call npm ci --no-audit --no-fund
  if errorlevel 1 goto failed
)
call npm start
if errorlevel 1 goto failed
exit /b 0
:failed
echo Could not start the app. Read the error messages above.
pause
exit /b 1
