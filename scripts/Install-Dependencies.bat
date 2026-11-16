@echo off
setlocal
start "" /wait "%~dp0SideWidget.exe" --install-dependencies
exit /b %errorlevel%
