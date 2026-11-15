param([switch]$Inspect)
$root=Split-Path $PSScriptRoot -Parent
$exe=Join-Path $root 'artifacts\release\SideWidget.App.exe'
if(!(Test-Path -LiteralPath $exe)){throw 'Run scripts/build.ps1 first'}
if($Inspect){Start-Process -FilePath $exe -ArgumentList '--inspect','--diagnostics' -WindowStyle Hidden}
else {Start-Process -FilePath $exe -WindowStyle Hidden}
