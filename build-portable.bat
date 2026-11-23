@echo off
setlocal
cd /d "%~dp0"
where npm >nul 2>&1
if errorlevel 1 (
  echo Install Node.js 24 LTS first. This script is for building from source only.
  pause
  exit /b 1
)
call npm ci --no-audit --no-fund
if errorlevel 1 goto failed
call npm test
if errorlevel 1 goto failed
call npm run test:smoke
if errorlevel 1 goto failed
call npm run dist
if errorlevel 1 goto failed
echo Portable EXE created in the dist folder. No installation needed.
pause
exit /b 0
:failed
echo Build failed. Read the error messages above.
pause
exit /b 1
