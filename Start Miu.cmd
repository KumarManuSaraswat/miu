@echo off
cd /d "%~dp0"
if not exist "dist\Miu.exe" (
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
  if errorlevel 1 (
    echo Miu could not be built. See the message above.
    pause
    exit /b 1
  )
)
start "" "%~dp0dist\Miu.exe"
