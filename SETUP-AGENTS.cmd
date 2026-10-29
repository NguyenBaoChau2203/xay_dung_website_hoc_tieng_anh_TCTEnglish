@echo off
setlocal
cd /d "%~dp0"
where node >nul 2>nul
if errorlevel 1 (
  echo Install Node.js 22 or newer with npm from https://nodejs.org/ then reopen this file.
  pause
  exit /b 1
)
node scripts\agent-tools.cjs setup
set "setupExit=%ERRORLEVEL%"
echo.
echo See docs\team-agent-setup.md for editor trust and the optional CodeGraph index.
pause
exit /b %setupExit%
