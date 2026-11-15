@echo off
setlocal EnableExtensions DisableDelayedExpansion
rem Shared x64 runtimes for SideWidget Lite. Run once on each PC.
set "runtimeVersion=8000.994.2142.0"
set "runtimeInstaller=https://aka.ms/windowsappsdk/1.8/1.8.260921001/windowsappruntimeinstall-x64.exe"
set "manualHelp=https://github.com/Kelltomal/SideWidget/blob/main/docs/github/README.en.md#getting-started"
set "exitCode=0"
pushd "%SystemRoot%\System32" || exit /b 1

winget.exe --version >nul 2>&1
if errorlevel 1 (
    echo WinGet is unavailable. Install Microsoft App Installer or use the manual links:
    echo %manualHelp%
    set "exitCode=1"
    goto finish
)

echo Installing SideWidget Lite prerequisites...
call :install "Microsoft.VCRedist.2015+.x64" || goto failed
call :install "Microsoft.DotNet.Runtime.10" || goto failed
call :install "Microsoft.DotNet.AspNetCore.10" || goto failed
call :install "Microsoft.EdgeWebView2Runtime" || goto failed

rem WinGet's Windows App Runtime 1.8 package may lag the required SDK patch.
rem Skip the official installer if the current user already has a sufficient runtime.
powershell.exe -NoLogo -NoProfile -Command ^
  "$ErrorActionPreference = 'Stop';" ^
  "$minimum = [version]$env:runtimeVersion;" ^
  "function HasRuntime { $null -ne (Get-AppxPackage -Name Microsoft.WindowsAppRuntime.1.8 | Where-Object { $_.Architecture -eq 'X64' -and $_.Version -ge $minimum } | Select-Object -First 1) }" ^
  "if (HasRuntime) { Write-Host 'Windows App Runtime 1.8 x64 is already installed.'; exit 0 }" ^
  "$installer = Join-Path ([IO.Path]::GetTempPath()) ('SideWidget-WindowsAppRuntime-' + [guid]::NewGuid().ToString('N') + '.exe');" ^
  "try {" ^
  "  Write-Host 'Installing Windows App Runtime 1.8 x64...';" ^
  "  Invoke-WebRequest -UseBasicParsing -Uri $env:runtimeInstaller -OutFile $installer;" ^
  "  $sig = Get-AuthenticodeSignature -LiteralPath $installer;" ^
  "  if ($sig.Status -ne 'Valid' -or $sig.SignerCertificate.Subject -notmatch '(?:^|,\s*)CN=Microsoft Corporation(?:,|$)') { throw 'Microsoft installer signature verification failed.' }" ^
  "  $installed = Start-Process -FilePath $installer -ArgumentList '--quiet' -Wait -PassThru;" ^
  "  if ($installed.ExitCode -ne 0 -or !(HasRuntime)) { throw ('Windows App Runtime installation did not complete. Installer exit code: ' + $installed.ExitCode) }" ^
  "} finally { if (Test-Path -LiteralPath $installer) { Remove-Item -LiteralPath $installer -Force } }"
if errorlevel 1 goto failed

echo.
echo SideWidget Lite prerequisites are ready. Start SideWidget.exe.
goto finish

:install
winget.exe install --id "%~1" --exact --source winget --architecture x64 --no-upgrade --accept-package-agreements --accept-source-agreements --disable-interactivity
set "installCode=%errorlevel%"
rem WinGet returns these HRESULTs for an already installed package or no update.
if "%installCode%"=="-1978335135" exit /b 0
if "%installCode%"=="-1978335189" exit /b 0
if not "%installCode%"=="0" echo Installation failed for %~1.
exit /b %installCode%

:failed
set "exitCode=%errorlevel%"
echo.
echo A dependency could not be installed. See:
echo %manualHelp%

:finish
popd
if /I not "%~1"=="--no-pause" pause
endlocal & exit /b %exitCode%
