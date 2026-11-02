@echo off
REM Windows wrapper so local-speech can Start-Process a fake executable.
setlocal
set SCRIPT_DIR=%~dp0
python "%SCRIPT_DIR%fake_whisper_server.py" %*
