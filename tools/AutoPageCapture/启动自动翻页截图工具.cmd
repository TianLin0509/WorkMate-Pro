@echo off
setlocal
set "APP_ROOT=%~dp0"
set "PACKAGED_APP=%APP_ROOT%output\AutoPageCapture.exe"
set "SOURCE_APP=%APP_ROOT%src\main.py"

if exist "%PACKAGED_APP%" (
    start "" "%PACKAGED_APP%"
    exit /b 0
)

where pyw.exe >nul 2>nul
if not errorlevel 1 (
    start "" pyw.exe -3.12 "%SOURCE_APP%"
    exit /b 0
)

where pythonw.exe >nul 2>nul
if not errorlevel 1 (
    start "" pythonw.exe "%SOURCE_APP%"
    exit /b 0
)

echo AutoPageCapture.exe and Python were not found.
echo Please run build.ps1 first, or install Python 3.12 and Pillow.
pause
exit /b 1
