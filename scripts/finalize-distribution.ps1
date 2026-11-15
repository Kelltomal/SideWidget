param([Parameter(Mandatory=$true)][string]$ReleaseDirectory, [switch]$Lite)
$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot -Parent
$release=[IO.Path]::GetFullPath($ReleaseDirectory)
$dotnet=Join-Path $root '.tools/dotnet/dotnet.exe'
if(!(Test-Path -LiteralPath $dotnet)){$dotnet='dotnet'}
if(!$Lite){
    & (Join-Path $PSScriptRoot 'bundle-webview2.ps1') -Destination (Join-Path $release 'WebView2Runtime')
    foreach($name in @('vcruntime140.dll','vcruntime140_1.dll','msvcp140.dll','msvcp140_codecvt_ids.dll','concrt140.dll','vccorlib140.dll')){
        Copy-Item -LiteralPath (Join-Path $release "WebView2Runtime/$name") -Destination (Join-Path $release $name) -Force
    }
}
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $release 'README.md') -Force
$variant=if($Lite){'lite'}else{'full'}
& $dotnet run --project (Join-Path $root 'tools/SideWidget.Distribution') -c Release -- manifest $release $variant
if($LASTEXITCODE){throw 'Portable distribution verification failed'}
