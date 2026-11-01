@echo off
setlocal
cd /d "%~dp0"
where node >nul 2>nul
if errorlevel 1 (
  echo Install Node.js 22 or newer from https://nodejs.org/ and reopen this file.
  pause
  exit /b 1
)
node scripts\check-local.cjs
if errorlevel 1 (
  echo See docs\operations\team-local-onboarding.md for setup instructions.
  pause
  exit /b 1
)
pwsh -NoProfile -File scripts\start-local.ps1 -NoBrowser
set "localExit=%ERRORLEVEL%"
if not "%localExit%"=="0" pause
exit /b %localExit%
