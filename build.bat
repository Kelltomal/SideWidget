@echo off
setlocal

where pwsh.exe >nul 2>&1
if errorlevel 1 (
    if exist "%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell\pwsh.exe" (
        set "PATH=%USERPROFILE%\.cache\codex-runtimes\codex-primary-runtime\dependencies\native\powershell;%PATH%"
    )
)

where pwsh.exe >nul 2>&1
if errorlevel 1 (
    echo PowerShell 7 is required. Install it and add pwsh.exe to PATH.
    pause
    exit /b 1
)

where npm.cmd >nul 2>&1
if errorlevel 1 (
    echo Node.js with npm is required. Install it and add npm.cmd to PATH.
    pause
    exit /b 1
)

pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\bootstrap.ps1"
if errorlevel 1 goto failed

pwsh.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\build.ps1"
if errorlevel 1 goto failed

echo.
echo Launch "%~dp0artifacts\release\SideWidget.App.exe"
pause
exit /b 0

:failed
echo.
echo Build failed. See the error above.
pause
exit /b 1
